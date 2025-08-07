namespace R.DescriptionModelGenerator;

public class PropertyInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsOptional { get; set; }      // 新增
    public bool IsNullable { get; set; }      // 新增
}