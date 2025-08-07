namespace R.DescriptionModelGenerator;

public class ActionInfo
{
    public string Name { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public List<SimplifiedParameter> Parameters { get; set; }
    public string ReturnType { get; set; }
}