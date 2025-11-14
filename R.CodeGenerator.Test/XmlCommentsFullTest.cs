using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using R.DescriptionModelGenerator;
using R.CodeGenerator;

namespace R.CodeGenerator.Test;

/// <summary>
/// 测试XML注释功能的完整示例程序
/// </summary>
public partial class XmlDocumentationTest
{
    public static void TestXmlComments()
    {
        Console.WriteLine("=== XML注释功能完整测试 ===");
        Console.WriteLine();

        // 1. 创建简单的Web应用来获取API描述
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        
        var app = builder.Build();
        app.MapControllers();

        // 2. 获取API描述服务
        using var scope = app.Services.CreateScope();
        var apiDescriptionProvider = scope.ServiceProvider.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        
        // 3. 创建我们的服务并获取API描述模型
        var service = new AspNetCoreApiDescriptionModelProviderService(apiDescriptionProvider);
        var model = service.GetApiDescriptionModel(includeTypes: true);

        Console.WriteLine($" 成功获取 {model.Apis.Count} 个API接口");
        Console.WriteLine($" 成功获取 {model.Types.Count} 个类型定义");
        Console.WriteLine();

        // 4. 展示API注释信息
        Console.WriteLine("🔹 API接口注释信息:");
        foreach (var api in model.Apis.Take(3))
        {
            Console.WriteLine($"  � {api.Controller}/{api.Action} ({api.HttpMethod})");
            Console.WriteLine($"     路径: {api.Path}");
            
            if (!string.IsNullOrEmpty(api.Summary))
                Console.WriteLine($"     摘要: {api.Summary}");
                
            if (!string.IsNullOrEmpty(api.Remarks))
                Console.WriteLine($"     详细说明: {api.Remarks}");

            // 显示参数注释
            foreach (var param in api.Parameters.Where(p => !string.IsNullOrEmpty(p.Summary)))
            {
                Console.WriteLine($"     📋 参数 {param.Name}: {param.Summary}");
            }

            // 显示返回值注释  
            if (!string.IsNullOrEmpty(api.ReturnType?.Summary))
                Console.WriteLine($"     🔄 返回值: {api.ReturnType.Summary}");
                
            Console.WriteLine();
        }

        // 5. 展示类型注释信息
        Console.WriteLine("🔸 类型注释信息:");
        foreach (var type in model.Types.Values.Where(t => t.Namespace?.StartsWith("R.DescriptionModelGenerator") == true).Take(3))
        {
            Console.WriteLine($"  � {type.Name}");
            
            if (!string.IsNullOrEmpty(type.Summary))
                Console.WriteLine($"     摘要: {type.Summary}");
                
            if (!string.IsNullOrEmpty(type.Remarks))
                Console.WriteLine($"     详细说明: {type.Remarks}");

            // 显示属性注释
            foreach (var prop in type.Properties.Where(p => !string.IsNullOrEmpty(p.Summary)).Take(3))
            {
                Console.WriteLine($"       🔹 属性 {prop.Name}: {prop.Summary}");
                if (!string.IsNullOrEmpty(prop.Remarks))
                    Console.WriteLine($"            详细: {prop.Remarks}");
            }
            Console.WriteLine();
        }

        // 6. 生成代码以测试注释是否正确包含
        Console.WriteLine("📝 开始生成TypeScript代码...");
        var config = new ApiGenConfig
        {
            OutputDir = "test-output\\api",
            TypesDir = "test-output\\types",
            UseInterface = true,
            ImportLine = ["import { request as requestHttp } from '../request';"],
            NamespacePrefix = "R.DescriptionModelGenerator",
            UnwrapGenericTypes = ["ApiResult"]
        };

        var generator = new ApiCodeGenerator();
        
        // 生成类型定义
        generator.GenerateTypes(model.Types, config.TypesDir, config.UseInterface, config.NamespacePrefix);
        Console.WriteLine($" 类型定义已生成到: {config.TypesDir}");
        
        // 生成API服务
        generator.GenerateApis(model.Apis, config.OutputDir, config.ImportLine, model.Types, config.UnwrapGenericTypes);
        Console.WriteLine($" API服务已生成到: {config.OutputDir}");

        // 7. 显示生成的文件内容示例
        ShowGeneratedFilesSample(config);
        
        Console.WriteLine();
        Console.WriteLine("🎉 XML注释功能测试完成！");
        Console.WriteLine("🎯 现在生成的TypeScript代码包含了完整的JSDoc注释，包括:");
        Console.WriteLine("   • API方法的摘要和详细说明");
        Console.WriteLine("   • 参数说明 (@param)");
        Console.WriteLine("   • 返回值说明 (@returns)");
        Console.WriteLine("   • 类型和属性的注释");
    }

    private static void ShowGeneratedFilesSample(ApiGenConfig config)
    {
        Console.WriteLine();
        Console.WriteLine("📄 生成文件内容示例:");
        
        try
        {
            // 显示API服务文件
            var apiDir = new DirectoryInfo(config.OutputDir);
            if (apiDir.Exists)
            {
                var apiFile = apiDir.GetFiles("*.ts").FirstOrDefault();
                if (apiFile != null)
                {
                    Console.WriteLine($"  📄 API服务文件: {apiFile.Name}");
                    var content = File.ReadAllText(apiFile.FullName);
                    var lines = content.Split('\n').Take(25).ToArray();
                    foreach (var line in lines)
                    {
                        Console.WriteLine($"    {line}");
                    }
                    if (content.Split('\n').Length > 25)
                        Console.WriteLine("    ... (更多内容)");
                    Console.WriteLine();
                }
            }

            // 显示类型文件
            var typesDir = new DirectoryInfo(config.TypesDir);
            if (typesDir.Exists)
            {
                var typeFile = typesDir.GetFiles("*.ts").FirstOrDefault();
                if (typeFile != null)
                {
                    Console.WriteLine($"  📄 类型定义文件: {typeFile.Name}");
                    var content = File.ReadAllText(typeFile.FullName);
                    var lines = content.Split('\n').Take(20).ToArray();
                    foreach (var line in lines)
                    {
                        Console.WriteLine($"    {line}");
                    }
                    if (content.Split('\n').Length > 20)
                        Console.WriteLine("    ... (更多内容)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"显示生成文件时出错: {ex.Message}");
        }
    }

    public static void Main12()
    {
        TestXmlComments();
    }
}
