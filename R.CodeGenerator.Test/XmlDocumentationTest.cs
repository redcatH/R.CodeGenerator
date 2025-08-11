using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using R.DescriptionModelGenerator;

namespace R.CodeGenerator.Test;

/// <summary>
/// XML文档注释测试程序
/// </summary>
public partial class XmlDocumentationTest
{
    public static void Main1()
    {
        Console.WriteLine("开始测试 XML 文档注释功能...");

        var builder = WebApplication.CreateBuilder();
        
        // 添加API Explorer服务
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        
        var app = builder.Build();

        // 手动创建服务提供者来获取API描述
        using var scope = app.Services.CreateScope();
        var apiDescriptionProvider = scope.ServiceProvider.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        
        var service = new AspNetCoreApiDescriptionModelProviderService(apiDescriptionProvider);
        var result = service.GetApiDescriptionModel(includeTypes: true);

        // 输出API信息和注释
        Console.WriteLine("\n=== API 描述信息 ===");
        foreach (var api in result.Apis)
        {
            Console.WriteLine($"控制器: {api.Controller}");
            Console.WriteLine($"方法: {api.Action}");
            Console.WriteLine($"HTTP方法: {api.HttpMethod}");
            Console.WriteLine($"路径: {api.Path}");
            
            if (!string.IsNullOrEmpty(api.Summary))
                Console.WriteLine($"摘要: {api.Summary}");
            
            if (!string.IsNullOrEmpty(api.Remarks))
                Console.WriteLine($"备注: {api.Remarks}");
            
            Console.WriteLine($"参数数量: {api.Parameters.Count}");
            Console.WriteLine("---");
        }

        // 输出类型信息和注释
        Console.WriteLine("\n=== 类型描述信息 ===");
        foreach (var type in result.Types.Values)
        {
            Console.WriteLine($"类型: {type.Name}");
            Console.WriteLine($"命名空间: {type.Namespace}");
            
            if (!string.IsNullOrEmpty(type.Summary))
                Console.WriteLine($"摘要: {type.Summary}");
            
            if (!string.IsNullOrEmpty(type.Remarks))
                Console.WriteLine($"备注: {type.Remarks}");
            
            Console.WriteLine($"属性数量: {type.Properties.Count}");
            
            // 输出属性信息
            foreach (var prop in type.Properties)
            {
                Console.WriteLine($"  属性: {prop.Name} ({prop.Type})");
                if (!string.IsNullOrEmpty(prop.Summary))
                    Console.WriteLine($"    摘要: {prop.Summary}");
                if (!string.IsNullOrEmpty(prop.Remarks))
                    Console.WriteLine($"    备注: {prop.Remarks}");
            }
            Console.WriteLine("---");
        }

        // 输出JSON格式的结果
        Console.WriteLine("\n=== JSON 输出 ===");
        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var jsonOutput = JsonSerializer.Serialize(result, jsonOptions);
        Console.WriteLine(jsonOutput);

        Console.WriteLine("\nXML 文档注释功能测试完成！");
    }
}
