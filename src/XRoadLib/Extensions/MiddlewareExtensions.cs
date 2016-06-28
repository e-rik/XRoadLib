#if NETSTANDARD1_5

using System;
using Microsoft.AspNetCore.Builder;

namespace XRoadLib.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseXRoadLib(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<XRoadLibMiddleware>();
        }

        public static IApplicationBuilder UseXRoadLib(this IApplicationBuilder builder, Action<XRoadLibOptions> optionBuilder)
        {
            var options = new XRoadLibOptions();
            optionBuilder(options);
            return builder.UseMiddleware<XRoadLibMiddleware>(options);
        }
    }
}

#endif