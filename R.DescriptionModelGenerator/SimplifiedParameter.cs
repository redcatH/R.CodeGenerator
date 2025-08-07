namespace R.DescriptionModelGenerator;
// 扩展 SimplifiedParameter 类
public class SimplifiedParameter
{
    public string Name { get; set; }
    public string MethodParamName { get; set; } // 新增：实际方法参数名
    public string Type { get; set; }
    public string Source { get; set; }
    public bool IsOptional { get; set; } // 新增：是否可选
    public string DefaultValue { get; set; } // 新增：默认值
    public string[] Constraints { get; set; } // 新增：约束条件
    public string ContainerName { get; set; } // 新增：容器名称
    public bool IsNullable { get; set; }
}