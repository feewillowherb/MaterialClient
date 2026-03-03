# Technical Terminology English Notes Guide

This document defines which technical and professional terminology should retain English notes in translations.

## Core Technology Terms (Always Keep in English)

### Programming Languages & Frameworks
- **C#** (C Sharp)
- **.NET**
- **.NET Core**
- **Avalonia UI**
- **ReactiveUI**
- **Entity Framework** / **EF Core**

### Web & Network Protocols
- **HTTP**
- **HTTPS**
- **REST** / **RESTful**
- **API**
- **JSON**
- **XML**
- **WebSocket**
- **TCP/IP**
- **UDP**

### Data Formats & Standards
- **JSON**
- **XML**
- **YAML**
- **CSV**
- **UTF-8**
- **ASCII**
- **ISO**

### Database Terms
- **SQL**
- **ORM**
- **LINQ**
- **CRUD**
- **ACID**
- **Transaction**
- **Connection String**
- **Query**
- **Stored Procedure**

### Design Patterns & Architecture
- **MVC** (Model-View-Controller)
- **MVVM** (Model-View-ViewModel)
- **MVP** (Model-View-Presenter)
- **Repository Pattern**
- **Service Pattern**
- **Dependency Injection**
- **Singleton**
- **Factory**
- **Observer**
- **Command**
- **Strategy**

### Development Tools & Concepts
- **Git**
- **CI/CD**
- **Build**
- **Deploy**
- **Debug**
- **Refactor**
- **Unit Test**
- **Integration Test**
- **Mock**
- **Stub**

## Domain-Specific Terms (Keep English Notes)

### MaterialClient Domain
- **License Plate Recognition** (LPR)
- **OCR** (Optical Character Recognition)
- **IP Camera**
- **RTSP** (Real Time Streaming Protocol)
- **ONVIF** (Open Network Video Interface Forum)
- **SDK** (Software Development Kit)

### Weighing & Measurement
- **Scale** / **Balance**
- **Sensor**
- **ADC** (Analog-to-Digital Converter)
- **Modbus**
- **RS-232**
- **RS-485**
- **TCP/IP**

## Translation Format Guidelines

### Format 1: English Term Only (for very common terms)
For extremely common technical terms that are widely recognized in Chinese technical contexts:

```
API
HTTP
JSON
```

### Format 2: Chinese Translation + English (for less common terms)
For technical terms that may need clarification:

```
应用程序接口 (API)
超文本传输协议 (HTTP)
JavaScript 对象表示法 (JSON)
```

### Format 3: English Term + Chinese Translation (for explanations)
When explaining a term in a document:

```
API (Application Programming Interface) - 应用程序接口
HTTP (HyperText Transfer Protocol) - 超文本传输协议
JSON (JavaScript Object Notation) - JavaScript 对象表示法
```

## Terminology Classification by Usage Frequency

### Tier 1: Universal Terms (Keep in English only)
These terms are universally recognized and should never be translated:

```
API
HTTP
HTTPS
REST
JSON
XML
SQL
C#
.NET
Git
```

### Tier 2: Common Technical Terms (Chinese + English note)
These terms are well-known but benefit from Chinese translation:

```
应用程序接口 (API)
超文本传输协议 (HTTP)
数据库管理系统 (DBMS)
对象关系映射 (ORM)
依赖注入 (DI)
控制反转 (IoC)
```

### Tier 3: Domain-Specific Terms (Chinese + English note)
These terms are specific to the MaterialClient domain:

```
车牌识别 (LPR)
光学字符识别 (OCR)
网络视频接口 (ONVIF)
软件开发工具包 (SDK)
实时流传输协议 (RTSP)
```

## Code Comment Guidelines

### In Code Comments
When translating code comments, maintain English terminology for:

```csharp
// 使用 HTTP API 获取车牌识别数据
// 处理 JSON 响应并解析字段
// 执行 SQL 查询以获取称重记录
// 实现依赖注入模式
// 应用 MVVM 架构
```

### In Documentation
When translating documentation, follow these rules:

```markdown
## API 接口设计

本文档描述了 MaterialClient 使用的 HTTP API 接口规范。

### REST 端点

所有 API 端点遵循 REST 架构风格，使用 JSON 格式进行数据交换。
```

## Specific Examples by Category

### Programming Terms
```
变量
方法
类
接口
属性
事件
委托
异常
命名空间
程序集
```

### UI Framework Terms
```
窗口
控件
命令
绑定
样式
模板
资源
转换器
行为
触发器
```

### Data Access Terms
```
实体
仓储
上下文
查询
数据库连接
迁移
种子数据
关系
导航属性
外键
主键
```

### Build & Deployment Terms
```
构建
配置
发布
程序包
依赖
解决方案
项目
目标框架
运行时
```

## Consistency Rules

1. **First Use**: When a technical term first appears in a document, provide both Chinese and English
2. **Subsequent Uses**: After the first mention, use the format established
3. **Cross-Document**: Maintain consistency across all documents in the project
4. **Glossary Reference**: Always refer to the translation glossary for standard terms

## Examples in Context

### Documentation Example
```markdown
# 称重记录 API 文档

## 概述

本 API 提供称重记录的查询和管理功能，基于 REST 架构风格，使用 JSON 格式进行数据交换。

## 端点列表

### GET /api/weighing-records
获取所有称重记录列表。

**请求参数:**
- `pageSize`: 页面大小
- `pageNumber`: 页码

**响应:**
返回包含称重记录数组的 JSON 对象。
```

### Code Comment Example
```csharp
/// <summary>
/// 调用 HTTP API 获取车牌识别结果
/// </summary>
/// <param name="imageUrl">图像 URL</param>
/// <returns>LPR 识别结果对象 (JSON 格式)</returns>
public async Task<LprResult> GetLicensePlateRecognitionAsync(string imageUrl)
{
    // 构建 API 请求
    var request = new HttpRequestMessage(HttpMethod.Post, _lprApiEndpoint);

    // 设置 JSON 请求体
    request.Content = new StringContent(
        JsonSerializer.Serialize(new { imageUrl }),
        Encoding.UTF8,
        "application/json"
    );

    // 执行 HTTP 请求
    var response = await _httpClient.SendAsync(request);

    // 解析 JSON 响应
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<LprResult>(json);
}
```

## Review Checklist

Before finalizing any translation, verify:

- [ ] All Tier 1 terms (API, HTTP, etc.) are kept in English only
- [ ] Tier 2 and Tier 3 terms include both Chinese and English
- [ ] Code comments maintain English terminology for technical concepts
- [ ] First occurrence of terms includes full translation
- [ ] Consistent format is maintained throughout the document
- [ ] No arbitrary translations of standard technical terms
- [ ] Formatting follows the established guidelines

## Conclusion

Maintaining English notes for technical and professional terminology ensures:
- Clarity and precision in technical communication
- Consistency with industry standards
- Easy understanding by developers familiar with English terminology
- Professional presentation of technical content

These guidelines should be followed consistently across all translations in the MaterialClient project.
