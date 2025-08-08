// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using R.DescriptionModelGenerator;

namespace R.CodeGenerator.Test;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("程序启动，准备解析参数...");
        // args = new[] {  "" , "http://localhost:5200/api-description-model"};
        // 1. 解析 config 路径参数
        string configPath = args.Length > 0 ? args[0] : "config.json";
        Console.WriteLine($"配置文件路径: {configPath}");
        ApiGenConfig config;
        if (File.Exists(configPath))
        {
            Console.WriteLine($"检测到配置文件 {configPath}，开始读取...");
            var configJson = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<ApiGenConfig>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new Exception($"配置文件 {configPath} 解析失败");
            Console.WriteLine("配置文件读取并解析成功。");
        }
        else
        {
            Console.WriteLine("未检测到配置文件，使用默认配置。");
            // 默认配置
            config = new ApiGenConfig
            {
                OutputDir = "./api",
                TypesDir = "./types",
                ImportLine = new[] { "import { request } from '../request';" },
                NamespacePrefix    = "VividCMS"
            };
        }

        // 2. 解析 swagger.json 来源参数
        string swaggerSource = args.Length > 1 ? args[1] : "swagger.json";
        Console.WriteLine($"Swagger 源: {swaggerSource}");
        string json;
        if (swaggerSource.StartsWith("http://") || swaggerSource.StartsWith("https://"))
        {
            Console.WriteLine("检测到远程 swagger 源，开始请求...");
            using var http = new System.Net.Http.HttpClient();
            json = http.GetStringAsync(swaggerSource).GetAwaiter().GetResult();
            Console.WriteLine("远程 swagger 数据获取成功。");
        }
        else
        {
            Console.WriteLine("检测到本地 swagger 文件，开始读取...");
            json = File.ReadAllText(swaggerSource);
            Console.WriteLine("本地 swagger 文件读取成功。");
        }

        Console.WriteLine("开始解析 swagger 数据...");
        var model = JsonSerializer.Deserialize<ApiDescriptionModelResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (model == null)
            throw new Exception("swagger.json 解析失败");
        Console.WriteLine("swagger 数据解析成功。");

        var generator = new ApiCodeGenerator();
        Console.WriteLine("开始生成类型定义...");
        generator.GenerateTypes(model.Types, config.TypesDir, config.UseInterface,config.NamespacePrefix);
        Console.WriteLine("类型定义生成完成。");
        Console.WriteLine("开始生成 API 代码...");
        generator.GenerateApis(model.Apis, config.OutputDir, config.ImportLine, config.TypesDir,model.Types);
        Console.WriteLine("API 代码生成完成。");
        Console.WriteLine("全部流程执行完毕。");
    }
}