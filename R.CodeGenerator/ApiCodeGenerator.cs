using System.Diagnostics;
using System.Text.RegularExpressions;
using R.DescriptionModelGenerator;
using Scriban;

namespace R.CodeGenerator;

public class ApiCodeGenerator
{
    private readonly ApiGeneratorConfig _config;

    public ApiCodeGenerator(ApiGeneratorConfig? config = null)
    {
        _config = config ?? new ApiGeneratorConfig();
    }
    
    /// <summary>
    /// 从 ApiGenConfig 创建 ApiCodeGenerator
    /// </summary>
    /// <param name="genConfig">外部配置</param>
    public ApiCodeGenerator(ApiGenConfig genConfig)
    {
        _config = new ApiGeneratorConfig
        {
            TemplatePath = genConfig.TemplatePath,
            EnablePostGenerationCommand = genConfig.EnablePostGenerationCommand,
            CommandWorkingDirectory = genConfig.CommandWorkingDirectory,
            Command = genConfig.Command,
            CommandArguments = genConfig.CommandArguments,
            CommandTimeoutMs = genConfig.CommandTimeoutMs,
            WaitForCommandCompletion = genConfig.WaitForCommandCompletion,
            UnwrapGenericTypes = genConfig.UnwrapGenericTypes?.ToHashSet() ?? new HashSet<string>()
        };
    }

    public static string MapCSharpTypeToTs(string? csharpType)
    {
        if (string.IsNullOrEmpty(csharpType)) return "any";

        // 如果已经是 TypeScript 类型语法，直接返回
        if (csharpType.StartsWith("types.") || csharpType.Contains(">>"))
        {
            return csharpType;
        }

        // 使用正则表达式解析泛型类型
        var typeInfo = ParseGenericType(csharpType);

        // 映射基础类型
        var mappedType = MapBasicType(typeInfo.BaseType);

        // 如果有泛型参数，递归处理
        if (typeInfo.GenericArguments.Count > 0)
        {
            var genericArgs = typeInfo.GenericArguments.Select(MapCSharpTypeToTs).ToArray();

            // 特殊处理可空类型 Nullable<T>
            if (IsNullableType(typeInfo.BaseType))
            {
                // 可空类型在 TypeScript 中使用联合类型，按约定 null 在前
                return $"null | {genericArgs[0]}";
            }

            // 特殊处理列表类型
            if (IsListType(typeInfo.BaseType))
            {
                return $"{genericArgs[0]}[]";
            }

            return $"{mappedType}<{string.Join(", ", genericArgs)}>";
        }

        // 特殊处理无参数的 Task
        if (IsTaskType(typeInfo.BaseType))
        {
            return "Promise<void>";
        }

        return mappedType;
    }

    /// <summary>
    /// 解析泛型类型信息
    /// </summary>
    private static TypeParseResult ParseGenericType(string typeString)
    {
        // 正则表达式匹配泛型类型: BaseType<Arg1, Arg2, ...>
        var genericPattern = @"^([^<]+)(?:<(.+)>)?$";
        var match = Regex.Match(typeString.Trim(), genericPattern);

        if (!match.Success)
        {
            return new TypeParseResult { BaseType = typeString.Trim(), GenericArguments = new List<string>() };
        }

        var baseType = match.Groups[1].Value.Trim();
        var genericPart = match.Groups[2].Value;

        var genericArgs = new List<string>();
        if (!string.IsNullOrEmpty(genericPart))
        {
            // 解析泛型参数，考虑嵌套泛型
            genericArgs = ParseGenericArguments(genericPart);
        }

        return new TypeParseResult { BaseType = baseType, GenericArguments = genericArgs };
    }

