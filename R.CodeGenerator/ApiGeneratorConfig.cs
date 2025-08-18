namespace R.CodeGenerator;

/// <summary>
/// API代码生成配置
/// </summary>
public class ApiGeneratorConfig
{
    /// <summary>
    /// 注释清理器配置
    /// </summary>
    public CommentSanitizerConfig CommentConfig { get; set; } = new();
    
    /// <summary>
    /// 是否生成详细的注释
    /// </summary>
    public bool GenerateDetailedComments { get; set; } = true;
    
    /// <summary>
    /// 是否在模板中启用额外的安全检查
    /// </summary>
    public bool EnableTemplateSafety { get; set; } = true;
    
    /// <summary>
    /// 类型前缀，用于避免命名冲突
    /// </summary>
    public string TypePrefix { get; set; } = "types.";
    
    /// <summary>
    /// 需要解包的泛型类型
    /// </summary>
    public HashSet<string> UnwrapGenericTypes { get; set; } = new();
    
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
