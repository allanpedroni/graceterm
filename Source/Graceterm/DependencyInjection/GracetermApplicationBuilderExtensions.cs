using Graceterm;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Graceterm ApplicationBuilder extensions provides convenient way to add Graceterm middleware to your application pipeline.
    /// </summary>
    public static class GracetermApplicationBuilderExtensions
    {
        /// <summary>
        /// Add Graceterm middleware to requests pipeline with default options <see cref="GracetermOptions"/>.
        /// In order to graceterm work properly, you should add it just after log configuration (if you have one), before any other middleware like Mvc, Swagger etc.
        /// </summary>
        /// <param name="applicationBuilder">The applicationBuilder to configure.</param>
        /// <returns>The applicationBuilder</returns>
        public static IApplicationBuilder UseGraceterm(this IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseMiddleware<GracetermMiddleware>();

            return applicationBuilder;
        }
    }
}