    /// <summary>
    /// 解析泛型参数列表，处理嵌套泛型
    /// </summary>
    private static List<string> ParseGenericArguments(string argsString)
    {
        var args = new List<string>();
        var current = "";
        var depth = 0;

        for (int i = 0; i < argsString.Length; i++)
        {
            var ch = argsString[i];

            if (ch == '<')
            {
                depth++;
                current += ch;
            }
            else if (ch == '>')
            {
                depth--;
                current += ch;
            }
            else if (ch == ',' && depth == 0)
            {
                // 只有在顶层才分割参数
                if (!string.IsNullOrWhiteSpace(current))
                {
                    args.Add(current.Trim());
                }

                current = "";
            }
            else
            {
                current += ch;
            }
        }

        // 添加最后一个参数
        if (!string.IsNullOrWhiteSpace(current))
        {
            args.Add(current.Trim());
        }

        return args;
    }

    /// <summary>
    /// 映射基础类型
    /// </summary>
    private static string MapBasicType(string baseType)
    {
        // System 类型映射
        var systemTypeMappings = new Dictionary<string, string>
        {
            { "System.String", "string" },
            { "System.Int32", "number" },
            { "System.Int64", "number" },
            { "System.Int16", "number" },
            { "System.Double", "number" },
            { "System.Single", "number" },
            { "System.Decimal", "number" },
            { "System.Boolean", "boolean" },
            { "System.Object", "any" },
            { "System.DateTime", "string" },
            { "System.DateTimeOffset", "string" },
            { "System.Guid", "string" },
            { "System.Byte[]", "File" }, // 字节数组映射为 File
            { "byte[]", "File" }, // 简化的字节数组映射
            // 可空类型的基础映射（会被泛型处理覆盖）
            { "System.Nullable", "any" },
        };

        // 精确匹配
        if (systemTypeMappings.TryGetValue(baseType, out var mapped))
        {
            return mapped;
        }

        // 前缀匹配（为了兼容性）
        foreach (var kvp in systemTypeMappings)
        {
            if (baseType.StartsWith(kvp.Key))
            {
                return kvp.Value;
            }
        }

        // 特殊类型处理
        if (IsListType(baseType))
        {
            return "any[]"; // 无泛型参数的列表返回 any[]
        }

        if (IsTaskType(baseType))
        {
            return "Promise"; // Task<T> 映射为 Promise<T>，无参数的 Task 映射为 Promise<void>
        }

        // ASP.NET Core 类型
        if (baseType.StartsWith("Microsoft.AspNetCore.Mvc."))
        {
            return "any";
        }

        // ASP.NET Core Http 类型 - 文件上传
        if (baseType == "Microsoft.AspNetCore.Http.IFormFile")
        {
            return "File";
        }

        if (baseType.StartsWith("Microsoft.AspNetCore.Http."))
        {
            return "any";
        }

        // VividCMS 类型处理
        if (baseType.StartsWith($"{_namespacePrefix}"))
        {
            return ExtractVividCmsTypeName(baseType);
        }

        // 默认返回 any
        return "any";
    }

    /// <summary>
    /// 检查是否是可空类型
    /// </summary>
    private static bool IsNullableType(string type)
    {
        return type.StartsWith("System.Nullable");
    }

    /// <summary>
    /// 检查是否是列表类型
    /// </summary>
    private static bool IsListType(string type)
    {
        return type.StartsWith("System.Collections.Generic.List") ||
               type.EndsWith("[]") ||
               type.StartsWith("System.Collections.Generic.IList") ||
               type.StartsWith("System.Collections.Generic.IEnumerable");
    }

    /// <summary>
    /// 检查是否是 Task 类型
    /// </summary>
    private static bool IsTaskType(string type)
    {
        return type.StartsWith("System.Threading.Tasks.Task");
    }

    /// <summary>
    /// 检查是否是文件类型
    /// </summary>
    private static bool IsFileType(string type)
    {
        return type == "Microsoft.AspNetCore.Http.IFormFile" ||
               type.StartsWith("Microsoft.AspNetCore.Http.IFormFile") ||
               type == "System.IO.Stream" ||
               type == "System.Byte[]" ||
               type == "byte[]";
    }

