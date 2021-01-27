using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Graceterm
{
    public class LifetimeGracetermService : ILifetimeGracetermService
    {
        public LifetimeGracetermService(
            ILoggerFactory loggerFactory,
            IHostApplicationLifetime appLifetime,
            IOptions<GracetermOptions> options)
        {
            logger = loggerFactory?.CreateLogger<LifetimeGracetermService>() ??
                throw new ArgumentNullException(nameof(loggerFactory));
            this.appLifetime = appLifetime;
            this.options = options?.Value ??
                throw new ArgumentException(nameof(options));

            appLifetime.ApplicationStopping.Register(OnStopping);
            appLifetime.ApplicationStopped.Register(OnStopped);
        }

        private readonly ILogger logger;
        private readonly IHostApplicationLifetime appLifetime;
        private readonly GracetermOptions options;
        private volatile static bool stopRequested = false;
        private volatile static int stopRequestedTime = 0;
        private static readonly long AssemblyLoadedWhenInTicks = DateTime.Now.Ticks;
        private static volatile int requestCount = 0;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static int RequestCount => requestCount;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TimeoutOccurredWithPenddingRequests { get; private set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool DisableTerminationFallback { get; set; } = false;

        private void OnStopped()
        {
            logger.LogInformation("ApplicationStopped event fired.");
        }

        private void OnStopping()
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

        public bool StopRequested => stopRequested;

        public void IncrementRequestCount() => Interlocked.Increment(ref requestCount);

        public void DecrementRequestCount() => Interlocked.Decrement(ref requestCount);

        private int ComputeIntegerTimeReference()
            =>
            (int)(((DateTime.Now.Ticks - AssemblyLoadedWhenInTicks) / TimeSpan.TicksPerMillisecond / 1000) & 0x3fffffff);

        private bool TimeoutOccurred()
        {
            return ComputeIntegerTimeReference() - stopRequestedTime > options.TimeoutSeconds;
        }
    }
}
