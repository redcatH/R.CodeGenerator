namespace R.DescriptionModelGenerator;

public class ApiDescriptionDto
{
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public List<ApiParameterDescriptionDto> Parameters { get; set; } = new();
    public ReturnTypeModel? ReturnType { get; set; }
    /// <summary>
    /// Action方法的XML注释
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// Action方法的详细说明
    /// </summary>
    public string? Remarks { get; set; }
}