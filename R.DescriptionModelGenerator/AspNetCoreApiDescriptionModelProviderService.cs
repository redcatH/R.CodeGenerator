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

namespace R.DescriptionModelGenerator;

public class AspNetCoreApiDescriptionModelProviderService
{
    private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public AspNetCoreApiDescriptionModelProviderService(
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>? jsonOptions = null)
    {
        _apiDescriptionProvider = apiDescriptionProvider;
        _jsonOptions = jsonOptions?.Value.SerializerOptions ?? new JsonSerializerOptions();
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
                var parameters = api.ParameterDescriptions.Select(p =>
                {
                    var isOptional = GetIsOptional(p);
                    return new ApiParameterDescriptionDto
                    {
                        Name = p.Name,
                        Type = p.Type.FullName?.Replace('+', '.'),
                        Source = p.Source.Id,
                        IsOptional = isOptional,
                        DefaultValue = p.RouteInfo?.DefaultValue
                    };
                }).ToList();

                apis.Add(new ApiDescriptionDto
                {
                    Controller = controllerName,
                    Action = actionName,
                    HttpMethod = httpMethod,
                    Path = relativePath,
                    Parameters = parameters,
                    ReturnType = api.ActionDescriptor is ControllerActionDescriptor cad
                        ? CreateReturnTypeModel(cad.MethodInfo.ReturnType)
                        : null
                });

                if (includeTypes && api.ActionDescriptor is ControllerActionDescriptor cad2)
                {
                    foreach (var p in cad2.MethodInfo.GetParameters())
                    {
                        AddTypeRecursive(types, p.ParameterType);
                    }

                    AddTypeRecursive(types, cad2.MethodInfo.ReturnType);
                }
            }
        }

        return new ApiDescriptionModelResult
        {
            Apis = apis,
            Types = types
        };
    }

    private static ReturnTypeModel? CreateReturnTypeModel(Type? type)
    {
        if (type == null) return null;
        var unwrappedType = UnwrapActionResult(UnwrapTask(type));
        if (unwrappedType == null) return null;
        return new ReturnTypeModel
        {
            Type = CalculateTypeName(unwrappedType),
            TypeSimple = GetSimpleTypeName(unwrappedType)
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
        if (p.RouteInfo?.IsOptional == true)
        {
            isOptional = true;
        }
        else
        {
            if (Nullable.GetUnderlyingType(paramType) != null)
            {
                isOptional = true;
            }
            else if (!paramType.IsValueType)
            {
                // 引用类型，查NullableAttribute（通过ControllerParameterDescriptor.ParameterInfo）
                var controllerParam =
                    p.ParameterDescriptor as
                        ControllerParameterDescriptor;
                var paramInfo = controllerParam?.ParameterInfo;
                if (paramInfo != null)
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
                    else
                    {
                        // 默认引用类型可空
                        isOptional = false;
                    }
                }
                else
                {
                    isOptional = true;
                }
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
                return new TypePropertyDescriptionDto
                {
                    Name = GetJsonPropertyName(p), // 使用实际的 JSON 属性名
                    Type = FriendlyTypeName(CalculateTypeName(propertyType)),
                    IsNullable = isNullable,
                    IsRequired = isRequired
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

        types[typeName] = new TypeDescriptionDto
        {
            Name = pureName,
            Namespace = type.Namespace,
            BaseType = (type.BaseType != null && type.BaseType != typeof(object))
                ? FriendlyTypeName(CalculateTypeName(type.BaseType))
                : null,
            GenericArguments = genericArgs,
            Properties = props
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