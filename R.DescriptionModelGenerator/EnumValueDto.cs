namespace R.DescriptionModelGenerator;

public class EnumValueDto
{
    /// <summary>
    /// 枚举成员名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 枚举成员对应的数值（以字符串形式以避免精度/类型差异）
    /// </summary>
    public string? Value { get; set; }
}
