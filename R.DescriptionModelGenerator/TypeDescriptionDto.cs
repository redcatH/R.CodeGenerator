namespace R.DescriptionModelGenerator;

public class TypeDescriptionDto
{
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public List<TypePropertyDescriptionDto> Properties { get; set; } = new();
}