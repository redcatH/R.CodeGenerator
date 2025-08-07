using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace R.DescriptionModelGenerator;

public class AspNetCoreApiDescriptionModelProviderService
{
    private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionProvider;

    public AspNetCoreApiDescriptionModelProviderService(
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider)
    {
        _apiDescriptionProvider = apiDescriptionProvider;
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
                        Type = p.Type?.FullName?.Replace('+', '.'),
                        Source = p.Source?.Id,
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

    // abp风格的ReturnType处理
    public static ReturnTypeModel? CreateReturnTypeModel(Type? type)
    {
        if (type == null) return null;
        var unwrappedType = UnwrapTask(type);
        if (unwrappedType == null) return null;
        return new ReturnTypeModel
        {
            Type = unwrappedType != null ? CalculateTypeName(unwrappedType) : string.Empty,
            TypeSimple = unwrappedType != null ? GetSimpleTypeName(unwrappedType) : string.Empty
        };
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

    // 具体类型定义
    public class ApiDescriptionModelResult
    {
        public List<ApiDescriptionDto> Apis { get; set; } = new();
        public Dictionary<string, TypeDescriptionDto> Types { get; set; } = new();
    }

    public class ApiDescriptionDto
    {
        public string? Controller { get; set; }
        public string? Action { get; set; }
        public string? HttpMethod { get; set; }
        public string? Path { get; set; }
        public List<ApiParameterDescriptionDto> Parameters { get; set; } = new();
        public ReturnTypeModel? ReturnType { get; set; }
    }

    private static bool GetIsOptional(ApiParameterDescription p)
    {
        bool isOptional = false;
        var paramType = p.Type;
        if (p.RouteInfo?.IsOptional == true)
        {
            isOptional = true;
        }
        else if (paramType != null)
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
        if (type == null) return;
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
                    if (arg != null)
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
                bool isNullable = false;
                bool isRequired = false;
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

                isRequired = p.GetCustomAttributes().Any(a => a.GetType().Name == "RequiredAttribute") ||
                             (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null);
                return new TypePropertyDescriptionDto
                {
                    Name = p.Name,
                    Type = FriendlyTypeName(CalculateTypeName(propertyType)),
                    IsNullable = isNullable,
                    IsRequired = isRequired
                };
            }).ToList();
        types[typeName] = new TypeDescriptionDto
        {
            Name = type.Name,
            Namespace = type.Namespace,
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

    // ReturnTypeModel 类，仿 abp 的 ReturnValueApiDescriptionModel
    public class ReturnTypeModel
    {
        public string? Type { get; set; }
        public string? TypeSimple { get; set; }
    }


    private static string CalculateTypeName(Type type)
    {
        if (type == null) return string.Empty;
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

        var argNames = string.Join(",", genericArgs.Select(t => t != null ? CalculateTypeName(t) : string.Empty));
        return $"{baseName}<{argNames}>";
    }
}