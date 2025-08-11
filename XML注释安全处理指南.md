# XML注释安全处理 - 使用指南

## 概述

我们实现了一套完整的双重安全保护机制，确保XML注释内容在生成TypeScript代码时不会破坏JSDoc语法结构。

## 特性

### 1. 双重安全保护
- **C#层面清理**：在代码生成前预处理所有注释内容
- **模板层面保护**：在Scriban模板中进行最后的安全检查

### 2. 配置化处理
- 支持自定义注释长度限制
- 可配置HTML标签保留策略
- 灵活的换行处理选项

### 3. 智能格式化
- 保留重要的注释结构
- 自动压缩多余空白字符
- 支持多行注释的合理截断

## 使用方法

### 基础使用

```csharp
// 使用默认配置
var generator = new ApiCodeGenerator();
generator.GenerateTypes(types, outputDir, useInterface, namespacePrefix);
generator.GenerateApis(apis, outputDir, importLines, types, unwrapGenericTypes);
```

### 自定义配置

```csharp
// 创建自定义配置
var config = new ApiGeneratorConfig
{
    CommentConfig = new CommentSanitizerConfig
    {
        MaxLength = 300,        // 最大注释长度
        MaxLines = 8,           // 最大行数
        PreserveHtmlTags = true,// 保留HTML标签
        PreserveLineBreaks = true // 保留换行结构
    },
    GenerateDetailedComments = true,    // 生成详细注释
    EnableTemplateSafety = true,        // 启用模板安全检查
    TypePrefix = "types.",              // 类型前缀
    UnwrapGenericTypes = new HashSet<string> { "ApiResult", "PagedResult" }
};

// 使用自定义配置
var generator = new ApiCodeGenerator(config);
```

## 安全处理机制

### 1. 危险字符转义
- `*/` → `*​/` (使用零宽度空格)
- `/*` → `/​*` (使用零宽度空格)

### 2. 空白字符规范化
- 换行符转换为空格
- 制表符转换为空格
- 压缩连续空格

### 3. HTML实体清理
- `&lt;` → `<`
- `&gt;` → `>`
- `&amp;` → `&`
- `&quot;` → `"`
- `&apos;` → `'`

### 4. 长度和行数限制
- 支持配置最大字符长度
- 支持配置最大行数
- 超长内容自动截断并添加省略号

## 模板增强

模板中包含了自定义的安全函数 `safe_comment`，提供额外的保护：

```scriban
{{~ 
func safe_comment(text)
  if text == null || text == ""
    ret ""
  end
  # 双重安全：替换危险字符
  text = text | string.replace "*/" "*​/"
  text = text | string.replace "/*" "/​*"
  # ... 更多处理逻辑
  ret text | string.trim
end
~}}
```

## 最佳实践

1. **渐进式配置**：从默认配置开始，根据项目需要逐步调整
2. **测试驱动**：在正式环境使用前，用包含特殊字符的XML注释进行测试
3. **监控生成结果**：定期检查生成的TypeScript代码是否符合预期
4. **版本控制**：将生成的代码纳入版本控制，便于发现问题

## 故障排除

### 常见问题

**Q: 注释内容被截断了？**
A: 检查 `CommentSanitizerConfig.MaxLength` 和 `MaxLines` 配置

**Q: HTML标签被移除了？**
A: 设置 `CommentSanitizerConfig.PreserveHtmlTags = true`

**Q: 多行格式丢失？**
A: 设置 `CommentSanitizerConfig.PreserveLineBreaks = true`

**Q: 生成的代码有语法错误？**
A: 确保 `EnableTemplateSafety = true`，这会启用模板级别的额外保护

### 调试建议

1. 启用详细日志输出
2. 检查原始XML注释内容
3. 验证清理后的注释内容
4. 检查最终生成的TypeScript代码

## 升级指南

如果你已经在使用旧版本的 `ApiCodeGenerator`，升级步骤：

1. 创建配置对象（可选，不创建则使用默认配置）
2. 更新构造函数调用
3. 测试生成结果
4. 根据需要调整配置

```csharp
// 旧版本
var generator = new ApiCodeGenerator();

// 新版本 - 兼容旧用法
var generator = new ApiCodeGenerator();

// 新版本 - 使用配置
var generator = new ApiCodeGenerator(customConfig);
```
