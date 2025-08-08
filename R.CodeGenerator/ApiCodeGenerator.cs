using R.DescriptionModelGenerator;
using Scriban;

namespace R.CodeGenerator;

public class ApiCodeGenerator
{
    public static string MapCSharpTypeToTs(string? csharpType)
    {
        if (string.IsNullOrEmpty(csharpType)) return "any";
        if (csharpType.StartsWith("System.String")) return "string";
        if (csharpType.StartsWith("System.Int") || csharpType.StartsWith("System.Double") ||
            csharpType.StartsWith("System.Decimal")) return "number";
        if (csharpType.StartsWith("System.Boolean")) return "boolean";
        if (csharpType.StartsWith("System.Collections.Generic.List") || csharpType.EndsWith("[]"))
        {
            var inner = csharpType.Contains("<") ? csharpType.Split('<', '>')[1] : "any";
            return $"{MapCSharpTypeToTs(inner)}[]";
        }

        // 泛型 Task<T>
        if (csharpType.StartsWith("System.Threading.Tasks.Task"))
        {
            var inner = csharpType.Contains("<") ? csharpType.Split('<', '>')[1] : "any";
            return MapCSharpTypeToTs(inner);
        }

        // 直接映射常见类型
        if (csharpType.StartsWith("Microsoft.AspNetCore.Mvc.IActionResult") ||
            csharpType.StartsWith("Microsoft.AspNetCore.Mvc.ActionResult")) return "any";
        if (csharpType.StartsWith("Microsoft.AspNetCore.Mvc.FileResult")) return "any";
        if (csharpType.StartsWith("System.DateTime") || csharpType.StartsWith("System.DateTimeOffset")) return "string";
        if (csharpType.StartsWith("System.Guid")) return "string";
        // 只为 VividCMS. 命名空间下的类型生成 TS 类型，否则 any
        if (csharpType.StartsWith("VividCMS."))
        {
            // 复杂类型名处理
            return csharpType.Split('.').Last().Replace("+", ".");
        }

        return "any";
    }

    public void GenerateTypes(Dictionary<string, TypeDescriptionDto> types, string typesDir, bool useInterface, string namespacePrefix)
    {
        Directory.CreateDirectory(typesDir);
        var typeNames = new List<string>();
        foreach (var kv in types)
        {
            var type = kv.Value;
            if (type.Namespace == null || !type.Namespace.StartsWith(namespacePrefix))
                continue;
            var lines = new List<string>();
            // 泛型参数声明
            string genericParams = type.GenericArguments != null && type.GenericArguments.Count > 0
                ? $"<{string.Join(", ", type.GenericArguments.Select((g, i) => $"T{i}"))}>"
                : "";
            // extends基类
            string baseClause = "";
            if (!string.IsNullOrEmpty(type.BaseType) && types.ContainsKey(type.BaseType))
            {
                var baseType = types[type.BaseType];
                var baseGenericParams = baseType.GenericArguments != null && baseType.GenericArguments.Count > 0
                    ? $"<{string.Join(", ", baseType.GenericArguments.Select((g, i) => $"T{i}"))}>"
                    : "";
                baseClause = $" extends {baseType.Name}{baseGenericParams}";
            }
            lines.Add($"{(useInterface ? "export interface" : "export type")} {type.Name}{genericParams}{baseClause} {{");
            foreach (var prop in type.Properties)
            {
                // 泛型参数属性直接用T0/T1，否则用MapCSharpTypeToTs
                string tsType = null;
                int idx = -1;
                if (type.GenericArguments != null && (idx = type.GenericArguments.IndexOf(prop.Type)) >= 0)
                {
                    tsType = $"T{idx}";
                }
                else
                {
                    tsType = MapCSharpTypeToTs(prop.Type);
                }
                var optional = prop.IsNullable || !prop.IsRequired ? "?" : "";
                lines.Add($"  {prop.Name}{optional}: {tsType};");
            }
            lines.Add("}");
            var filePath = Path.Combine(typesDir, $"{type.Name}.ts");
            File.WriteAllText(filePath, string.Join("\n", lines));
            Console.WriteLine($"[Type] Generated: {filePath}");
            if (!string.IsNullOrEmpty(type.Name))
                typeNames.Add(type.Name);
        }

        // 生成 index.ts，统一导出所有类型
        if (typeNames.Count > 0)
        {
            var indexLines = typeNames.Select(n => $"export * from './{n}';");
            var indexPath = Path.Combine(typesDir, "index.ts");
            File.WriteAllText(indexPath, string.Join("\n", indexLines));
            Console.WriteLine($"[Type] Generated: {indexPath}");
        }
    }

