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
}
