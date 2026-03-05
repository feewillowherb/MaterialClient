# 安装 github.com/modelcontextprotocol/csharp-sdk Stdio MCP Server 提案

**日期**: 2026-03-05  
**状态**: 待评审

---

## 一、目标与角色

- **目标**：在物料客户端（桌面应用）中集成 [Model Context Protocol (MCP) C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)，以 **MCP Server** 身份向 Cursor 等客户端暴露一组 **Tools**，便于在对话/调试流程中读取当前窗口、控件树、抓取属性、导出 XAML/Binding 错误、截屏等。
- **角色约定**：
  - **本应用 = MCP Server**：提供 tools（如「读取当前窗口/控件树」「抓取某个控件的属性」「导出 XAML/Binding 错误」「截屏」等）。
  - **Cursor = MCP Client**：连接本应用的 MCP server，在对话中调用这些 tools。
- **约束**：需与现有 **ABP + Autofac** 的 DI 架构兼容，工具实现通过 ABP 容器解析依赖（如窗口/UI 访问服务）。

---

## 二、传输方式选择

MCP 支持多种传输方式，与「Cursor 连到本地桌面应用」最相关的有两种：

| 方式 | 说明 | Cursor 侧配置 | 本应用侧 |
|------|------|----------------|----------|
| **stdio** | Cursor 启动子进程，通过标准输入/输出通信 | 配置为执行 `MaterialClient.exe --mcp-stdio` | 以 `--mcp-stdio` 启动时仅运行 MCP 服务，不显示 UI；需通过 IPC 与已运行的 UI 进程通信以获取窗口/控件信息 |
| **Streamable HTTP** | 本应用在现有 Web Host 上暴露 MCP 端点，Cursor 通过 HTTP 连接 | 配置 MCP Server URL，如 `http://localhost:9960/mcp` | 在现有 `MinimalWebHostService` 中增加 MCP 端点，与 UI 同进程，直接访问窗口/控件 |

**建议**：优先采用 **Streamable HTTP**，理由如下：

1. 本应用已有 **MinimalWebHostService**（Kestrel），与 UI 共享同一进程和 ABP ServiceProvider，无需再起子进程或做 stdio/IPC 桥接。
2. 工具实现可直接依赖 ABP/Autofac 中已注册的服务（如主窗口、UI 线程调度、诊断服务），无需跨进程。
3. Cursor 支持配置 HTTP 类型的 MCP Server，只需在设置中填写本应用提供的 MCP URL 即可。

若后续确有「仅命令行、无 UI 进程」的 stdio 场景，可再增加「stdio 启动模式 + 与 UI 进程 IPC」的二期方案；本提案以 **HTTP 集成** 为主，并在第四节说明与 ABP/Autofac 的集成方式（同样适用于未来 stdio + IPC 的 MCP 宿主进程）。

---

## 三、SDK 安装与包选择

