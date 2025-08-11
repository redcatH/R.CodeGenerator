# 🚀 CodeGenerator

> Generate TypeScript code for frontend by parsing ASP.NET Core API routes and descriptions.

**[中文说明请见 README-zh_CN.md](./README-zh_CN.md)**

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
- 📝 **XML Documentation Support**: Extract and generate JSDoc comments from C# XML documentation.
- 🔍 **Complete Type Information**: Include parameter descriptions, return value documentation, and property comments.

---

## 🚦 Quick Start

### Step 1: Integrate into Your ASP.NET Core API Project

1. Enable XML documentation generation in your API project file:
```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DocumentationFile>bin\$(Configuration)\$(TargetFramework)\$(AssemblyName).xml</DocumentationFile>
</PropertyGroup>
```

2. Add the R.DescriptionModelGenerator package reference:
```xml
<PackageReference Include="R.DescriptionModelGenerator" Version="1.0.0" />
```

3. Configure services in your existing `Program.cs`:
```csharp
// Your existing service configuration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add the API description model provider service
builder.Services.AddAspNetCoreApiDescriptionModelProvider();

var app = builder.Build();

// Your existing middleware configuration
app.UseRouting();
// ... other middleware

// Add the API description model provider
app.UseAspNetCoreApiDescriptionModelProvider();

// Map your existing controllers
app.MapControllers();

// Add the API description endpoint
app.MapAspNetCoreApiDescriptionModelProviderEndpoint(); // Creates /api-description-model endpoint

app.Run();
```

### Step 2: Generate TypeScript Code

Use the R.CodeGenerator.Test project to generate TypeScript code from your running API:

```bash
# Basic usage - generates to default directories
dotnet run --project R.CodeGenerator.Test "http://localhost:5000/api-description-model"

# With custom config file
dotnet run --project R.CodeGenerator.Test config.json "http://localhost:5000/api-description-model"
```

#### Configuration Parameters:
- **First parameter**: Configuration file path (optional, defaults to built-in config)
- **Second parameter**: API description model URL from your running ASP.NET Core API

#### Example config.json:
```json
{
  "outputDir": "frontend/src/api",
  "typesDir": "frontend/src/types", 
  "useInterface": true,
  "importLine": [
    "import { request as requestHttp } from '../request';"
  ],
  "namespacePrefix": "YourProject.Api",
  "unwrapGenericTypes": ["ApiResult", "ResponseWrapper"]
}
```

### Step 3: Generated TypeScript Code

The generator creates two types of files with complete JSDoc comments:

**Type Definitions** (`src/types/`):
```typescript
/**
 * User information model
 * 
 * Contains all basic user properties and relationships
 */
export interface UserDto {
  /**
   * User unique identifier
   */
  id: number;
  
  /**
   * User display name
   * 
   * Must be between 2-50 characters
   */
  name: string;
  
  /**
   * User email address
   */
  email?: string;
}
```

**API Services** (`src/api/`):
```typescript
const UserService = {
  /**
   * Create a new user
   * 
   * This endpoint allows you to create a new user account with the provided details
   * @param request User creation request containing user details
   * @returns Returns the created user information
   */
  createUser(request: types.CreateUserRequest) {
    return requestHttp<types.UserDto>({
      url: '/api/User/CreateUser',
      method: 'post',
      data: { request }
    });
  },
};
```

---

## ⚙️ Configuration Options

| Option | Type | Description | Default |
|--------|------|-------------|---------|
| `outputDir` | string | API service files output directory | `"api"` |
| `typesDir` | string | Type definition files output directory | `"types"` |
| `useInterface` | boolean | Use `interface` instead of `type` | `true` |
| `importLine` | string[] | Custom import statements | `[]` |
| `namespacePrefix` | string | Filter types by namespace prefix | `""` |
| `unwrapGenericTypes` | string[] | Generic types to unwrap (e.g., `ApiResult<T>` → `T`) | `[]` |

---

## 🎯 Advanced Usage

### Custom Templates

You can customize the generated code by modifying the template file:
```
R.CodeGenerator/Templates/api_service.sbn
```

### Multiple Namespace Support

```json
{
  "namespacePrefix": "MyApp.Core",
  "unwrapGenericTypes": ["Result", "ApiResponse", "PagedResult"]
}
```

### Integration with Build Process

Add to your `package.json`:
```json
{
  "scripts": {
    "generate-api": "dotnet run --project ../Backend/CodeGenerator config.json http://localhost:5000/api-description-model",
    "build": "npm run generate-api && vite build"
  }
}
```

---

## 💻 Complete Example

### C# Controller with XML Documentation