    /// <summary>
    /// 提取 VividCMS 类型名
    /// </summary>
    private static string ExtractVividCmsTypeName(string fullTypeName)
    {
        var escapedPrefix = Regex.Escape(_namespacePrefix);
        // 使用正则表达式提取最后一个命名空间部分
        var pattern = @$"{escapedPrefix}(?:[^.]+\.)*([^.+]+(?:\+.+)?)$";
        var match = Regex.Match(fullTypeName, pattern);

        if (match.Success)
        {
            var typeName = match.Groups[1].Value;
            // 处理嵌套类型的 + 符号
            return typeName.Replace("+", ".");
        }

        // fallback: 使用原来的方法
        return fullTypeName.Split('.').Last().Replace("+", ".");
    }

    /// <summary>
    /// 类型解析结果
    /// </summary>
    private class TypeParseResult
    {
        public string BaseType { get; set; } = "";
        public List<string> GenericArguments { get; set; } = new List<string>();
    }

    /// <summary>
    /// 为 TypeScript 类型添加 types. 前缀（智能处理数组、联合类型等）
    /// </summary>
    private static string AddTypesPrefix(string tsType, Dictionary<string, TypeDescriptionDto> types)
    {
        // 基础类型不需要前缀
        if (IsTsBasicType(tsType))
            return tsType;
            
        // 已经有前缀的不重复添加
        if (tsType.StartsWith("types."))
            return tsType;
            
        // 处理数组类型 (例如: Device[] -> types.Device[])
        if (tsType.EndsWith("[]"))
        {
            var elementType = tsType.Substring(0, tsType.Length - 2);
            var prefixedElementType = AddTypesPrefix(elementType, types);
            return $"{prefixedElementType}[]";
        }
        
        // 处理联合类型 (例如: null | Device -> null | types.Device)
        if (tsType.Contains(" | "))
        {
            var parts = tsType.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
            var prefixedParts = parts.Select(part => AddTypesPrefix(part.Trim(), types));
            return string.Join(" | ", prefixedParts);
        }
        
        // 处理泛型类型 (例如: List<Device> -> types.List<types.Device>)
        var genericMatch = Regex.Match(tsType, @"^([^<]+)<(.+)>$");
        if (genericMatch.Success)
        {
            var baseType = genericMatch.Groups[1].Value.Trim();
            var genericArgs = genericMatch.Groups[2].Value;
            
            var prefixedBaseType = types.Values.Any(t => t.Name == baseType) ? $"types.{baseType}" : baseType;
            
            // 递归处理泛型参数
            var args = ParseGenericArguments(genericArgs);
            var prefixedArgs = args.Select(arg => AddTypesPrefix(arg, types));
            
            return $"{prefixedBaseType}<{string.Join(", ", prefixedArgs)}>";
        }
        
        // 简单类型名
        if (types.Values.Any(t => t.Name == tsType))
        {
            return $"types.{tsType}";
        }
        
        // 不需要前缀的类型
        return tsType;
    }

    /// <summary>
    /// 从 TypeScript 类型字符串中提取需要 import 的类型名
    /// </summary>
    private static HashSet<string> ExtractImportableTypeNames(string tsType, Dictionary<string, TypeDescriptionDto> types)
    {
        var result = new HashSet<string>();
        
        // 跳过基础类型
        if (IsTsBasicType(tsType))
            return result;
            
        // 处理数组类型 (例如: Device[] -> Device)
        if (tsType.EndsWith("[]"))
        {
            var elementType = tsType.Substring(0, tsType.Length - 2);
            result.UnionWith(ExtractImportableTypeNames(elementType, types));
            return result;
        }
        
        // 处理联合类型 (例如: null | Device -> Device)
        if (tsType.Contains(" | "))
        {
            var parts = tsType.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                result.UnionWith(ExtractImportableTypeNames(part.Trim(), types));
            }
            return result;
        }
        
        // 处理泛型类型 (例如: List<Device> -> Device)
        var genericMatch = Regex.Match(tsType, @"^([^<]+)<(.+)>$");
        if (genericMatch.Success)
        {
            var baseType = genericMatch.Groups[1].Value.Trim();
            var genericArgs = genericMatch.Groups[2].Value;
            
            // 递归处理泛型参数
            var args = ParseGenericArguments(genericArgs);
            foreach (var arg in args)
            {
                result.UnionWith(ExtractImportableTypeNames(arg, types));
            }
            
            // 检查基础类型是否需要 import
            if (types.Values.Any(t => t.Name == baseType))
            {
                result.Add(baseType);
            }
            
            return result;
        }
        
