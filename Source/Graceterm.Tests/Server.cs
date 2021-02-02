using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Graceterm.Tests
{
    public class Server
    {
        public const string ResponseContent = "hello";
        private TestServer testServer;
        private IHostApplicationLifetime applicationLifetime;

        public Task<HttpResponseMessage> CreateRequest()
        {
            var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), "/");

            return Task.Factory.StartNew(() =>
                    testServer.CreateClient().GetAsync("/").Result,
                TaskCreationOptions.LongRunning);
        }

        public IEnumerable<Task<HttpResponseMessage>> CreateRequests(int num)
        {
            var requestTasks = new List<Task<HttpResponseMessage>>();

            for (var i = 0; i < num; i++)
            {
                requestTasks.Add(CreateRequest());
            }

            return requestTasks;
        }

        public void Stop()
        {
            applicationLifetime.StopApplication();
        }

        public static Server Create(GracetermOptions gracetermOptions)
        {
            return new Server(gracetermOptions);
        }

        public static Server Create()
        {
            return new Server(null);
        }

        protected Server(GracetermOptions gracetermOptions)
        {
            LifetimeGracetermService.DisableTerminationFallback = true;

            var webHostBuilder = new WebHostBuilder()
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.AddDebug();
                    loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                })
                .Configure(app =>
                {
                    applicationLifetime = app.ApplicationServices.GetService<IHostApplicationLifetime>();

                    if (applicationLifetime == null)
                    {
                        throw new InvalidOperationException("Could not get IApplicationLifetime service!");
                    }

                    app.UseGraceterm();

                    app.Run(async context =>
                    {
                        context.Response.StatusCode = 200;
                        await Task.Delay(new Random().Next(10000, 20000));
                        await context.Response.WriteAsync(ResponseContent);
                    });
                })
                .ConfigureServices(s => s.AddGraceterm(gracetermOptions));

            testServer = new TestServer(webHostBuilder);
        }
    }
}
