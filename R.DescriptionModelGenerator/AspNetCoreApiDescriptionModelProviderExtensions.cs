using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace R.DescriptionModelGenerator
{
    public static class AspNetCoreApiDescriptionModelProviderExtensions
    {
        public static string JoinAsString(this IEnumerable<string> source, string separator)
        {
            return string.Join(separator, source);
        }

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
                context.Response.ContentType = "application/json; charset=utf-8";
                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                };

                // 使用 UTF-8 编码的 StreamWriter
                await using var writer = new StreamWriter(context.Response.Body, new UTF8Encoding(false));
                await JsonSerializer.SerializeAsync(writer.BaseStream, result, options);
                await writer.FlushAsync();
            }).AllowAnonymous();
        }
    }
}