    public void GenerateApis(List<ApiDescriptionDto> apis, string outputDir, string[] importLines, string typesDir, Dictionary<string, TypeDescriptionDto> types)
    {
        Directory.CreateDirectory(outputDir);
        var groups = apis.GroupBy(a => a.Controller);
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "ApiFile.sbn");
        string templateText = File.Exists(templatePath)
            ? File.ReadAllText(templatePath)
            : @"{{~ for line in import_lines ~}}
{{ line }}
{{~ end ~}}
import * as types from '../types';

{{~ for api in apis ~}}
/** {{ api.action }} */
export function fetch{{ api.action }}({{ api.param_list }}) {
  return request<{{ api.return_type }}>({
    url: '{{ api.path }}',
    method: '{{ api.http_method | string.downcase }}',
    {{ api.data_line }}
  });
}

{{~ end ~}}";
        foreach (var group in groups)
        {
            var apisForTpl = new List<object>();
            foreach (var api in group)
            {
                var paramList = string.Join(", ", api.Parameters.Select(p =>
                {
                    var tsType = MapCSharpTypeToTs(p.Type);
                    if (tsType != "string" && tsType != "number" && tsType != "boolean" && tsType != "any")
                        tsType = $"types.{tsType}";
                    return $"{p.Name}{(p.IsOptional ? "?" : "")}: {tsType}";
                }));
                string dataLine = "";
                if (api.HttpMethod?.ToUpper() == "GET")
                    dataLine = $"params: {{ {string.Join(", ", api.Parameters.Select(p => p.Name))} }}";
                else
                    dataLine = $"data: {{ {string.Join(", ", api.Parameters.Select(p => p.Name))} }}";
                // 处理返回类型泛型
                string returnType = "any";
                if (api.ReturnType != null && !string.IsNullOrEmpty(api.ReturnType.Type))
                {
                    // 查找类型描述
                    if (types.TryGetValue(api.ReturnType.Type, out var retTypeDesc))
                    {
                        returnType = retTypeDesc.Name;
                        if (retTypeDesc.GenericArguments != null && retTypeDesc.GenericArguments.Count > 0)
                        {
                            var genArgs = retTypeDesc.GenericArguments.Select(MapCSharpTypeToTs);
                            returnType += $"<{string.Join(", ", genArgs)}>";
                        }
                        returnType = $"types.{returnType}";
                    }
                    else
                    {
                        returnType = MapCSharpTypeToTs(api.ReturnType.Type);
                        if (returnType != "string" && returnType != "number" && returnType != "boolean" && returnType != "any")
                            returnType = $"types.{returnType}";
                    }
                }
                apisForTpl.Add(new
                {
                    action = api.Action,
                    param_list = paramList,
                    return_type = returnType,
                    path = api.Path,
                    http_method = api.HttpMethod ?? "get",
                    data_line = api.Parameters.Count > 0 ? dataLine : ""
                });
            }

            var scribanTemplate = Template.Parse(templateText);
            var result = scribanTemplate.Render(new
            {
                import_lines = importLines,
                apis = apisForTpl
            });
            var filePath = Path.Combine(outputDir, $"{group.Key}.ts");
            File.WriteAllText(filePath, result);
            Console.WriteLine($"[API] Generated: {filePath}");
        }
    }
}