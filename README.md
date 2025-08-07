# 🚀 CodeGenerator

> Generate TypeScript code for frontend by parsing ASP.NET Core API routes and descriptions.

**[中文说明请见 README-cn.md](./README-zh_CN.md)**

---

## 📝 Project Introduction

- 🔍 Parse ASP.NET Core API routes and type descriptions (based on Swagger or ApiDescription).
- ⚡ Automatically generate TypeScript type definitions and API call code.
- 📦 Built-in axios request template for easy frontend-backend integration.

---

## ✨ Features

- 🌐 Support parsing API info from local or remote swagger.json.
- 🛠️ Automatically generate TypeScript type files and API wrapper files.
- ⚙️ Customizable output directory, namespace prefix, etc.
- 🧩 Generated code uses axios by default, extensible for other templates.

---

## 🚦 Usage

1. 🏗️ Configure your ASP.NET Core project to expose swagger.json or ApiDescription.
2. ▶️ Run this project and specify the swagger.json URL or local path.
3. 📁 Use the generated TypeScript code directly in your frontend project.

---

## 💻 Example

```bash
dotnet run --project R.CodeGenerator.Test "http://localhost:5200/api-description-model"
```

---

## ⏳ TODO

- [ ] Support more HTTP client templates (e.g., fetch, uni.request, etc.)
- [ ] Support custom template extensions
- [ ] More comprehensive type mapping and comment generation
- [ ] Automated integration for frontend code generation
- [ ] Complete unit tests and documentation

---
Welcome to contribute and submit PRs!
