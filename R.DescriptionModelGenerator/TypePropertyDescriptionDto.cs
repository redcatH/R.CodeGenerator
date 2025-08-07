namespace R.DescriptionModelGenerator;

public class TypePropertyDescriptionDto
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool IsNullable { get; set; }
    public bool IsRequired { get; set; }
}