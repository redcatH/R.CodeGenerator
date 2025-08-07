# 🚀 CodeGenerator

> 通过解析 ASP.NET Core API 路由及描述信息，自动生成前端 TypeScript 代码。

**[English version see README.md](./README.md)**

---

## 📝 项目简介

- 🔍 解析 ASP.NET Core 的 API 路由与类型描述（基于 Swagger 或 ApiDescription）。
- ⚡ 自动生成 TypeScript 类型定义和 API 调用代码。
- 📦 内置 axios 请求模板，方便前端直接调用后端接口。

---

## ✨ 主要功能

- 🌐 支持从本地或远程 swagger.json 解析 API 信息。
- 🛠️ 自动生成 TypeScript 类型文件和 API 封装文件。
- ⚙️ 可自定义输出目录、命名空间前缀等参数。
- 🧩 生成代码默认适配 axios，可根据需求扩展模板。

---

## 🚦 使用方法

1. 🏗️ 配置好 ASP.NET Core 项目，确保能获取到 swagger.json 或 ApiDescription。
2. ▶️ 运行本项目，指定 swagger.json 地址或本地路径。
3. 📁 生成的 TypeScript 代码可直接在前端项目中使用。

---

## 💻 示例

```bash
dotnet run --project R.CodeGenerator.Test "http://localhost:5200/api-description-model"
```

---

## ⏳ 未完成/待办事项

- [ ] 支持更多 HTTP 客户端模板（如 fetch、uni.request 等）
- [ ] 支持自定义模板扩展
- [ ] 更丰富的类型映射与注释生成
- [ ] 前端代码生成的自动化集成
- [ ] 完善的单元测试和文档

---

🙏 欢迎提出建议和 PR！
