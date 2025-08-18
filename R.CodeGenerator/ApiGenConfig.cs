namespace R.CodeGenerator;

public class ApiGenConfig
{
    public string OutputDir { get; set; } = "./api";
    public string TypesDir { get; set; } = "./types";
    public string[] ImportLine { get; set; } = new[] { "import { request as requestHttp } from '../request';" };
    public string ApiPrefix { get; set; } = "";
    public bool UseInterface { get; set; } = true;
    public string NamespacePrefix { get; set; }
    public string[] UnwrapGenericTypes { get; set; }
    
    /// <summary>
    /// API 模板文件路径（相对路径或绝对路径）
    /// </summary>
    public string TemplatePath { get; set; } = "Templates/api_service_vben.sbn";
    
    /// <summary>
    /// 是否在生成完成后执行自定义命令
    /// </summary>
    public bool EnablePostGenerationCommand { get; set; } = false;
    
    /// <summary>
    /// 执行命令的工作目录（相对于当前目录或绝对路径）
    /// </summary>
    public string? CommandWorkingDirectory { get; set; }
    
    /// <summary>
    /// 要执行的命令（例如："npm run format" 或 "git add ."）
    /// </summary>
    public string? Command { get; set; }
    
    /// <summary>
    /// 命令参数（如果需要分开指定）
    /// </summary>
    public string? CommandArguments { get; set; }
    
    /// <summary>
    /// 命令执行超时时间（毫秒），0表示无限制
    /// </summary>
    public int CommandTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// 是否等待命令执行完成再退出程序
    /// </summary>
    public bool WaitForCommandCompletion { get; set; } = true;
}