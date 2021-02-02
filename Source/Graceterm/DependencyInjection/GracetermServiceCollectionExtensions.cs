using System;
using Graceterm;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class GracetermServiceCollectionExtensions
    {
        public static IServiceCollection AddGraceterm(this IServiceCollection services)
        {
            services.AddTransient<ILifetimeGracetermService, LifetimeGracetermService>();

            return services;
        }

        public static IServiceCollection AddGraceterm(this IServiceCollection services, GracetermOptions configureOptions)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configureOptions is null)
            {
                configureOptions = new GracetermOptions();
            }

            services.AddSingleton(configureOptions);

            return AddGraceterm(services);
        }

        public static IServiceCollection AddGraceterm(this IServiceCollection services, Action<GracetermOptions> actionOptions)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (actionOptions is null)
            {
                throw new ArgumentNullException(nameof(actionOptions));
            }

            var options = new GracetermOptions();
            actionOptions.Invoke(options);

            return AddGraceterm(services, options);
        }
    }
}
