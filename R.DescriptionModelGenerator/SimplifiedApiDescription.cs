namespace R.DescriptionModelGenerator;

public class SimplifiedApiDescription
{
    public string ControllerName { get; set; }
    public string HttpMethod { get; set; }
    public string RelativePath { get; set; }
    public string ActionName { get; set; }
    public string ResponseType { get; set; }
    public List<SimplifiedParameter> Parameters { get; set; }
}