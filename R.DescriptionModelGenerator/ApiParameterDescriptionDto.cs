namespace R.DescriptionModelGenerator;

public class ApiParameterDescriptionDto
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Source { get; set; }
    public bool IsOptional { get; set; }
    public object? DefaultValue { get; set; }
    /// <summary>
    /// 参数的XML注释
    /// </summary>
    public string? Summary { get; set; }
}