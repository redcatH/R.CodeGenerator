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

    /// <summary>
    /// 判断 tsType 是否为 TypeScript 基础类型
    /// </summary>
    private static bool IsTsBasicType(string tsType)
    {
        return tsType == "string" || tsType == "number" || tsType == "boolean" || tsType == "any";
    }

    public void GenerateTypes(Dictionary<string, TypeDescriptionDto> types, string typesDir, bool useInterface,
        string namespacePrefix)
    {
        Directory.CreateDirectory(typesDir);
        var typeNames = new List<string>();
        var generatedTypes = new HashSet<string>();
        var pendingTypes = new Dictionary<string, TypeDescriptionDto>(types);
        // 拓扑排序，优先生成无依赖类型
        bool progress;
        do
        {
            progress = false;
            var toRemove = new List<string>();
            foreach (var kv in pendingTypes)
            {
                var type = kv.Value;
                if (type.Namespace == null || !type.Namespace.StartsWith(namespacePrefix))
                    continue;
                // 如果有基类且基类未生成，跳过
                if (!string.IsNullOrEmpty(type.BaseType) && types.ContainsKey(type.BaseType) && !generatedTypes.Contains(types[type.BaseType].Name))
                    continue;

                var lines = new List<string>();
                // 需要 import 的类型
                var importSet = new HashSet<string>();
                string genericParams = type.GenericArguments != null && type.GenericArguments.Count > 0
                    ? $"<{string.Join(", ", type.GenericArguments.Select((g, i) => $"T{i}"))}>"
                    : "";
                string baseClause = "";
                if (!string.IsNullOrEmpty(type.BaseType) && types.ContainsKey(type.BaseType))
                {
                    var baseType = types[type.BaseType];
                    var baseGenericParams = baseType.GenericArguments != null && baseType.GenericArguments.Count > 0
                        ? $"<{string.Join(", ", baseType.GenericArguments.Select((g, i) => $"T{i}"))}>"
                        : "";
                    baseClause = $" extends {baseType.Name}{baseGenericParams}";
                    // 只要不是全局类型就 import
                    if (baseType.Name != "object" && baseType.Name != "any" && baseType.Name != type.Name)
                        importSet.Add(baseType.Name);
                }
                lines.Add($"{(useInterface ? "export interface" : "export type")} {type.Name}{genericParams}{baseClause} {{");
                foreach (var prop in type.Properties)
                {
                    string tsType = null;
                    int idx = -1;
                    if (type.GenericArguments != null && (idx = type.GenericArguments.IndexOf(prop.Type)) >= 0)
                    {
                        tsType = $"T{idx}";
                    }
                    else
                    {
                        tsType = MapCSharpTypeToTs(prop.Type);
                        // 需要 import 的属性类型（非基础类型、非泛型参数、非自身）
                        if (!IsTsBasicType(tsType) && types.Values.Any(t => t.Name == tsType) && tsType != type.Name)
                            importSet.Add(tsType);
                    }
                    var optional = prop.IsNullable || !prop.IsRequired ? "?" : "";
                    lines.Add($"  {prop.Name}{optional}: {tsType};");
                }
                lines.Add("}");
                // 先写 import 语句
                if (importSet.Count > 0)
                {
                    var importLines = importSet.Select(n => $"import {{ {n} }} from './{n}';");
                    lines.InsertRange(0, importLines);
                }
                var filePath = Path.Combine(typesDir, $"{type.Name}.ts");
                File.WriteAllText(filePath, string.Join("\n", lines));
                Console.WriteLine($"[Type] Generated: {filePath}");
                if (!string.IsNullOrEmpty(type.Name))
                    typeNames.Add(type.Name);
                generatedTypes.Add(type.Name);
                toRemove.Add(kv.Key);
                progress = true;
            }
            foreach (var key in toRemove)
                pendingTypes.Remove(key);
        } while (pendingTypes.Count > 0 && progress);

        // 生成 index.ts，统一导出所有类型
        if (typeNames.Count > 0)
        {
            var indexLines = typeNames.Select(n => $"export * from './{n}';");
            var indexPath = Path.Combine(typesDir, "index.ts");
            File.WriteAllText(indexPath, string.Join("\n", indexLines));
            Console.WriteLine($"[Type] Generated: {indexPath}");
        }
    }

    public void GenerateApis(List<ApiDescriptionDto> apis, string outputDir, string[] importLines,
        Dictionary<string, TypeDescriptionDto> types, string[] configUnwrapGenericTypes)
    {
        Directory.CreateDirectory(outputDir);
        var groups = apis.GroupBy(a => a.Controller);
        var templateText = File.ReadAllText("templates/api_service.sbn");
        var template = Template.Parse(templateText);

        foreach (var group in groups)
        {
            var serviceName = group.Key + "Service";
            var apiList = group.Select(api => new
            {
                action_name = GenerateFriendlyActionName(group.Key, api.Action, api.HttpMethod),
                param_list = string.Join(", ", api.Parameters.Select(p =>
                {
                    var tsType = MapCSharpTypeToTs(p.Type);
                    if (!IsTsBasicType(tsType))
                        tsType = $"types.{tsType}";
                    return $"{p.Name}{(p.IsOptional ? "?" : "")}: {tsType}";
                })),
                return_type = api.ReturnType != null && !string.IsNullOrEmpty(api.ReturnType.Type)
                    ? GetReturnType(types, api,configUnwrapGenericTypes.ToHashSet())
                    : "any",
                path = api.Path,
                http_method = api.HttpMethod ?? "get",
                data_line = api.HttpMethod?.ToUpper() == "GET"
                    ? $"params: {{ {string.Join(", ", api.Parameters.Select(p => p.Name))} }}"
                    : $"data: {{ {string.Join(", ", api.Parameters.Select(p => p.Name))} }}"
            }).ToList();

            var renderObj = new
            {
                import_lines = new List<string> { "import * as types from '../types';" }
                    .Concat(importLines ?? Array.Empty<string>()).ToList(),
                service_name = serviceName,
                apis = apiList
            };

            var result = template.Render(renderObj, member => member.Name);

            var filePath = Path.Combine(outputDir, $"{serviceName}.ts");
            File.WriteAllText(filePath, result);
            Console.WriteLine($"[API] Generated: {filePath}");
        }
    }
    private static string GetReturnType(Dictionary<string, TypeDescriptionDto> types, ApiDescriptionDto api,HashSet<string> unwrapGenericTypes)
    {
        string returnType;
        if (types.TryGetValue(api.ReturnType.Type, out var retTypeDesc))
        {
            // 判断是否需要解包
            if (unwrapGenericTypes.Contains(retTypeDesc.Name)
                && retTypeDesc.GenericArguments != null
                && retTypeDesc.GenericArguments.Count == 1)
            {
                var tsType = MapCSharpTypeToTs(retTypeDesc.GenericArguments[0]);
                if (!IsTsBasicType(tsType) && !tsType.StartsWith("types."))
                    tsType = $"types.{tsType}";
                return tsType;
            }
            
            returnType = retTypeDesc.Name;
            if (retTypeDesc.GenericArguments != null && retTypeDesc.GenericArguments.Count > 0)
            {
                // 泛型参数类型加 types. 前缀（如有必要）
                var genArgs = retTypeDesc.GenericArguments.Select(arg =>
                {
                    var tsType = MapCSharpTypeToTs(arg);
                    if (!IsTsBasicType(tsType) && !tsType.StartsWith("types."))
                        tsType = $"types.{tsType}";
                    return tsType;
                });
                returnType += $"<{string.Join(", ", genArgs)}>";
            }
            
            returnType = $"types.{returnType}";
        }
        else
        {
            returnType = MapCSharpTypeToTs(api.ReturnType.Type);
            if (!IsTsBasicType(returnType) && !returnType.StartsWith("types."))
                returnType = $"types.{returnType}";
        }

        return returnType;
    }

    /// <summary>
    /// 根据 Controller 名和 Action 名生成友好的 API 函数名（小驼峰、去冗余、合并动作等）
    /// </summary>
    public static string GenerateFriendlyActionName(string? controller, string? action, string? httpMethod)
    {
        // controller 为空直接返回 action
        if (string.IsNullOrEmpty(action))
        {
            // fallback: 用 http 动作
            switch ((httpMethod ?? "get").ToLower())
            {
                case "get": return "get";
                case "post": return "create";
                case "put": return "update";
                case "delete": return "delete";
                default: return "do";
            }
        }

        string ctrl = controller ?? string.Empty;
        if (ctrl.EndsWith("Controller"))
            ctrl = ctrl.Substring(0, ctrl.Length - "Controller".Length);
        string act = action;
        if (act.EndsWith("Async"))
            act = act.Substring(0, act.Length - "Async".Length);
        if (act.EndsWith("Action"))
            act = act.Substring(0, act.Length - "Action".Length);
        if (!string.IsNullOrEmpty(ctrl) && act.StartsWith(ctrl))
            act = act.Substring(ctrl.Length);
        if (string.IsNullOrEmpty(act))
        {
            switch ((httpMethod ?? "get").ToLower())
            {
                case "get": act = "get"; break;
                case "post": act = "create"; break;
                case "put": act = "update"; break;
                case "delete": act = "delete"; break;
                default: act = "do"; break;
            }
        }

        // 首字母小写
        if (!string.IsNullOrEmpty(act) && char.IsUpper(act[0]))
            act = char.ToLower(act[0]) + act.Substring(1);
        return act;
    }
}