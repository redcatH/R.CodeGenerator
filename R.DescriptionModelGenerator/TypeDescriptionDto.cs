namespace R.DescriptionModelGenerator;

public class TypeDescriptionDto
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public string? BaseType { get; set; } // 基类全名
    public List<TypePropertyDescriptionDto> Properties { get; set; } = new();
}