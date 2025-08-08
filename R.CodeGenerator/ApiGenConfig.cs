namespace R.CodeGenerator;

public class ApiGenConfig
{
    public string OutputDir { get; set; } = "./api";
    public string TypesDir { get; set; } = "./types";
    public string[] ImportLine { get; set; } = new[] { "import { request as requestHttp } from '../request';" };
    public string ApiPrefix { get; set; } = "";
    public bool UseInterface { get; set; } = true;
    public string NamespacePrefix { get; set; }
    public string[] UnwrapGenericTypes { get; set; }
}