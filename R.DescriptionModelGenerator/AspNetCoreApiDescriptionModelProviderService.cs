using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using System.IO;

namespace R.DescriptionModelGenerator;

public class AspNetCoreApiDescriptionModelProviderService
{
    private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionProvider;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, XPathDocument> _xmlDocs = new();
    private readonly List<string> _includedNamespacePrefixes = new();

    public AspNetCoreApiDescriptionModelProviderService(
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>? jsonOptions = null,
        List<string>? includedNamespacePrefixes = null)
    {
        _apiDescriptionProvider = apiDescriptionProvider;
        _jsonOptions = jsonOptions?.Value.SerializerOptions ?? new JsonSerializerOptions();
        
        // 如果没有指定命名空间前缀，则默认排除系统命名空间
        _includedNamespacePrefixes = includedNamespacePrefixes ?? new List<string>();
        
        LoadXmlDocumentation();
    }

    /// <summary>
    /// 根据 JsonPropertyName 特性和 PropertyNamingPolicy 获取实际的 JSON 属性名
    /// </summary>
    private string GetJsonPropertyName(System.Reflection.PropertyInfo property)
    {
        // 1. 优先检查 JsonPropertyName 特性 - 使用 Attribute.GetCustomAttribute
        var jsonPropertyNameAttr = Attribute.GetCustomAttribute(property, typeof(JsonPropertyNameAttribute));
        if (jsonPropertyNameAttr is JsonPropertyNameAttribute attr)
        {
            return attr.Name;
        }

        // 2. 使用配置的 PropertyNamingPolicy
        if (_jsonOptions.PropertyNamingPolicy != null)
        {
            return _jsonOptions.PropertyNamingPolicy.ConvertName(property.Name);
        }

        // 3. 默认返回原属性名
        return property.Name;
    }

