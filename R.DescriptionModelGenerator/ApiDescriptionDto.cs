namespace R.DescriptionModelGenerator;

public class ApiDescriptionDto
{
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public List<ApiParameterDescriptionDto> Parameters { get; set; } = new();
    public ReturnTypeModel? ReturnType { get; set; }
}