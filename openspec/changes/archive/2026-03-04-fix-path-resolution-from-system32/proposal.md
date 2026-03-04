# 变更：从 System32 启动时修复路径解析

## 原因

当 MaterialClient 通过 Windows 任务计划程序或注册表自启（例如从 `C:\Windows\System32\`）启动时，应用无法访问关键资源：

1. **数据库访问失败**：SQLite 无法打开 `MaterialClient.db`，因为相对路径被解析为 `C:\Windows\System32\MaterialClient.db`，而非应用目录
2. **附件访问失败**：存放在 `PhotoPiaoJu/` 与 `PhotoJianKong/` 目录下的照片附件无法找到，因为相对路径被解析到 System32

**错误证据**（来自日志）：
```
SQLite Error 14: 'unable to open database file'
Server: 'MaterialClient.db'
```

导致核心功能不可用：
- 启动时数据库迁移失败
- 设置服务无法初始化
- 车辆照片无法保存或加载
- 磅单照片无法访问
- 若传入相对路径，票单 PDF 可能生成到 System32 目录

## 变更内容

- **修复数据库连接字符串解析**：在 `MaterialClientCommonModule.cs` 中使用现有 `DatabaseConnectionStringFactory.FixConnectionString()`
- **修复附件路径解析**：扩展 `AttachmentPathUtils`，基于 `AppContext.BaseDirectory` 返回绝对路径
- **修复票单打印输出路径**：在 `TicketPrintingService` 入口（`PrintToPdf`、`PrintImageToPdf`、`RenderTicketToImage`）增加路径解析
- 确保所有文件系统路径均相对于应用程序可执行文件目录解析，而非当前工作目录
- 不修改全局工作目录（保持其他操作的现有行为）

## 影响

### 涉及规格
- `attended-weighing`：拍照与存储行为

### 涉及代码
- `MaterialClient.Common/MaterialClientCommonModule.cs`：数据库配置
- `MaterialClient.Common/Utils/AttachmentPathUtils.cs`：照片路径解析
- `MaterialClient.Common/Utils/DatabaseConnectionStringFactory.cs`：已存在，将被使用
- `MaterialClient.Common/Services/Hardware/TicketPrintingService.cs`：票单 PDF/图片输出路径解析
- 所有保存/加载附件的代码（无需改代码，由工具类统一处理）

### 间接受益的组件
修复后以下组件将自动正确工作，无需改代码：
- `MaterialClient.Common/Services/Hikvision/HikvisionService.cs`：摄像头拍照（从 `AttachmentPathUtils` 接收绝对路径）
- `MaterialClient.Common/Services/AttendedWeighingService.cs`：照片附件创建（使用 `AttachmentPathUtils`）
- `MaterialClient/ViewModels/AttendedWeighingViewModel.cs`：磅单拍照（使用 `AttachmentPathUtils`）
- `MaterialClient.Common/Services/AttachmentService.cs`：照片加载与 OSS 上传（从数据库路径读取）
- 所有展示照片的 ViewModel（从数据库读取路径）

### 破坏性变更
无——此为恢复预期行为的缺陷修复。

### 迁移
自动——下次应用启动后路径将正确解析。
