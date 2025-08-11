namespace R.DescriptionModelGenerator;

public class TypePropertyDescriptionDto
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool IsNullable { get; set; }
    public bool IsRequired { get; set; }
    /// <summary>
    /// 属性的XML注释
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// 属性的详细说明
    /// </summary>
    public string? Remarks { get; set; }
}