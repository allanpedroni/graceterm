using System;
using Microsoft.Extensions.DependencyInjection;

namespace Graceterm
{
    public static class GracetermServiceCollectionExtensions
    {
        public static IServiceCollection AddGraceterm(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddTransient<ILifetimeGracetermService, LifetimeGracetermService>();

            //services.AddHostedService<LifetimeGracetermService>();

            return services;
        }
    }
}
