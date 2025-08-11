namespace R.CodeGenerator;

/// <summary>
/// 注释清理器配置
/// </summary>
public class CommentSanitizerConfig
{
    /// <summary>
    /// 最大注释长度
    /// </summary>
    public int MaxLength { get; set; } = 200;
    
    /// <summary>
    /// 最大行数
    /// </summary>
    public int MaxLines { get; set; } = 5;
    
    /// <summary>
    /// 是否保留HTML标签
    /// </summary>
    public bool PreserveHtmlTags { get; set; } = true;
    
    /// <summary>
    /// 是否保留换行结构
    /// </summary>
    public bool PreserveLineBreaks { get; set; } = true;
}

/// <summary>
/// 专门用于处理注释内容的清理器
/// </summary>
public static class CommentSanitizer
{
    private static readonly CommentSanitizerConfig DefaultConfig = new();
    
    /// <summary>
    /// 清理单行注释
    /// </summary>
    public static string SanitizeSingleLine(string? comment, CommentSanitizerConfig? config = null)
    {
        config ??= DefaultConfig;
        
        if (string.IsNullOrWhiteSpace(comment))
            return string.Empty;

        comment = comment.Trim();
        
        // 基础安全清理
        comment = EscapeJSDocCharacters(comment);
        comment = NormalizeWhitespace(comment);
        
        if (!config.PreserveHtmlTags)
            comment = CleanHtmlEntities(comment);
        
        // 长度限制
        if (comment.Length > config.MaxLength)
            comment = comment.Substring(0, config.MaxLength - 3) + "...";
            
        return comment;
    }
    
    /// <summary>
    /// 清理多行注释，返回适合JSDoc的格式
    /// </summary>
    public static List<string> SanitizeMultiLine(string? comment, CommentSanitizerConfig? config = null)
    {
        config ??= DefaultConfig;
        
        if (string.IsNullOrWhiteSpace(comment))
            return new List<string>();

        var lines = comment.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                          .Select(line => line.Trim())
                          .Where(line => !string.IsNullOrEmpty(line))
                          .Select(line => EscapeJSDocCharacters(line))
                          .Select(line => CleanHtmlEntities(line))
                          .Take(config.MaxLines)
                          .ToList();
                          
        // 如果超过最大行数，添加省略号
        if (comment.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).Length > config.MaxLines)
        {
            lines.Add("...");
        }
        
        return lines;
    }
    
    private static string EscapeJSDocCharacters(string text)
    {
        return text.Replace("*/", "*​/")  // 零宽度空格
                  .Replace("/*", "/​*");
    }
    
    private static string NormalizeWhitespace(string text)
    {
        text = text.Replace("\r\n", " ")
                  .Replace("\n", " ")
                  .Replace("\r", " ")
                  .Replace("\t", " ");
        
        while (text.Contains("  "))
            text = text.Replace("  ", " ");
            
        return text.Trim();
    }
    
    private static string CleanHtmlEntities(string text)
    {
        return text.Replace("&lt;", "<")
                  .Replace("&gt;", ">")
                  .Replace("&amp;", "&")
                  .Replace("&quot;", "\"")
                  .Replace("&apos;", "'");
    }
}
