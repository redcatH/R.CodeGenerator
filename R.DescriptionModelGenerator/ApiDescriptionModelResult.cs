namespace R.DescriptionModelGenerator;

public class ApiDescriptionModelResult
{
    public List<ApiDescriptionDto> Apis { get; set; } = new();
    public Dictionary<string, TypeDescriptionDto> Types { get; set; } = new();
}