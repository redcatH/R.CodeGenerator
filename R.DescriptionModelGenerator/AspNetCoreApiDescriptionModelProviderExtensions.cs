using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace R.DescriptionModelGenerator
{
    public static class AspNetCoreApiDescriptionModelProviderExtensions
    {
        public static IServiceCollection AddAspNetCoreApiDescriptionModelProvider(this IServiceCollection services)
        {
            services.AddSingleton<AspNetCoreApiDescriptionModelProviderService>();
            return services;
        }

        // 保留原有扩展方法（可用于其他用途）
        public static IApplicationBuilder UseAspNetCoreApiDescriptionModelProvider(this IApplicationBuilder app)
        {
            // 这里不再注册 UseEndpoints，避免冲突
            return app;
        }

        // 新增：用于 endpoints.MapAspNetCoreApiDescriptionModelProviderEndpoint()
        public static void MapAspNetCoreApiDescriptionModelProviderEndpoint(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api-description-model", async context =>
            {
                var service = context.RequestServices.GetService<AspNetCoreApiDescriptionModelProviderService>();
                var result = service?.GetApiDescriptionModel();
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(result);
            }).AllowAnonymous();
        }
    }
}