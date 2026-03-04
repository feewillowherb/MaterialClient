# 规格增量：attended-weighing

## 新增需求

### 需求：系统必须将文件路径以相对路径存储以实现数据库可移植性

系统应在数据库中使用相对路径（相对于应用基目录）存储文件路径，以便在不同服务器或目录间迁移数据库时不破坏文件引用。

**背景**：当数据库文件迁移到新服务器或目录（例如从 `D:\MaterialClient\` 到 `E:\Apps\MaterialClient\`）时，绝对路径如 `D:\MaterialClient\Photos\car.jpg` 会失效。相对路径如 `Photos/car.jpg` 在迁移后仍然有效。

**实现约束**：适用于数据库中存储的所有 `AttachmentFile.LocalPath` 值。

#### 场景：照片路径以相对路径存储

**给定** 应用从 `D:\MaterialClient\` 运行  
**当** 拍摄并保存照片到 `D:\MaterialClient\Photos\2026\01\23\bill.jpg`  
**则** 数据库 `AttachmentFile.LocalPath` 必须存储 `"Photos/2026/01/23/bill.jpg"`（相对路径）  
**且** 不得存储 `"D:\MaterialClient\Photos\2026\01\23\bill.jpg"`（绝对路径）

#### 场景：数据库迁移到新位置

**给定** 数据库包含 `LocalPath = "Photos/2026/01/23/car.jpg"` 的 `AttachmentFile`  
**且** 数据库文件从 `D:\MaterialClient\` 复制到 `E:\NewLocation\`  
**且** `Photos` 文件夹也复制到 `E:\NewLocation\Photos\`  
**当** 应用从 `E:\NewLocation\` 运行  
**则** `E:\NewLocation\Photos\2026/01/23/car.jpg` 处的照片必须能成功加载  
**且** 无需在数据库中更新路径

#### 场景：已有绝对路径仍可正常使用

**给定** 数据库包含旧版本的 `AttachmentFile`，其 `LocalPath = "D:\MaterialClient\Photos\car.jpg"`（绝对路径）  
**当** 应用从 `D:\MaterialClient\` 运行  
**则** 照片必须仍能成功加载  
**且** 之后新拍摄的照片必须使用相对路径

---

### 需求：图片转换器必须在加载前规范化路径

图片转换器应在进行文件存在性检查和图片加载操作之前，将相对路径规范化为绝对路径，确保无论应用启动时的工作目录如何，图片都能正确渲染。

**背景**：当应用从 `C:\Windows\System32` 启动（例如通过任务计划程序）时，数据库中取出的相对路径若未做规范化，会错误解析为 `C:\Windows\System32\Photos\...`。

**实现约束**：适用于整个 UI 中使用的 `CarNullOrEmptyImageConverter` 与 `NullOrEmptyImageConverter`。

#### 场景：从 System32 启动时从相对路径加载图片

**给定** 应用从 `C:\Windows\System32\` 启动  
**且** 数据库包含 `LocalPath = "Photos/2026/01/23/car.jpg"` 的 `AttachmentFile`  
**当** UI 使用 `CarNullOrEmptyImageConverter` 尝试显示图片  
**则** 转换器必须将路径规范化为 `{AppContext.BaseDirectory}\Photos\2026\01\23\car.jpg`  
**且** 图片必须成功渲染  
**且** 不得尝试从 `C:\Windows\System32\Photos\2026\01\23\car.jpg` 加载

#### 场景：从绝对路径加载图片（向后兼容）

**给定** 数据库包含旧版 `AttachmentFile`，其 `LocalPath = "D:\MaterialClient\Photos\car.jpg"`（绝对路径）  
**当** UI 尝试显示该图片  
**则** 转换器必须识别路径已是绝对路径  
**且** 直接使用、不做修改  
**且** 图片必须成功渲染

#### 场景：文件缺失时显示默认图片

**给定** 数据库包含 `LocalPath = "Photos/missing.jpg"` 的 `AttachmentFile`  
**且** `{AppContext.BaseDirectory}\Photos\missing.jpg` 处不存在该文件  
**当** UI 尝试显示该图片  
**则** 转换器必须显示默认车辆图片占位图  
**且** 不得抛出异常

#### 场景：资源路径单独处理

**给定** ViewModel 提供资源路径 `"avares://MaterialClient/Assets/Car_Default.png"`  
**当** UI 使用 `CarNullOrEmptyImageConverter` 尝试显示图片  
**则** 转换器必须识别其为资源路径  
**且** 从嵌入式资源加载  
**且** 不进行文件路径规范化

---

### 需求：系统必须提供统一的 PathManager 工具

系统应提供集中的 `PathManager` 工具，包含双向路径转换方法（`ToAbsolutePath`、`ToRelativePath`）及文件操作辅助方法，确保各服务中的路径处理一致。

**背景**：路径转换逻辑此前分散在 `DatabaseConnectionStringFactory`、`AttachmentPathUtils` 及服务代码中。集中该逻辑可减少重复并保证一致性。

**实现约束**：`PathManager` 必须为 `MaterialClient.Common/Utils/` 命名空间下的静态工具类，遵循项目中与配置无关的工具逻辑约定。

#### 场景：为文件操作将相对路径转为绝对路径

**给定** 应用基目录为 `D:\MaterialClient\`  
**当** 调用 `PathManager.ToAbsolutePath("Photos/2026/01/23/car.jpg")`  
**则** 必须返回 `"D:\MaterialClient\Photos\2026\01\23\car.jpg"`  
**且** 路径必须完全规范化（无 `..` 或多余斜杠）

#### 场景：为数据库存储将绝对路径转为相对路径

**给定** 应用基目录为 `D:\MaterialClient\`  
**当** 调用 `PathManager.ToRelativePath("D:\MaterialClient\Photos\2026\01\23\car.jpg")`  
**则** 必须返回 `"Photos\2026\01\23\car.jpg"`

#### 场景：幂等转换（已是绝对路径）

**给定** 绝对路径 `"D:\MaterialClient\Photos\car.jpg"`  
**当** 调用 `PathManager.ToAbsolutePath("D:\MaterialClient\Photos\car.jpg")`  
**则** 必须原样返回输入：`"D:\MaterialClient\Photos\car.jpg"`

#### 场景：幂等转换（已是相对路径）

**给定** 相对路径 `"Photos/car.jpg"`  
**当** 调用 `PathManager.ToRelativePath("Photos/car.jpg")`  
**则** 必须原样返回输入：`"Photos/car.jpg"`

#### 场景：应用目录外的路径保持为绝对路径

**给定** 绝对路径 `"C:\Users\Admin\Desktop\export.pdf"`（在应用目录外）  
**当** 调用 `PathManager.ToRelativePath("C:\Users\Admin\Desktop\export.pdf")`  
**则** 必须原样返回输入（无法转为相对路径）  
**且** 返回 `"C:\Users\Admin\Desktop\export.pdf"`

#### 场景：带路径规范化的文件存在性检查

**给定** 应用基目录为 `D:\MaterialClient\`  
**且** 文件存在于 `D:\MaterialClient\Photos\car.jpg`  
**当** 调用 `PathManager.FileExists("Photos/car.jpg")`  
**则** 必须在内部规范化为绝对路径  
**且** 返回 `true`

#### 场景：带路径规范化的目录创建

**给定** 应用基目录为 `D:\MaterialClient\`  
**当** 调用 `PathManager.EnsureDirectoryExists("Photos/2026/01/23")`  
**则** 必须在 `D:\MaterialClient\Photos\2026\01\23\` 创建目录  
**且** 返回绝对路径 `"D:\MaterialClient\Photos\2026\01\23"`  
**且** 创建所有缺失的父目录

---

## 修改的需求

无。本变更仅新增能力，不修改现有需求。

---

## 移除的需求

无。本变更为纯新增，保持向后兼容。
