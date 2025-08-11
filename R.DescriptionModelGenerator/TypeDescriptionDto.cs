namespace R.DescriptionModelGenerator;

public class TypeDescriptionDto
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public string? BaseType { get; set; } // 基类全名
    public List<string> GenericArguments { get; set; } = new(); // 泛型参数类型全名
    public List<TypePropertyDescriptionDto> Properties { get; set; } = new();
    /// <summary>
    /// 类型的XML注释
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// 类型的详细说明
    /// </summary>
    public string? Remarks { get; set; }
}