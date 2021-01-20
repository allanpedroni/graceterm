using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Graceterm
{
    /// <summary>
    /// Graceterm middleware provides implementation to ensure graceful shutdown of aspnet core applications. 
    /// It was originally written to get zero downtime while performing Kubernetes rolling updates.
    /// The basic concept is: After aplication received a SIGTERM (a signal asking it to terminate), 
    /// Graceterm will hold it alive till all pending requests are completed or a timeout ocurr. 
    /// </summary>
    public class GracetermMiddleware
    {

        //MELHOR EXPLICAÇÂO GOOGLE
        //https://cloud.google.com/blog/products/gcp/kubernetes-best-practices-terminating-with-grace

        //DOC preStop
        //https://kubernetes.io/docs/concepts/containers/container-lifecycle-hooks/#hook-details

        //DOC POD TERMINATION
        //https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/#pod-termination

        //EXAMPLE
        //https://kubernetes.io/docs/tasks/configure-pod-container/attach-handler-lifecycle-event/



        //FLUXO usando preStop
        //https://drek4537l1klr.cloudfront.net/luksa/Figures/17fig05_alt.jpg
        //https://blog.gruntwork.io/delaying-shutdown-to-wait-for-pod-deletion-propagation-445f779a8304
        //https://www.syntaxsuccess.com/viewarticle/deploying-kubernetes-with-zero-downtime ------
        //https://learnk8s.io/graceful-shutdown ------
        //https://blog.markvincze.com/graceful-termination-in-kubernetes-with-asp-net-core/
        //https://training.play-with-kubernetes.com/kubernetes-workshop/ xxxxxxxxx

        //PAREI AQUI
        //https://github.com/Lybecker/k8s-friendly-aspnetcore

        //shutdown api - https://shazwazza.com/post/aspnet-core-application-shutdown-events/
        //https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-3/

        //POD terminando
        //preStop é executado sem precisar alterar a aplicação
        //incluir um sleep para que o pod seja removido do endpoint controller
        //SIGTERM é enviado se acabar a execução no preStop
        //roda antes do status terminated.
        //      spec:
        //containers:
        //- image: luksa/kubia
        //  name: kubia
        //  ports:
        //  - containerPort: 8080
        //    protocol: TCP
        //  lifecycle:
        //    preStop:
        //      httpGet:
        //        port: 8080
        //        path: shutdown
        //terminationGracePeriodSeconds 

        //OBS: preStop e terminationGracePeriodSeconds  trabalham em paralelo, se não acabar de processar
        //o preStop, será esgotado o GracePeriodo e enviado um SIGKILL

        //Se terminationGracePeriodSeconds expirar, é enviado o SIGKILL


        //https://blog.gruntwork.io/avoiding-outages-in-your-kubernetes-cluster-using-poddisruptionbudgets-ef6a4baa5085

        /// <summary>
        /// The logger category for log events created here.
        /// </summary>
        public const string LoggerCategory = "Graceterm";

        private readonly RequestDelegate next;
        //private static volatile object lockPad = new object();
        private readonly ILogger logger;
        private static volatile int requestCount = 0;
        private readonly GracetermOptions options;

        private readonly Func<HttpContext, Task> postSigtermRequestsHandler = async (httpContext) =>
        {
            httpContext.Response.StatusCode = 503;
            await httpContext.Response.WriteAsync("503 - Service unavailable.");
        };

        #region [Test properties]

        //
        // this properties exists for test purpose only (used in TimeoutTests.ShouldStopIfTimeoutOccur)
        //

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static int RequestCount => requestCount;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TimeoutOccurredWithPenddingRequests { get; private set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool DisableTerminationFallback { get; set; } = false;

        #endregion

        public GracetermMiddleware(RequestDelegate next, IApplicationLifetime applicationLifetime,
            ILoggerFactory loggerFactory, IOptions<GracetermOptions> options)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            logger = loggerFactory?.CreateLogger(LoggerCategory) ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.options = options?.Value ?? throw new ArgumentException(nameof(options));

            if (applicationLifetime == null)
            {
                throw new ArgumentNullException(nameof(applicationLifetime));
            }

            if (this.options.CustomPostSigtermRequestsHandler != null)
            {
                postSigtermRequestsHandler = this.options.CustomPostSigtermRequestsHandler;
            }

            applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
            applicationLifetime.ApplicationStopped.Register(OnApplicationStopped);
        }

        private volatile static bool stopRequested = false;
        private volatile static int stopRequestedTime = 0;
        private static long assemblyLoadedWhenInTicks = DateTime.Now.Ticks;

        private int ComputeIntegerTimeReference()
            =>
            (int)(((DateTime.Now.Ticks - assemblyLoadedWhenInTicks) / TimeSpan.TicksPerMillisecond / 1000) & 0x3fffffff);

        private bool TimeoutOccurred()
        {
            return ComputeIntegerTimeReference() - stopRequestedTime > options.TimeoutSeconds;
        }

        private void OnApplicationStopping()
        {
            logger.LogInformation("Sigterm received, will waiting for pending requests to complete if has any.");

            do
            {
                Task.Delay(1000).Wait();
                logger.LogInformation("Waiting for pending requests, current request count: {RequestCount}.", requestCount);

                if (!stopRequested)
                {
                    stopRequested = true;
                    stopRequestedTime = ComputeIntegerTimeReference();
                }
            }

            while (requestCount > 0 && !TimeoutOccurred());

            if (requestCount > 0 && TimeoutOccurred())
            {
                logger.LogCritical("Timeout ocurred! Application will terminate with {RequestCount} pedding requests.", requestCount);

                // This assignment is done for tests purpose only, TimeoutOccurredWithPenddingRequests will be checked in TimeoutTests.ShouldStopIfTimeoutOccur
                // to verify if the condition occured.
                TimeoutOccurredWithPenddingRequests = true;

                if (!DisableTerminationFallback)
                {
                    // Ensure to terminate process if it dont terminate by it self.
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000); // Give a last chance to process terminate by it self
                        logger.LogWarning("(TIMEOUT) Forcing process to exit, it should terminated by it self, if you seeing this message, must be something wrong.");
                        await Task.Delay(2000); // Give a break in order to the above log got written
                        Environment.Exit(124); // Terminate the process. 124 exit code means timeout for unix systems
                    });
                }
                
            }
            else
            {
                logger.LogInformation("Pending requests were completed, application will now terminate gracefully.");
            }
        }

        private void OnApplicationStopped()
        {
            logger.LogInformation("ApplicationStopped event fired.");
        }

        

        public async Task Invoke(HttpContext httpContext)
        {
            if (ShouldIgnore(httpContext))
            {
                await next.Invoke(httpContext);
            }
            else if (stopRequested)
            {
                await HandleIncommingRequestAfterAppAskedToTerminate(httpContext);
            }
            else
            {
                Interlocked.Increment(ref requestCount);

                await next.Invoke(httpContext);

                Interlocked.Decrement(ref requestCount);
            }
        }

        private bool ShouldIgnore(HttpContext httpContext)
        {
            foreach (var ignoredPath in options.IgnoredPaths)
            {
                if (httpContext.Request.Path.StartsWithSegments(ignoredPath))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task HandleIncommingRequestAfterAppAskedToTerminate(HttpContext httpContext)
        {
            logger.LogCritical("Request received, but this application instance is not accepting new requests because it asked for terminate (eg.: a sigterm were received). Seding response as service unavailable (HTTP 503).");

            await postSigtermRequestsHandler.Invoke(httpContext);
        }
    }
}