    /// <summary>
    /// 判断类型是否应该被包含（用于过滤系统类型）
    /// </summary>
    private bool ShouldIncludeType(Type type)
    {
        // 先尝试去除外层常见包装类型（Task<>, ActionResult<>, Nullable<> 等），再判断实际类型的命名空间
        if (type == null) return false;

        // 去除 Task 包装
        var actual = UnwrapTask(type) ?? type;

        // 去除 ActionResult 包装
        var unwrappedAction = UnwrapActionResult(actual);
        if (unwrappedAction == null)
        {
            // 如果解包后为 null，表示这是 IActionResult/JsonResult/NoContentResult 等，前端通常不需要此类型
            return false;
        }
        actual = unwrappedAction;

        // 去除 Nullable<T>
        if (actual.IsGenericType && actual.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlying = Nullable.GetUnderlyingType(actual);
            if (underlying != null)
                actual = underlying;
        }

        // 如果是数组或集合，取元素类型进行判断
        if (typeof(IEnumerable).IsAssignableFrom(actual) && actual != typeof(string))
        {
            if (actual.IsArray)
            {
                var elem = actual.GetElementType();
                if (elem != null)
                    actual = elem;
            }
            else if (actual.IsGenericType)
            {
                var args = actual.GetGenericArguments();
                if (args.Length == 1)
                    actual = args[0];
            }
        }

        // 现在根据实际类型的命名空间判断是否需要包含
        var typeNamespace = actual.Namespace ?? string.Empty;

        if (_includedNamespacePrefixes.Count == 0)
        {
            // 排除常见的系统命名空间
            if (typeNamespace.StartsWith("System") ||
                typeNamespace.StartsWith("Microsoft") ||
                typeNamespace.StartsWith("Newtonsoft") ||
                string.IsNullOrEmpty(typeNamespace))
            {
                return false;
            }

            return true;
        }

        // 如果配置了命名空间前缀列表，只包含匹配的命名空间
        var ns = typeNamespace;
        return _includedNamespacePrefixes.Any(prefix => ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 加载程序集的XML文档文件
    /// </summary>
    private void LoadXmlDocumentation()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .ToList();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
                    if (File.Exists(xmlPath) && !_xmlDocs.ContainsKey(assembly.FullName!))
                    {
                        var xmlDoc = new XPathDocument(xmlPath);
                        _xmlDocs[assembly.FullName!] = xmlDoc;
                    }
                }
                catch
                {
                    // 忽略单个程序集XML加载失败
                }
            }
        }
        catch
        {
            // 忽略XML文档加载失败
        }
    }

    /// <summary>
    /// 获取方法的XML注释（包括参数和返回值注释）
    /// </summary>
    private (string? summary, string? remarks, Dictionary<string, string> paramComments, string? returnsComment) GetMethodXmlDocumentationFull(MethodInfo method)
    {
        try
        {
            var assembly = method.DeclaringType?.Assembly;
            if (assembly?.FullName == null || !_xmlDocs.ContainsKey(assembly.FullName))
                return (null, null, new Dictionary<string, string>(), null);

            var xmlDoc = _xmlDocs[assembly.FullName];
            var navigator = xmlDoc.CreateNavigator();

            // 构建方法的XML注释路径
            var memberName = GetMethodMemberName(method);
            var xpath = $"/doc/members/member[@name='{memberName}']";
            var memberNode = navigator.SelectSingleNode(xpath);

            if (memberNode == null) 
                return (null, null, new Dictionary<string, string>(), null);

            var summaryNode = memberNode.SelectSingleNode("summary");
            var remarksNode = memberNode.SelectSingleNode("remarks");
            var returnsNode = memberNode.SelectSingleNode("returns");

            var summary = summaryNode?.Value?.Trim();
            var remarks = remarksNode?.Value?.Trim();
            var returnsComment = returnsNode?.Value?.Trim();

            // 获取参数注释
            var paramComments = new Dictionary<string, string>();
            var paramNodes = memberNode.Select("param");
            while (paramNodes.MoveNext())
            {
                var paramNode = paramNodes.Current;
                if (paramNode != null)
                {
                    var paramName = paramNode.GetAttribute("name", "");
                    var paramComment = paramNode.Value?.Trim();
                    if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramComment))
                    {
                        paramComments[paramName] = paramComment;
                    }  
                }
            }

            return (summary, remarks, paramComments, returnsComment);
        }
        catch
        {
            return (null, null, new Dictionary<string, string>(), null);
        }
    }

    /// <summary>
    /// 获取类型的XML注释
    /// </summary>
    private (string? summary, string? remarks) GetTypeXmlDocumentation(Type type)
    {
        try
        {
            var assembly = type.Assembly;
            if (assembly?.FullName == null || !_xmlDocs.ContainsKey(assembly.FullName))
                return (null, null);

            var xmlDoc = _xmlDocs[assembly.FullName];
            var navigator = xmlDoc.CreateNavigator();

            // 构建类型的XML注释路径
            var memberName = GetTypeMemberName(type);
            var xpath = $"/doc/members/member[@name='{memberName}']";
            var memberNode = navigator.SelectSingleNode(xpath);

            if (memberNode == null) return (null, null);

            var summaryNode = memberNode.SelectSingleNode("summary");
            var remarksNode = memberNode.SelectSingleNode("remarks");

            var summary = summaryNode?.Value?.Trim();
            var remarks = remarksNode?.Value?.Trim();

            return (summary, remarks);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// 获取属性的XML注释
    /// </summary>
    private (string? summary, string? remarks) GetPropertyXmlDocumentation(System.Reflection.PropertyInfo property)
    {
        try
        {
            var assembly = property.DeclaringType?.Assembly;
            if (assembly?.FullName == null || !_xmlDocs.ContainsKey(assembly.FullName))
                return (null, null);

            var xmlDoc = _xmlDocs[assembly.FullName];
            var navigator = xmlDoc.CreateNavigator();

            // 构建属性的XML注释路径
            var memberName = GetPropertyMemberName(property);
            var xpath = $"/doc/members/member[@name='{memberName}']";
            var memberNode = navigator.SelectSingleNode(xpath);

            if (memberNode == null) return (null, null);

            var summaryNode = memberNode.SelectSingleNode("summary");
            var remarksNode = memberNode.SelectSingleNode("remarks");

            var summary = summaryNode?.Value?.Trim();
            var remarks = remarksNode?.Value?.Trim();

            return (summary, remarks);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// 构建方法的XML成员名称
    /// </summary>
    private static string GetMethodMemberName(MethodInfo method)
    {
        var declaringType = method.DeclaringType;
        var typeName = declaringType?.FullName?.Replace('+', '.');
        var parameters = method.GetParameters();
        
        var memberName = $"M:{typeName}.{method.Name}";
        
        if (parameters.Length > 0)
        {
            var paramTypes = parameters.Select(p => GetXmlTypeName(p.ParameterType));
            memberName += $"({string.Join(",", paramTypes)})";
        }
        
        return memberName;
    }

    /// <summary>
    /// 构建类型的XML成员名称
    /// </summary>
    private static string GetTypeMemberName(Type type)
    {
        return $"T:{type.FullName?.Replace('+', '.')}";
    }

    /// <summary>
    /// 构建属性的XML成员名称
    /// </summary>
    private static string GetPropertyMemberName(System.Reflection.PropertyInfo property)
    {
        var declaringType = property.DeclaringType;
        var typeName = declaringType?.FullName?.Replace('+', '.');
        return $"P:{typeName}.{property.Name}";
    }

    /// <summary>
    /// 获取用于XML文档的类型名称
    /// </summary>
    private static string GetXmlTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var name = genericTypeDef.FullName?.Replace('+', '.');
            if (name != null)
            {
                var tickIndex = name.IndexOf('`');
                if (tickIndex > 0)
                    name = name.Substring(0, tickIndex);
            }
            
            var args = type.GetGenericArguments().Select(GetXmlTypeName);
            return $"{name}{{{string.Join(",", args)}}}";
        }
        
        return type.FullName?.Replace('+', '.') ?? type.Name;
    }
    
    /// <summary>
    /// 获取所有API描述信息和相关类型信息，供前端生成api services
    /// </summary>
    /// 
    public ApiDescriptionModelResult GetApiDescriptionModel(bool includeTypes = true)
    {
        var apiGroups = _apiDescriptionProvider.ApiDescriptionGroups.Items;
        var apis = new List<ApiDescriptionDto>();
        var types = new Dictionary<string, TypeDescriptionDto>();

        foreach (var group in apiGroups)
        {
            foreach (var api in group.Items)
            {
                if (!api.ActionDescriptor.IsControllerAction())
                    continue;
                
                var controllerName = api.ActionDescriptor.RouteValues["controller"];
                var actionName = api.ActionDescriptor.RouteValues["action"];
                var httpMethod = api.HttpMethod;
                var relativePath = api.RelativePath;
                // 获取Action方法的XML注释
                string? summary = null;
                string? remarks = null;
                Dictionary<string, string> paramComments = new();
                string? returnsComment = null;
                
                if (api.ActionDescriptor is ControllerActionDescriptor cad)
                {
                    var (methodSummary, methodRemarks, methodParamComments, methodReturnsComment) = GetMethodXmlDocumentationFull(cad.MethodInfo);
                    summary = methodSummary;
                    remarks = methodRemarks;
                    paramComments = methodParamComments;
                    returnsComment = methodReturnsComment;
                }

                var parameters = api.ParameterDescriptions.Select(p =>
                {
                    var isOptional = GetIsOptional(p);
                    // 查找参数注释
                    paramComments.TryGetValue(p.Name, out var paramSummary);
                    
                    // 获取默认值 - 优先从 ParameterInfo 获取，其次从 RouteInfo 获取
                    object? defaultValue = null;
                    if (p.ParameterDescriptor is ControllerParameterDescriptor controllerParam)
                    {
                        var paramInfo = controllerParam.ParameterInfo;
                        if (paramInfo != null && paramInfo.HasDefaultValue)
                        {
                            defaultValue = paramInfo.DefaultValue;
                        }
                    }
                    // 如果从 ParameterInfo 没获取到，尝试从 RouteInfo 获取
                    if (defaultValue == null)
                    {
                        defaultValue = p.RouteInfo?.DefaultValue;
                    }
                    
                    return new ApiParameterDescriptionDto
                    {
                        Name = p.Name,
                        Type = p.Type.FullName?.Replace('+', '.'),
                        Source = p.Source.Id,
                        IsOptional = isOptional,
                        DefaultValue = defaultValue,
                        Summary = paramSummary
                    };
                }).ToList();

                apis.Add(new ApiDescriptionDto
                {
                    Controller = controllerName,
                    Action = actionName,
                    HttpMethod = httpMethod,
                    Path = relativePath,
                    Parameters = parameters,
                    ReturnType = api.ActionDescriptor is ControllerActionDescriptor cad2
                        ? CreateReturnTypeModel(cad2.MethodInfo.ReturnType, returnsComment)
                        : null,
                    Summary = summary,
                    Remarks = remarks
                });

                if (includeTypes && api.ActionDescriptor is ControllerActionDescriptor cadForTypes)
                {
                    foreach (var p in cadForTypes.MethodInfo.GetParameters())
                    {
                        AddTypeRecursive(types, p.ParameterType);
                    }

                    AddTypeRecursive(types, cadForTypes.MethodInfo.ReturnType);
                }
            }
        }

        return new ApiDescriptionModelResult
        {
            Apis = apis,
            Types = types
        };
    }

    private static ReturnTypeModel? CreateReturnTypeModel(Type? type, string? returnsComment = null)
    {
        if (type == null) return null;
        var unwrappedType = UnwrapActionResult(UnwrapTask(type));
        if (unwrappedType == null) return null;
        return new ReturnTypeModel
        {
            Type = CalculateTypeName(unwrappedType),
            TypeSimple = GetSimpleTypeName(unwrappedType),
            Summary = returnsComment
        };
    }

    /// <summary>
    /// 解包 ActionResult<T>、IActionResult、ActionResult、Task<ActionResult<T>>、Task<T> 等，
    /// </summary>
    public static Type? UnwrapActionResult(Type? type)
    {
        if (type == null) return null;

        // 递归解包 Task<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            return UnwrapActionResult(inner);
        }

        // 解包 ActionResult<T>
        var actionResultType = typeof(ActionResult<>);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == actionResultType)
        {
            var inner = type.GetGenericArguments()[0];
            return UnwrapActionResult(inner);
        }

        // 如果不是 IActionResult 派生类，直接返回
        var iactionResultType = typeof(IActionResult);
        if (!iactionResultType.IsAssignableFrom(type))
        {
            return type;
        }

        // 针对 JsonResult/ObjectResult/NoContentResult 这些特殊类型
        var jsonResultType = typeof(JsonResult);
        var objectResultType = typeof(ObjectResult);
        var noContentResultType = typeof(NoContentResult);
        if (jsonResultType.IsAssignableFrom(type) || objectResultType.IsAssignableFrom(type) ||
            noContentResultType.IsAssignableFrom(type))
        {
            return null; // 通常前端不需要类型
        }

        // 非泛型 ActionResult、IActionResult、void、Task
        if (type == typeof(IActionResult) ||
            type == typeof(ActionResult) ||
            type == typeof(void) ||
            type == typeof(Task))
        {
            return null;
        }

        return null;
    }

    public static Type? UnwrapTask(Type? type)
    {
        if (type == null) return null;
        if (type == typeof(Task))
        {
            return typeof(void);
        }
        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    public static string GetSimpleTypeName(Type? type)
    {
        if (type == null) return string.Empty;
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var baseName = type.Name;
        var tickIndex = baseName.IndexOf('`');
        if (tickIndex > 0)
        {
            baseName = baseName.Substring(0, tickIndex);
        }

        var argNames = string.Join(",", type.GetGenericArguments().Select(GetSimpleTypeName));
        return $"{baseName}<{argNames}>";
    }

    private static bool GetIsOptional(ApiParameterDescription p)
    {
        var isOptional = false;
        var paramType = p.Type;
        
        // 1. 检查路由是否可选
        if (p.RouteInfo?.IsOptional == true)
        {
            isOptional = true;
        }
        // 2. 检查是否是 Nullable<T> (值类型的可空版本)
        else if (Nullable.GetUnderlyingType(paramType) != null)
        {
            isOptional = true;
        }
        // 3. 检查是否有默认值
        else if (p.RouteInfo?.DefaultValue != null)
        {
            isOptional = true;
        }
        // 4. 对于引用类型，检查可空性
        else if (!paramType.IsValueType)
        {
            // 引用类型，查NullableAttribute（通过ControllerParameterDescriptor.ParameterInfo）
            var controllerParam = p.ParameterDescriptor as ControllerParameterDescriptor;
            var paramInfo = controllerParam?.ParameterInfo;
            if (paramInfo != null)
            {
                // 检查参数是否有默认值
                if (paramInfo.HasDefaultValue)
                {
                    isOptional = true;
                }
                else
                {
                    var nullableAttr = paramInfo.GetCustomAttributes(false).FirstOrDefault(a =>
                        a.GetType().FullName == "System.Runtime.CompilerServices.NullableAttribute");
                    if (nullableAttr != null)
                    {
                        var attrType = nullableAttr.GetType();
                        var flagsProp = attrType.GetField("NullableFlags");
                        if (flagsProp != null)
                        {
                            var flags = flagsProp.GetValue(nullableAttr) as byte[];
                            if (flags != null && flags.Length > 0 && flags[0] == 2)
                                isOptional = true;
                        }
                    }
                    // 注意：这里不再默认设置为 false，而是保持原值
                }
            }
            else
            {
                isOptional = true;
            }
        }

        return isOptional;
    }

    private static string FriendlyTypeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace('+', '.');
    }

    private void AddTypeRecursive(Dictionary<string, TypeDescriptionDto> types, Type type)
    {
        if (type == typeof(string) || type.IsPrimitive || type == typeof(object) || type == typeof(void)) return;
        
        // 过滤掉不需要的命名空间（系统类型等）
        if (!ShouldIncludeType(type)) return;
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                AddTypeRecursive(types, underlying);
            return;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            if (type.IsArray)
            {
                var elemType = type.GetElementType();
                if (elemType != null)
                    AddTypeRecursive(types, elemType);
            }
            else if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    AddTypeRecursive(types, arg);
                }
            }

            return;
        }

        var typeName = FriendlyTypeName(CalculateTypeName(type));
        if (string.IsNullOrEmpty(typeName) || types.ContainsKey(typeName)) return;

        // 特殊处理：枚举类型 - 收集枚举成员和值
    if (type.IsEnum)
        {
            // 取枚举成员名与对应的数值（统一为 Int64 字符串表示）
            var enumValues = Enum.GetNames(type)
                .Select(n => {
                    var v = Enum.Parse(type, n);
                    string valStr;
                    try
                    {
                        valStr = Convert.ToInt64(v).ToString();
                    }
                    catch
                    {
                        // 若转换失败则回退为 ToString()
                        valStr = v?.ToString() ?? string.Empty;
                    }
                    return new EnumValueDto { Name = n, Value = valStr };
                }).ToList();

            // 处理 pureName（去掉泛型后缀等）
            var enumPureName = type.Name;
            var enumTickIndex = enumPureName.IndexOf('`');
            if (enumTickIndex > 0)
            {
                enumPureName = enumPureName.Substring(0, enumTickIndex);
            }

            var (enumTypeSummary, enumTypeRemarks) = GetTypeXmlDocumentation(type);

            types[typeName] = new TypeDescriptionDto
            {
                Name = enumPureName,
                Namespace = type.Namespace,
                BaseType = (type.BaseType != null && type.BaseType != typeof(object))
                    ? FriendlyTypeName(CalculateTypeName(type.BaseType))
                    : null,
                GenericArguments = new List<string>(),
                Properties = new List<TypePropertyDescriptionDto>(),
                Summary = enumTypeSummary,
                Remarks = enumTypeRemarks,
                EnumValues = enumValues
            };

            return;
        }

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Select(p =>
            {
                var propertyType = p.PropertyType;
                var isNullable = false;
                if (Nullable.GetUnderlyingType(propertyType) != null)
                {
                    isNullable = true;
                }
                else if (!propertyType.IsValueType)
                {
                    var nullableAttr = p.CustomAttributes.FirstOrDefault(a =>
                        a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
                    if (nullableAttr != null && nullableAttr.ConstructorArguments.Count > 0)
                    {
                        var arg = nullableAttr.ConstructorArguments[0];
                        byte flag = 0;
                        if (arg.ArgumentType == typeof(byte) && arg.Value != null)
                        {
                            flag = (byte)arg.Value;
                        }
                        else if (arg.ArgumentType == typeof(byte[]))
                        {
                            var arr = arg.Value as IReadOnlyCollection<CustomAttributeTypedArgument>;
                            if (arr != null && arr.Count > 0)
                            {
                                var firstVal = arr.First().Value;
                                if (firstVal != null)
                                    flag = (byte)firstVal;
                            }
                        }

                        isNullable = flag == 2;
                    }
                    else
                    {
                        var nullableContext = p.DeclaringType?.CustomAttributes.FirstOrDefault(a =>
                            a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
                        if (nullableContext != null && nullableContext.ConstructorArguments.Count == 1)
                        {
                            var val = nullableContext.ConstructorArguments[0].Value;
                            byte flag = 0;
                            if (val is byte b) flag = b;
                            else if (val is int i) flag = (byte)i;
                            isNullable = flag == 2;
                        }
                        else
                        {
                            isNullable = true;
                        }
                    }
                }

                var isRequired = p.GetCustomAttributes().Any(a => a.GetType().Name == "RequiredAttribute") ||
                                 (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null);
                
                // 获取属性的XML注释
                var (propSummary, propRemarks) = GetPropertyXmlDocumentation(p);
                
                return new TypePropertyDescriptionDto
                {
                    Name = GetJsonPropertyName(p), // 使用实际的 JSON 属性名
                    Type = FriendlyTypeName(CalculateTypeName(propertyType)),
                    IsNullable = isNullable,
                    IsRequired = isRequired,
                    Summary = propSummary,
                    Remarks = propRemarks
                };
            }).ToList();

        // 泛型参数类型全名
        List<string> genericArgs = new();
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                genericArgs.Add(FriendlyTypeName(CalculateTypeName(arg)));
            }
        }

        // 泛型类型名处理
        string pureName;
        if (type.IsGenericTypeDefinition)
        {
            // 泛型定义，如 ApiResult<T0>
            var i = 0;
            var argumentList = string.Join(",", type.GetGenericArguments().Select(_ => $"T{i++}"));
            var fullName = type.FullName ?? type.Name;
            var tickIdx = fullName.IndexOf('`');
            if (tickIdx > 0)
                fullName = fullName.Substring(0, tickIdx);
            pureName = $"{fullName}<{argumentList}>";
        }
        else
        {
            // 非泛型定义，去除 `1 之类的后缀
            pureName = type.Name;
            var tickIndex = pureName.IndexOf('`');
            if (tickIndex > 0)
            {
                pureName = pureName.Substring(0, tickIndex);
            }
        }

        // 获取类型的XML注释
        var (typeSummary, typeRemarks) = GetTypeXmlDocumentation(type);

        types[typeName] = new TypeDescriptionDto
        {
            Name = pureName,
            Namespace = type.Namespace,
            BaseType = (type.BaseType != null && type.BaseType != typeof(object))
                ? FriendlyTypeName(CalculateTypeName(type.BaseType))
                : null,
            GenericArguments = genericArgs,
            Properties = props,
            Summary = typeSummary,
            Remarks = typeRemarks
        };
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            AddTypeRecursive(types, p.PropertyType);
        }

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            AddTypeRecursive(types, type.BaseType);
        }
    }

    private static string CalculateTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericTypeDef = type.GetGenericTypeDefinition();
        var genericArgs = type.GetGenericArguments();
        var baseName = (genericTypeDef.FullName ?? genericTypeDef.Name);
        var tickIndex = baseName.IndexOf('`');
        if (tickIndex > 0)
        {
            baseName = baseName.Substring(0, tickIndex);
        }

        var argNames = string.Join(",", genericArgs.Select(CalculateTypeName));
        return $"{baseName}<{argNames}>";
    }
}