        // 简单类型名检查
        if (types.Values.Any(t => t.Name == tsType))
        {
            result.Add(tsType);
        }
        
        return result;
    }

    /// <summary>
    /// 判断 tsType 是否为 TypeScript 基础类型
    /// </summary>
    private static bool IsTsBasicType(string tsType)
    {
        // 基础类型
        if (tsType == "string" || tsType == "number" || tsType == "boolean" || tsType == "any")
            return true;
            
        // 联合类型（如 null | number 或 number | null）
        if (tsType.Contains(" | "))
        {
            var parts = tsType.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
            return parts.All(part => part == "null" || part == "undefined" || 
                           part == "string" || part == "number" || part == "boolean" || part == "any");
        }
        
        return false;
    }

    /// <summary>
    /// 清理和转义注释内容，确保不会破坏 JSDoc 语法
    /// </summary>
    private string SanitizeComment(string? comment)
    {
        return CommentSanitizer.SanitizeSingleLine(comment, _config.CommentConfig);
    }

    /// <summary>
    /// 生成多行JSDoc注释
    /// </summary>
    private List<string> GenerateJSDocComment(string? summary, string? remarks)
    {
        var lines = new List<string>();

        if (!_config.GenerateDetailedComments ||
            (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(remarks)))
            return lines;

        lines.Add("/**");

        // 处理 summary
        if (!string.IsNullOrWhiteSpace(summary))
        {
            var summaryLines = CommentSanitizer.SanitizeMultiLine(summary, _config.CommentConfig);
            foreach (var line in summaryLines)
            {
                lines.Add($" * {line}");
            }
        }

        // 处理 remarks
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add(" *");

            var remarksLines = CommentSanitizer.SanitizeMultiLine(remarks, _config.CommentConfig);
            foreach (var line in remarksLines)
            {
                lines.Add($" * {line}");
            }
        }

        lines.Add(" */");
        return lines;
    }

    private static string _namespacePrefix;
    public void GenerateTypes(Dictionary<string, TypeDescriptionDto> types, string typesDir, bool useInterface,
        string namespacePrefix)
    {
        _namespacePrefix = namespacePrefix; 
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
                if (!string.IsNullOrEmpty(type.BaseType) && types.ContainsKey(type.BaseType))
                {
                    var baseTypeName = types[type.BaseType].Name;
                    if (!string.IsNullOrEmpty(baseTypeName) && !generatedTypes.Contains(baseTypeName))
                        continue;
                }

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
                    if (!string.IsNullOrEmpty(baseType.Name) && baseType.Name != "object" && baseType.Name != "any" &&
                        baseType.Name != type.Name)
                        importSet.Add(baseType.Name);
                }

                // 添加类型的 JSDoc 注释
                var typeCommentLines = GenerateJSDocComment(type.Summary, type.Remarks);
                lines.AddRange(typeCommentLines);

                lines.Add(
                    $"{(useInterface ? "export interface" : "export type")} {type.Name}{genericParams}{baseClause} {{");
                foreach (var prop in type.Properties)
                {
                    string? tsType = null;
                    int idx = -1;
                    if (type.GenericArguments != null && prop.Type != null &&
                        (idx = type.GenericArguments.IndexOf(prop.Type)) >= 0)
                    {
                        tsType = $"T{idx}";
                    }
                    else
                    {
                        tsType = MapCSharpTypeToTs(prop.Type);
                        // 提取需要 import 的类型名
                        var importableTypes = ExtractImportableTypeNames(tsType, types);
                        foreach (var importType in importableTypes)
                        {
                            if (importType != type.Name) // 排除自身
                            {
                                importSet.Add(importType);
                            }
                        }
                    }

                    var optional = prop.IsNullable || !prop.IsRequired ? "?" : "";

                    // 添加属性的 JSDoc 注释
                    var propCommentLines = GenerateJSDocComment(prop.Summary, prop.Remarks);
                    if (propCommentLines.Count > 0)
                    {
                        // 为属性注释添加缩进
                        var indentedLines = propCommentLines.Select(line =>
                            line == "/**" || line == " */" ? $"  {line}" : $"  {line}");
                        lines.AddRange(indentedLines);
                    }

                    lines.Add($"  {prop.Name}{optional}: {tsType};");
                }

                lines.Add("}");
                // 先写 import 语句
                if (importSet.Count > 0)
                {
                    var importLines = importSet.Select(n => $"import type {{ {n} }} from './{n}';");
                    lines.InsertRange(0, importLines);
                }

                var filePath = Path.Combine(typesDir, $"{type.Name}.ts");
                File.WriteAllText(filePath, string.Join("\n", lines));
                Console.WriteLine($"[Type] Generated: {filePath}");
                if (!string.IsNullOrEmpty(type.Name))
                {
                    typeNames.Add(type.Name);
                    generatedTypes.Add(type.Name);
                }

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
        var templateText = File.ReadAllText("templates/api_service_vben.sbn");
        var template = Template.Parse(templateText);

        foreach (var group in groups)
        {
            var serviceName = group.Key + "Service";
            var apiList = group.Select(api =>
            {
                // 构建参数列表和参数注释信息
                var paramInfos = api.Parameters.Select(p =>
                {
                    var tsType = MapCSharpTypeToTs(p.Type);
                    var typedTsType = AddTypesPrefix(tsType, types);
                    var isComplexType = IsComplexType(p.Type, types);
                    var isFileType = IsFileType(p.Type ?? "");
                    
                    return new
                    {
                        name = p.Name,
                        type = typedTsType,
                        source = p.Source, // 添加参数来源信息
                        isOptional = p.IsOptional, // 修改为正确的属性名
                        defaultValue = p.DefaultValue, // 添加默认值
                        is_complex_type = isComplexType, // 添加是否为复杂类型的判断
                        is_file_type = isFileType, // 添加是否为文件类型的判断
                        param_string = $"{p.Name}{(p.IsOptional ? "?" : "")}: {typedTsType}",
                        summary = SanitizeComment(p.Summary), // 清理参数注释
                        has_comment = !string.IsNullOrEmpty(SanitizeComment(p.Summary))
                    };
                })
                // 重要：必需参数必须在可选参数之前，按 isOptional 排序
                .OrderBy(p => p.isOptional)
                .ToList();

                var result = new
                {
                    action_name = GenerateFriendlyActionName(group.Key, api.Action, api.HttpMethod),
                    param_list = string.Join(", ", paramInfos.Select(p => p.param_string)),
                    parameters = paramInfos, // 添加参数详细信息
                    return_type = api.ReturnType != null && !string.IsNullOrEmpty(api.ReturnType.Type)
                        ? GetReturnType(types, api, configUnwrapGenericTypes.ToHashSet())
                        : "any",
                    return_comment = SanitizeComment(api.ReturnType?.Summary ?? string.Empty), // 清理返回值注释
                    path = api.Path,
                    http_method = api.HttpMethod ?? "get",
                    data_line = api.HttpMethod?.ToUpper() == "GET"
                        ? $"params: {{ {string.Join(", ", api.Parameters.Select(p => p.Name))} }}"
                        : $"data: {{ {string.Join(", ", api.Parameters.Select(p => p.Name))} }}",
                    // 添加注释信息
                    summary = SanitizeComment(api.Summary ?? string.Empty), // 清理方法注释
                    remarks = api.Remarks ?? string.Empty,
                    has_comment = !string.IsNullOrEmpty(api.Summary) || !string.IsNullOrEmpty(api.Remarks) ||
                                  paramInfos.Any(p => p.has_comment) || !string.IsNullOrEmpty(api.ReturnType?.Summary)
                };
                return result;
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

    private static string GetReturnType(Dictionary<string, TypeDescriptionDto> types, ApiDescriptionDto api,
        HashSet<string> unwrapGenericTypes)
    {
        string returnType;
        if (api.ReturnType?.Type != null && types.TryGetValue(api.ReturnType.Type, out var retTypeDesc))
        {
            // 情况1：类型存在于 types 字典中，使用详细的类型描述信息

            // 判断是否需要解包
            if (!string.IsNullOrEmpty(retTypeDesc.Name) && unwrapGenericTypes.Contains(retTypeDesc.Name)
                                                        && retTypeDesc.GenericArguments != null
                                                        && retTypeDesc.GenericArguments.Count == 1)
            {
                var tsType = MapCSharpTypeToTs(retTypeDesc.GenericArguments[0]);
                return AddTypesPrefix(tsType, types);
            }

            returnType = retTypeDesc.Name ?? "any";
            if (retTypeDesc.GenericArguments != null && retTypeDesc.GenericArguments.Count > 0)
            {
                // 泛型参数类型加 types. 前缀（如有必要）
                var genArgs = retTypeDesc.GenericArguments.Select(arg =>
                {
                    var tsType = MapCSharpTypeToTs(arg);
                    return AddTypesPrefix(tsType, types);
                });
                returnType += $"<{string.Join(", ", genArgs)}>";
            }

            returnType = $"types.{returnType}";
        }
        else
        {
            // 情况2：类型不存在于 types 字典中，直接从类型字符串解析

            // 首先尝试解包操作
            var typeString = api.ReturnType?.Type;
            if (!string.IsNullOrEmpty(typeString))
            {
                var typeInfo = ParseGenericType(typeString);

                // 检查是否需要解包（基于类型名判断）
                var baseTypeName = ExtractTypeName(typeInfo.BaseType);
                if (!string.IsNullOrEmpty(baseTypeName) && unwrapGenericTypes.Contains(baseTypeName)
                                                        && typeInfo.GenericArguments.Count == 1)
                {
                    // 解包：返回泛型参数而不是包装类型
                    var tsType = MapCSharpTypeToTs(typeInfo.GenericArguments[0]);
                    return AddTypesPrefix(tsType, types);
                }
            }

            // 常规类型映射
            returnType = MapCSharpTypeToTs(api.ReturnType?.Type);
            returnType = AddTypesPrefix(returnType, types);
        }

        return returnType;
    }

    /// <summary>
    /// 从完整类型名中提取类型名（用于解包判断）
    /// </summary>
    private static string ExtractTypeName(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName)) return "";

        // 对于 VividCMS 类型，提取最后的类型名
        if (fullTypeName.StartsWith($"{_namespacePrefix}"))
        {
            return ExtractVividCmsTypeName(fullTypeName);
        }

        // 对于其他类型，取最后一部分
        var parts = fullTypeName.Split('.');
        var typeName = parts.Last();

        // 处理嵌套类型
        if (typeName.Contains("+"))
        {
            typeName = typeName.Split('+').Last();
        }

        return typeName;
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
    
    /// <summary>
    /// 执行后置命令
    /// </summary>
    public void ExecutePostGenerationCommand()
    {
        if (!_config.EnablePostGenerationCommand || string.IsNullOrWhiteSpace(_config.Command))
        {
            return;
        }
        
        Console.WriteLine("开始执行后置命令...");
        try
        {
            ExecuteCommand();
            Console.WriteLine("后置命令执行完成。");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"后置命令执行失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 执行配置中指定的命令
    /// </summary>
    private void ExecuteCommand()
    {
        if (string.IsNullOrWhiteSpace(_config.Command))
        {
            Console.WriteLine("未配置命令，跳过执行。");
            return;
        }
        
        var processInfo = new ProcessStartInfo();
        
        // 设置工作目录
        if (!string.IsNullOrWhiteSpace(_config.CommandWorkingDirectory))
        {
            var workingDir = Path.IsPathRooted(_config.CommandWorkingDirectory) 
                ? _config.CommandWorkingDirectory 
                : Path.GetFullPath(_config.CommandWorkingDirectory);
                
            if (Directory.Exists(workingDir))
            {
                processInfo.WorkingDirectory = workingDir;
                Console.WriteLine($"命令工作目录: {workingDir}");
            }
            else
            {
                Console.WriteLine($"警告: 指定的工作目录不存在: {workingDir}，使用当前目录");
            }
        }
        
        // 根据操作系统设置命令执行方式
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            processInfo.FileName = "cmd.exe";
            // 构建完整的命令字符串
            var fullCommand = _config.Command;
            if (!string.IsNullOrWhiteSpace(_config.CommandArguments))
            {
                fullCommand += $" {_config.CommandArguments}";
            }
            processInfo.Arguments = $"/c {fullCommand}";
        }
        else
        {
            processInfo.FileName = "/bin/bash";
            // 构建完整的命令字符串
            var fullCommand = _config.Command;
            if (!string.IsNullOrWhiteSpace(_config.CommandArguments))
            {
                fullCommand += $" {_config.CommandArguments}";
            }
            processInfo.Arguments = $"-c \"{fullCommand}\"";
        }
        
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        processInfo.UseShellExecute = false;
        processInfo.CreateNoWindow = true;
        
        Console.WriteLine($"执行命令: {processInfo.FileName} {processInfo.Arguments}");
        
        using var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new Exception("无法启动进程");
        }
        
        if (_config.WaitForCommandCompletion)
        {
            // 异步读取输出和错误流
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            // 等待进程完成
            bool hasExited;
            if (_config.CommandTimeoutMs > 0)
            {
                hasExited = process.WaitForExit(_config.CommandTimeoutMs);
            }
            else
            {
                process.WaitForExit();
                hasExited = true;
            }
            
            if (!hasExited)
            {
                process.Kill();
                throw new Exception($"命令执行超时 ({_config.CommandTimeoutMs}ms)");
            }
            
            // 获取输出
            var output = outputTask.Result;
            var error = errorTask.Result;
            
            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine("命令输出:");
                Console.WriteLine(output);
            }
            
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine("命令错误信息:");
                Console.WriteLine(error);
            }
            
            if (process.ExitCode != 0)
            {
                throw new Exception($"命令执行失败，退出代码: {process.ExitCode}");
            }
        }
        else
        {
            Console.WriteLine("命令已在后台启动，不等待完成。");
        }
    }

    /// <summary>
    /// 判断类型是否为复杂类型（自定义类型，非基础类型）
    /// </summary>
    /// <param name="typeFullName">类型的完整名称</param>
    /// <param name="types">类型字典</param>
    /// <returns>是否为复杂类型</returns>
    private static bool IsComplexType(string? typeFullName, Dictionary<string, TypeDescriptionDto> types)
    {
        if (string.IsNullOrEmpty(typeFullName))
            return false;

        // 基础类型列表
        var basicTypes = new HashSet<string>
        {
            "System.String", "System.Int32", "System.Int64", "System.Int16",
            "System.Double", "System.Single", "System.Decimal", "System.Boolean",
            "System.DateTime", "System.DateTimeOffset", "System.Guid",
            "System.Object", "string", "number", "boolean", "any"
        };

        // 如果是基础类型，返回 false
        if (basicTypes.Contains(typeFullName))
            return false;

        // 如果是数组类型，检查元素类型
        if (typeFullName.EndsWith("[]"))
        {
            var elementType = typeFullName.Substring(0, typeFullName.Length - 2);
            return IsComplexType(elementType, types);
        }

        // 如果是可空类型（System.Nullable<T>），检查内部类型
        if (typeFullName.StartsWith("System.Nullable<") && typeFullName.EndsWith(">"))
        {
            var innerType = typeFullName.Substring(16, typeFullName.Length - 17);
            return IsComplexType(innerType, types);
        }

        // 如果是泛型类型，简单判断为复杂类型
        if (typeFullName.Contains("<") && typeFullName.Contains(">"))
            return true;

        // 检查是否在自定义类型字典中
        var typeName = ExtractTypeName(typeFullName);
        if (types.ContainsKey(typeName))
            return true;

        // 检查是否是项目自定义类型（通常有特定的命名空间前缀）
        if (typeFullName.Contains(".") && !typeFullName.StartsWith("System."))
            return true;

        return false;
    }
}