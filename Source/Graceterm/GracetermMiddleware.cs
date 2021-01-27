using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// <summary>
        /// The logger category for log events created here.
        /// </summary>
        public const string LoggerCategory = "Graceterm";

        private readonly RequestDelegate next;
        private readonly ILifetimeGracetermService applicationLifetime;
        private readonly ILogger logger;
        private readonly GracetermOptions options;

        private readonly Func<HttpContext, Task> postSigtermRequestsHandler = async (httpContext) =>
        {
            httpContext.Response.StatusCode = 503;
            await httpContext.Response.WriteAsync("503 - Service unavailable.");
        };

        public GracetermMiddleware(RequestDelegate next, ILifetimeGracetermService applicationLifetime,
            ILoggerFactory loggerFactory, IOptions<GracetermOptions> options)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.applicationLifetime = applicationLifetime ??
                throw new ArgumentNullException(nameof(applicationLifetime));
            logger = loggerFactory?.CreateLogger(LoggerCategory) ??
                throw new ArgumentNullException(nameof(loggerFactory));
            this.options = options?.Value ??
                throw new ArgumentException(nameof(options));

            if (this.options.CustomPostSigtermRequestsHandler != null)
            {
                postSigtermRequestsHandler = this.options.CustomPostSigtermRequestsHandler;
            }
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (ShouldIgnore(httpContext))
            {
                await next.Invoke(httpContext);
            }
            else if (applicationLifetime.StopRequested)
            {
                await HandleIncommingRequestAfterAppAskedToTerminate(httpContext);
            }
            else
            {
                applicationLifetime.IncrementRequestCount();

                await next.Invoke(httpContext);

                applicationLifetime.DecrementRequestCount();
            }

            bool ShouldIgnore(HttpContext httpContext)
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
        }

        private async Task HandleIncommingRequestAfterAppAskedToTerminate(HttpContext httpContext)
        {
            logger.LogCritical("Request received, but this application instance is not accepting new requests " +
                "because it asked for terminate (eg.: a sigterm were received). Seding response as service unavailable (HTTP 503).");

            await postSigtermRequestsHandler.Invoke(httpContext);
        }
    }
}
