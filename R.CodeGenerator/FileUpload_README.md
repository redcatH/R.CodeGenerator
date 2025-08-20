# 文件上传 API 生成器支持说明

## 概述

当 ASP.NET Core 控制器中包含 `IFormFile` 类型的参数时，代码生成器会自动生成支持文件上传的 TypeScript API 客户端代码。

## 支持的文件类型

代码生成器会将以下 C# 类型识别为文件类型，并生成相应的 TypeScript 文件上传代码：

- `Microsoft.AspNetCore.Http.IFormFile`
- `System.IO.Stream`
- `System.Byte[]` / `byte[]`

## 生成的 TypeScript 类型

文件类型会被映射为 TypeScript 的 `File` 类型：

```csharp
// C# 控制器
public async Task<IActionResult> UploadFile(IFormFile file, string description)
```

```typescript
// 生成的 TypeScript API
async uploadFile(file: File, description: string) {
  // 文件上传请求，使用 FormData
  const formData = new FormData();
  if (file) {
    formData.append('file', file);
  }
  formData.append('description', JSON.stringify(description));
  
  return requestClient.post<any>(`api/upload`, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });
}
```

## 使用示例

### 1. 单文件上传

```csharp
// C# Controller
[HttpPost("upload")]
public async Task<IActionResult> UploadFile(IFormFile file, string description)
{
    // 处理文件上传逻辑
    return Ok(new { Message = "File uploaded successfully" });
}
```

```typescript
// 前端使用
const fileInput = document.querySelector<HTMLInputElement>('#file-input');
const file = fileInput?.files?.[0];

if (file) {
  const result = await ApiService.uploadFile(file, "这是一个文件描述");
  console.log(result);
}
```

### 2. 多文件上传

```csharp
// C# Controller
[HttpPost("upload-multiple")]
public async Task<IActionResult> UploadMultipleFiles(
    IFormFile avatar, 
    IFormFile document, 
    string userId)
{
    // 处理多文件上传逻辑
    return Ok();
}
```

```typescript
// 生成的 API
async uploadMultipleFiles(avatar: File, document: File, userId: string) {
  const formData = new FormData();
  if (avatar) {
    formData.append('avatar', avatar);
  }
  if (document) {
    formData.append('document', document);
  }
  formData.append('userId', JSON.stringify(userId));
  
  return requestClient.post<any>(`api/upload-multiple`, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });
}

// 前端使用
const avatarFile = avatarInput.files?.[0];
const documentFile = documentInput.files?.[0];

if (avatarFile && documentFile) {
  await ApiService.uploadMultipleFiles(avatarFile, documentFile, "user123");
}
```

### 3. 文件 + 复杂对象

```csharp
// C# Controller
[HttpPost("upload-with-metadata")]
public async Task<IActionResult> UploadWithMetadata(
    IFormFile file, 
    [FromBody] FileMetadata metadata)
{
    return Ok();
}

public class FileMetadata
{
    public string Name { get; set; }
    public string[] Tags { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

```typescript
// 生成的 API
async uploadWithMetadata(file: File, metadata: types.FileMetadata) {
  const formData = new FormData();
  if (file) {
    formData.append('file', file);
  }
  formData.append('metadata', JSON.stringify(metadata));
  
  return requestClient.post<any>(`api/upload-with-metadata`, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });
}

// 前端使用
const metadata: FileMetadata = {
  name: "重要文档",
  tags: ["文档", "重要"],
  createdAt: new Date().toISOString()
};

await ApiService.uploadWithMetadata(selectedFile, metadata);
```

## Vue 3 组件使用示例

```vue
<template>
  <div class="file-upload">
    <input 
      ref="fileInput" 
      type="file" 
      @change="handleFileSelect"
      accept="image/*,.pdf,.doc,.docx"
    >
    <button @click="uploadFile" :disabled="!selectedFile || uploading">
      {{ uploading ? '上传中...' : '上传文件' }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { ElMessage } from 'element-plus';
import ApiService from '@/api/ApiService';

const fileInput = ref<HTMLInputElement>();
const selectedFile = ref<File | null>(null);
const uploading = ref(false);

function handleFileSelect(event: Event) {
  const target = event.target as HTMLInputElement;
  selectedFile.value = target.files?.[0] || null;
}

async function uploadFile() {
  if (!selectedFile.value) {
    ElMessage.error('请选择文件');
    return;
  }

  try {
    uploading.value = true;
    
    const result = await ApiService.uploadFile(
      selectedFile.value, 
      '文件描述'
    );
    
    ElMessage.success('文件上传成功');
    console.log('Upload result:', result);
    
    // 重置文件选择
    selectedFile.value = null;
    if (fileInput.value) {
      fileInput.value.value = '';
    }
  } catch (error) {
    ElMessage.error('文件上传失败');
    console.error('Upload error:', error);
  } finally {
    uploading.value = false;
  }
}
</script>
```

## 注意事项

1. **Content-Type 自动设置**: 生成的代码会自动设置 `Content-Type: multipart/form-data` 头部。

2. **文件验证**: 前端应该进行文件大小、类型等验证：
   ```typescript
   function validateFile(file: File): boolean {
     const maxSize = 10 * 1024 * 1024; // 10MB
     const allowedTypes = ['image/jpeg', 'image/png', 'application/pdf'];
     
     if (file.size > maxSize) {
       ElMessage.error('文件大小不能超过 10MB');
       return false;
     }
     
     if (!allowedTypes.includes(file.type)) {
       ElMessage.error('不支持的文件类型');
       return false;
     }
     
     return true;
   }
   ```

3. **进度监听**: 可以通过 axios 的配置添加上传进度监听：
   ```typescript
   return requestClient.post<any>(`api/upload`, formData, {
     headers: {
       'Content-Type': 'multipart/form-data',
     },
     onUploadProgress: (progressEvent) => {
       const progress = Math.round(
         (progressEvent.loaded * 100) / progressEvent.total
       );
       console.log(`Upload progress: ${progress}%`);
     },
   });
   ```

4. **错误处理**: 服务器端应该返回清晰的错误信息，前端要有相应的错误处理逻辑。

5. **安全考虑**: 
   - 服务器端需要验证文件类型和大小
   - 需要防范恶意文件上传
   - 建议使用病毒扫描
   - 文件名应该进行清理或重命名

## axios 支持

是的，axios 完全支持文件上传：

- ✅ `FormData` 对象自动处理
- ✅ `multipart/form-data` 编码
- ✅ 上传进度监听
- ✅ 文件 + 其他数据混合上传
- ✅ 多文件上传
- ✅ 错误处理

生成的代码使用标准的 Web API（`FormData`），因此与所有现代浏览器和 HTTP 客户端库兼容。