```csharp
/// <summary>
/// Employee management API
/// </summary>
/// <remarks>
/// Provides endpoints for managing employee information including
/// creation, updates, and retrieval operations
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    /// <summary>
    /// Create a new employee
    /// </summary>
    /// <param name="request">Employee creation request with personal details</param>
    /// <returns>Returns the created employee with assigned ID</returns>
    /// <remarks>
    /// This endpoint validates the employee data and creates a new record
    /// in the system with auto-generated employee ID
    /// </remarks>
    [HttpPost("create")]
    public async Task<ApiResult<Employee>> CreateEmployee(CreateEmployeeRequest request)
    {
        // Implementation...
    }
}

/// <summary>
/// Employee data model
/// </summary>
/// <remarks>
/// Represents an employee entity with all required information
/// </remarks>
public class Employee
{
    /// <summary>
    /// Employee unique identifier
    /// </summary>
    /// <remarks>
    /// Auto-generated when employee is created
    /// </remarks>
    public int Id { get; set; }
    
    /// <summary>
    /// Employee full name
    /// </summary>
    /// <remarks>
    /// Must be between 2-100 characters
    /// </remarks>
    [Required]
    public string Name { get; set; }
}
```

### Generated TypeScript Types

```typescript
/**
 * Employee data model
 * 
 * Represents an employee entity with all required information
 */
export interface Employee {
  /**
   * Employee unique identifier
   * 
   * Auto-generated when employee is created
   */
  id: number;
  
  /**
   * Employee full name
   * 
   * Must be between 2-100 characters
   */
  name: string;
}
```

### Generated API Service

```typescript
const EmployeeService = {
  /**
   * Create a new employee
   * 
   * This endpoint validates the employee data and creates a new record
   * in the system with auto-generated employee ID
   * @param request Employee creation request with personal details
   * @returns Returns the created employee with assigned ID
   */
  createEmployee(request: types.CreateEmployeeRequest) {
    return requestHttp<types.Employee>({
      url: '/api/Employee/create',
      method: 'post',
      data: { request }
    });
  },
};
```

### Command to Generate

```bash
dotnet run --project R.CodeGenerator.Test "http://localhost:5000/api-description-model"
```

---

## ⏳ TODO

- [ ] Support more HTTP client templates (e.g., fetch, uni.request, etc.)
- [ ] Support custom template extensions
- [x] ~~More comprehensive type mapping and comment generation~~ ✅ **Completed**: Full XML documentation support with JSDoc generation
- [x] ~~Extract parameter and return value comments from XML documentation~~ ✅ **Completed**: @param and @returns JSDoc tags
- [ ] Automated integration for frontend code generation
- [ ] Complete unit tests and documentation
- [ ] Support for OpenAPI 3.0 specifications
- [ ] Generic type constraint handling
- [ ] Enum type generation with documentation
- [ ] Validation attribute integration (e.g., Required, Range, etc.)

---

## 🆕 What's New

### v2.0.0 - XML Documentation Support
- ✨ **Full XML Comments Extraction**: Automatically extract `<summary>`, `<remarks>`, `<param>`, and `<returns>` from C# XML documentation
- 📝 **JSDoc Generation**: Generate complete JSDoc comments for TypeScript interfaces and API methods
- 🔍 **Enhanced Type Information**: Include property descriptions and parameter documentation
- 🎯 **Better Developer Experience**: Rich IntelliSense support in TypeScript IDEs

### Example Generated Code with Comments:
```typescript
/**
 * Create a new employee record
 * 
 * This method creates a new employee with the provided information
 * and returns the created employee data with assigned ID
 * @param request Employee information request containing personal details
 * @returns Employee creation result with assigned ID and metadata
 */
createEmployee(request: types.EmployeeRequest) {
  return requestHttp<types.Employee>({
    url: '/api/Employee/CreateEmployee',
    method: 'post', 
    data: { request }
  });
}
```

---

## 🔧 Troubleshooting

### Common Issues

**XML Documentation Not Generated**
- Ensure `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is in your `.csproj`
- Check that XML files exist in your `bin` directory after building
- Verify XML comments use proper format with `///`

**API Description Model Endpoint Not Found**
- Ensure `services.AddAspNetCoreApiDescriptionModelProvider()` is called in service registration
- Make sure `app.MapAspNetCoreApiDescriptionModelProviderEndpoint()` is called after `app.MapControllers()`
- Test the endpoint: visit `http://localhost:5000/api-description-model` in browser

**No Types Generated or Empty Output**
- Check `namespacePrefix` in config matches your C# project namespace
- Ensure your controllers are decorated with `[ApiController]` attribute
- Verify your API is running and accessible at the specified URL

**Missing Comments in Generated TypeScript**
- Ensure XML documentation files (.xml) are generated in the same directory as your assemblies
- Check that your C# code uses standard XML doc tags (`<summary>`, `<param>`, `<returns>`)
- Verify the R.DescriptionModelGenerator package is properly referenced and configured

### Best Practices

1. **XML Documentation**: Add `<summary>` and `<param>` tags to all public API methods
2. **Namespace Consistency**: Use consistent namespace prefixes in your C# project for better filtering
3. **Build Integration**: Set up automated generation in your CI/CD pipeline
4. **Version Control**: Include generated TypeScript files in version control for consistency
5. **Testing**: Verify the `/api-description-model` endpoint returns complete data before generating code

---

## 🤝 Contributing

We welcome contributions! Please feel free to submit issues and pull requests.

### Development Setup

```bash
# Clone the repository
git clone https://github.com/redcatH/R.CodeGenerator.git

# Build the solution
dotnet build

# Run tests
dotnet test

# Run the test project
dotnet run --project R.CodeGenerator.Test
```
