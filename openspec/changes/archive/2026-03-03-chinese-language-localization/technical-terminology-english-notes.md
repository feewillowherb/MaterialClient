# 技术术语英文备注指南

本文定义在翻译中应保留英文备注的技术与专业术语。

## 核心技术术语（始终保留英文）

### 编程语言与框架
- **C#** (C Sharp)
- **.NET**
- **.NET Core**
- **Avalonia UI**
- **ReactiveUI**
- **Entity Framework** / **EF Core**

### 网络与 Web 协议
- **HTTP**
- **HTTPS**
- **REST** / **RESTful**
- **API**
- **JSON**
- **XML**
- **WebSocket**
- **TCP/IP**
- **UDP**

### 数据格式与标准
- **JSON**
- **XML**
- **YAML**
- **CSV**
- **UTF-8**
- **ASCII**
- **ISO**

### 数据库术语
- **SQL**
- **ORM**
- **LINQ**
- **CRUD**
- **ACID**
- **Transaction**
- **Connection String**
- **Query**
- **Stored Procedure**

### 设计模式与架构
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

### 开发工具与概念
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

## 领域专用术语（保留英文备注）

### MaterialClient 领域
- **License Plate Recognition** (LPR)
- **OCR** (Optical Character Recognition)
- **IP Camera**
- **RTSP** (Real Time Streaming Protocol)
- **ONVIF** (Open Network Video Interface Forum)
- **SDK** (Software Development Kit)

### 称重与测量
- **Scale** / **Balance**
- **Sensor**
- **ADC** (Analog-to-Digital Converter)
- **Modbus**
- **RS-232**
- **RS-485**
- **TCP/IP**

## 翻译格式指南

### 格式 1：仅英文术语（适用于极常用术语）
适用于在中文技术语境中广泛认可的极常用技术术语：

```
API
HTTP
JSON
```

### 格式 2：中文译名 + 英文（适用于较不常见术语）
适用于可能需要说明的技术术语：

```
应用程序接口 (API)
超文本传输协议 (HTTP)
JavaScript 对象表示法 (JSON)
```

### 格式 3：英文术语 + 中文译名（适用于解释）
在文档中解释术语时：

```
API (Application Programming Interface) - 应用程序接口
HTTP (HyperText Transfer Protocol) - 超文本传输协议
JSON (JavaScript Object Notation) - JavaScript 对象表示法
```

## 按使用频率的术语分类

### 第一类：通用术语（仅保留英文）
这些术语普遍认可，不应翻译：

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

### 第二类：常见技术术语（中文 + 英文备注）
这些术语较常见，但附中文译名更清晰：

```
应用程序接口 (API)
超文本传输协议 (HTTP)
数据库管理系统 (DBMS)
对象关系映射 (ORM)
依赖注入 (DI)
控制反转 (IoC)
```

### 第三类：领域专用术语（中文 + 英文备注）
这些术语为 MaterialClient 领域专用：

```
车牌识别 (LPR)
光学字符识别 (OCR)
网络视频接口 (ONVIF)
软件开发工具包 (SDK)
实时流传输协议 (RTSP)
```

## 代码注释指南

### 在代码注释中
翻译代码注释时，下列内容保留英文术语：

```csharp
// 使用 HTTP API 获取车牌识别数据
// 处理 JSON 响应并解析字段
// 执行 SQL 查询以获取称重记录
// 实现依赖注入模式
// 应用 MVVM 架构
```

### 在文档中
翻译文档时遵循以下规则：

```markdown
## API 接口设计

本文档描述了 MaterialClient 使用的 HTTP API 接口规范。

### REST 端点

所有 API 端点遵循 REST 架构风格，使用 JSON 格式进行数据交换。
```

## 按类别的具体示例

### 编程术语
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

### UI 框架术语
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

### 数据访问术语
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

### 构建与部署术语
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

## 一致性规则

1. **首次出现**：技术术语在文档中首次出现时，同时给出中文与英文
2. **后续使用**：首次出现后，沿用已确定的格式
3. **跨文档**：在项目所有文档中保持一致
4. **词汇表参考**：标准术语始终参照翻译词汇表

## 上下文示例

### 文档示例
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

### 代码注释示例
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

## 审校清单

在定稿任何翻译前，请确认：

- [ ] 第一类术语（API、HTTP 等）仅保留英文
- [ ] 第二、三类术语同时包含中文与英文
- [ ] 代码注释对技术概念保留英文术语
- [ ] 术语首次出现时包含完整译名
- [ ] 全文档格式一致
- [ ] 未随意翻译标准技术术语
- [ ] 格式符合既定指南

## 结论

为技术与专业术语保留英文备注可确保：
- 技术沟通清晰准确
- 与行业标准一致
- 熟悉英文术语的开发者易于理解
- 技术内容呈现专业

MaterialClient 项目中的所有翻译应一致遵循本指南。