- **仓库与文档**：[modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)，[C# SDK 概念文档](https://modelcontextprotocol.github.io/csharp-sdk/concepts/index.html)。
- **官方 NuGet 包**（[NuGet 上的 ModelContextProtocol](https://www.nuget.org/profiles/ModelContextProtocol)）：
  - **ModelContextProtocol.Core**：仅需低阶 API、最小依赖时使用。
  - **ModelContextProtocol**：非 HTTP 场景的宿主与 DI 扩展（含 stdio server）。
  - **ModelContextProtocol.AspNetCore**：HTTP 场景的 MCP 服务端（Streamable HTTP + 可选 SSE 兼容）。

**本方案选用**：**ModelContextProtocol.AspNetCore**（与现有 ASP.NET Core 宿主一致）。

安装示例（在 `MaterialClient` 工程目录）：

```bash
dotnet add package ModelContextProtocol.AspNetCore
```

版本以当时稳定版为准（如 1.x）；可先不锁小版本，便于跟随 SDK 更新。

---

## 四、与 ABP / Autofac 的集成要点

- 本应用使用 **Volo.Abp.Autofac**，根容器由 ABP 创建；**MinimalWebHostService** 内使用 `WebApplication.CreateBuilder()` 自建一套 `IServiceCollection` / `ServiceProvider`，并通过 `builder.Services.AddSingleton(_sharedServiceProvider)` 注入「共享的 ABP 容器」。
- MCP 的 **Tools** 在请求处理时由 MCP 中间件从 **WebApplication 的 ServiceProvider** 解析；要让工具内部使用 ABP 注册的服务（如主窗口、设置、诊断），有两种常见做法：

### 4.1 推荐：工具类在 ABP 中注册，Web 层仅做转发

1. **在 ABP 模块中注册 MCP 工具类**（如 `MaterialClientMcpTools`），使其通过构造函数注入所需服务（如 `MainWindow`、自定义的 `IWindowInspector`、`IDiagnosticService` 等）。
2. **在 MinimalWebHostService 中**，在 `builder.Build()` 之前：
   - 调用 `builder.Services.AddMcpServer().WithHttpTransport().WithTools<MaterialClientMcpTools>()`（或等价扩展）。
   - 将工具类从「共享 ABP 容器」转发到 Web 的 DI，使 MCP 解析到的工具实例即 ABP 容器中的实例，例如：

     ```csharp
     builder.Services.AddSingleton<MaterialClientMcpTools>(
         _ => _sharedServiceProvider.GetRequiredService<MaterialClientMcpTools>());
     ```

这样，工具类及其依赖（窗口、UI 访问、配置等）全部由 **Autofac/ABP** 解析，仅「谁提供 Tool 实例」这一层在 Web 宿主中绑定到共享容器。

### 4.2 可选：工具类仅写在主工程，依赖由「桥接」从 ABP 解析

- 工具类不直接注册到 Autofac，而是写在主工程，依赖抽象（如 `ICurrentWindowProvider`）。
- 在 ABP 模块中注册 `ICurrentWindowProvider` 等实现；在 Web 宿主中仅注册「从 _sharedServiceProvider 解析」的桥接，例如：

  ```csharp
  builder.Services.AddSingleton<ICurrentWindowProvider>(
      _ => _sharedServiceProvider.GetRequiredService<ICurrentWindowProvider>());
  builder.Services.AddSingleton<MaterialClientMcpTools>();
  ```

这样 MCP 工具仍由 Web 的 ServiceProvider 创建，但其依赖来自 ABP。两种方式二选一即可，推荐 4.1 以保持「工具即应用服务」的清晰边界。

### 4.3 避免重复注册与生命周期

- 不要在 ABP 和 Web 的 `IServiceCollection` 中重复注册同一工具类的**不同实现**，以免出现两个实例、状态不一致。
- 工具若为有状态或依赖单例窗口，应以 **Singleton** 从 ABP 解析并 above 方式转发到 Web；避免在 Web 层对同一工具类型再 `AddTransient` 导致每次请求新实例、无法访问「当前主窗口」等单例资源。

---

## 五、本应用侧实现要点

### 5.1 在 MinimalWebHostService 中挂载 MCP

- 在 `WebApplication` 构建时（`builder.Build()` 之前）：
  - 添加 `ModelContextProtocol.AspNetCore` 的 `AddMcpServer()`、`WithHttpTransport()`。
  - 使用 `WithTools<T>()` 注册工具类（T 已在 ABP 中注册并通过 4.1 方式转发）。
- 在 `ConfigureEndpoints`（或等效处）中增加：

  ```csharp
  app.MapMcp();  // 或 app.MapMcp("/mcp"); 若需固定路径
  ```

- MCP 端点与现有 LPR/API 路由可共存；若需固定路径便于 Cursor 配置，建议使用 `MapMcp("/mcp")`，则 Cursor 中填写的 URL 为 `http://localhost:9960/mcp`（端口与 `SystemSettings.Urls` 一致）。

### 5.2 建议提供的 Tools（示例）

| Tool 名称（示例） | 说明 |
|-------------------|------|
| 读取当前窗口/控件树 | 返回当前主窗口（或指定窗口）的视觉树/逻辑树结构（文本或 JSON），便于 Cursor 理解界面结构。 |
| 抓取某个控件的属性 | 根据控件名/路径/选择器，返回该控件的关键属性（Name、Type、Bounds、DataContext 等）。 |
| 导出 XAML/Binding 错误 | 扫描当前视图或指定窗口的 Binding 错误、验证错误，返回可读报告（文本或结构化数据）。 |
| 截屏 | 对当前窗口或主窗口做截屏，返回图片（如 base64 或 MCP 规定的 Image 内容块）。 |

实现时需注意 **UI 线程**：所有访问 Avalonia/WPF 控件的逻辑应在 UI 线程执行，可在 ABP 中注册一个 `IDispatcher`/`IUIThreadScheduler`，在工具内统一派发到 UI 线程再读取树/属性/截图。

### 5.3 工具类与 SDK 的对接方式

- 使用 C# SDK 推荐的 **属性标注** 方式定义工具（[Tools 概念文档](https://modelcontextprotocol.github.io/csharp-sdk/concepts/tools/tools.html)）：
  - 类上 `[McpServerToolType]`，方法上 `[McpServerTool, Description("...")]`。
  - 参数可用 `[Description("...")]` 便于生成 JSON Schema，供 Cursor/LLM 理解。
- 工具方法可注入 `McpServer`、`IProgress<T>`、`CancellationToken` 以及通过 DI 解析的任意服务（由 ABP 提供则通过 4.1 保证同一实例）。
- 返回值可为 `string`、`TextContentBlock`、`ImageContentBlock`（如截屏）、`IEnumerable<ContentBlock>` 等，按 SDK 文档处理即可。

---

## 六、Cursor 侧配置（Streamable HTTP）

- 在 Cursor 的 MCP 配置（如「Settings → MCP」或项目级 mcp 配置）中新增一条 **HTTP** Server：
  - **URL**：本应用启动后提供的 MCP 地址，例如 `http://localhost:9960/mcp`（与 `MapMcp("/mcp")` 及 `SystemSettings.Urls` 一致）。
- 无需配置「命令行 + stdio」；Cursor 会通过 HTTP 连接并发现/调用 tools。
- 使用前需先启动物料客户端并确保 Web Host 已启动（非海康 LPR 模式下的默认行为）。

---

## 七、Stdio 方案（可选二期）

若未来需要 **stdio** 方式（例如 Cursor 仅支持通过「执行本地命令」连接）：

1. **进程模型**：Cursor 执行 `MaterialClient.exe --mcp-stdio` 启动的进程**不显示 UI**，仅运行 MCP 服务（使用 `ModelContextProtocol` 主包 + `StdioServerTransport`），通过 stdin/stdout 与 Cursor 通信。
2. **与 UI 的联动**：该 stdio 进程**无法直接访问本机已打开的 UI 窗口**，需通过 **IPC**（如命名管道、gRPC、或本地 HTTP）与「已运行的 MaterialClient UI 进程」通信：stdio 进程将 tool 调用转发给 UI 进程，UI 进程在 ABP 容器中执行实际逻辑（读控件树、截屏等）并返回结果。
3. **ABP/Autofac**：stdio 进程若仍希望复用部分业务逻辑，可：
   - 仅作为「薄代理」，不引入完整 ABP，仅做 JSON-RPC 转发；或
   - 在 stdio 进程中启动一个轻量 ABP 宿主（不启动 UI），仅注册 MCP + IPC 客户端，通过 IPC 调用 UI 进程提供的内部 API。  
   Autofac 的集成点在 **UI 进程** 侧不变（工具实现与依赖仍在 ABP 中）；stdio 进程的 DI 可最小化。

本提案不展开 stdio 的详细实现，仅保留为扩展方向。

---

## 八、实施步骤小结

1. **安装包**：在 `MaterialClient` 项目中 `dotnet add package ModelContextProtocol.AspNetCore`。
2. **ABP 模块**：在 `MaterialClientModule`（或单独功能模块）中注册 MCP 工具类及所需依赖（如 `IWindowInspector`、主窗口、UI 调度器），保证其由 Autofac 解析。
3. **Web 宿主**：在 `MinimalWebHostService` 的 `WebApplication` 构建流程中：
   - 调用 `AddMcpServer().WithHttpTransport().WithTools<MaterialClientMcpTools>()`；
   - 将 `MaterialClientMcpTools` 以 Singleton 形式从 `_sharedServiceProvider` 转发到 `builder.Services`；
   - 在端点配置中调用 `app.MapMcp("/mcp")`。
4. **实现 Tools**：在工具类中实现「当前窗口/控件树」「控件属性」「XAML/Binding 错误导出」「截屏」等，所有 UI 访问经 UI 线程调度，依赖从 ABP 注入。
5. **配置 Cursor**：在 Cursor 的 MCP 设置中添加 URL `http://localhost:9960/mcp`（或实际使用的端口与路径）。
6. **验证**：启动应用后，在 Cursor 中确认 MCP 连接成功并能看到上述 tools，并做一次调用验证（如读取控件树或截屏）。

---

## 九、参考链接

- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP C# SDK 概览](https://modelcontextprotocol.github.io/csharp-sdk/)
- [Transports（stdio / Streamable HTTP）](https://modelcontextprotocol.github.io/csharp-sdk/concepts/transports/transports.html)
- [Tools（定义与消费）](https://modelcontextprotocol.github.io/csharp-sdk/concepts/tools/tools.html)
- [StdioServerTransport API](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.StdioServerTransport.html)
- [MCP 协议规范](https://modelcontextprotocol.io/specification/)
