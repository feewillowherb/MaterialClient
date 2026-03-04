# 规格增量：有人值守称重 - 路径解析修复

## 修改的需求

### 需求：车辆照片存储
系统应在称重过程中使用基于应用程序可执行文件目录解析的绝对文件路径，采集并存储车辆照片。

**背景**：当应用从任意工作目录（包括通过任务计划程序或注册表自启的 `C:\Windows\System32`）启动时，照片路径必须正确解析到应用的存储目录，而非当前工作目录。

#### 场景：从 System32 启动时的拍照
- **给定** MaterialClient 由任务计划程序以工作目录 `C:\Windows\System32` 启动
- **且** 车辆驶入地磅
- **当** 系统采集车辆照片
- **则** 照片应保存到 `{AppContext.BaseDirectory}\PhotoJianKong\{year}\{MM}\{dd}\{filename}.jpg`
- **且** 数据库应存储照片的绝对路径
- **且** 照片文件应可被查看

#### 场景：从 System32 启动时的磅单拍照
- **给定** MaterialClient 由任务计划程序以工作目录 `C:\Windows\System32` 启动
- **且** 用户手动采集磅单照片
- **当** 系统保存磅单照片
- **则** 照片应保存到 `{AppContext.BaseDirectory}\PhotoPiaoJu\{year}\{MM}\{dd}\bill_{timestamp}.jpg`
- **且** 数据库应存储照片的绝对路径
- **且** 照片文件应可被打印和查看

#### 场景：从 System32 启动时加载历史照片
- **给定** MaterialClient 由任务计划程序以工作目录 `C:\Windows\System32` 启动
- **且** 照片此前已以绝对路径采集并存储
- **当** 用户查看历史称重记录
- **则** 系统应成功加载并展示所有关联照片
- **且** 不得出现照片访问错误

#### 场景：正常启动时的照片存储
- **给定** MaterialClient 从其安装目录正常启动
- **当** 系统采集任意类型照片
- **则** 照片应使用基于 `AppContext.BaseDirectory` 的绝对路径存储
- **且** 行为应与任务计划程序启动场景一致
- **且** 不得出现现有功能回归

## 新增需求

### 需求：从任意工作目录访问数据库
系统应使用基于应用程序可执行文件目录解析的绝对路径访问 SQLite 数据库文件，与进程工作目录无关。

**背景**：通过 Windows 任务计划程序或注册表自启时，应用可能以 `C:\Windows\System32` 为工作目录启动。数据库文件必须从应用目录访问，而非工作目录。

#### 场景：从 System32 启动时的数据库初始化
- **给定** MaterialClient 由任务计划程序以工作目录 `C:\Windows\System32` 启动
- **且** `appsettings.json` 中的连接字符串为 `"Data Source=MaterialClient.db"`
- **当** 应用初始化数据库连接
- **则** 连接字符串应被转换为 `"Data Source={AppContext.BaseDirectory}\MaterialClient.db"`
- **且** 数据库文件应被成功打开
- **且** 数据库迁移应成功完成

#### 场景：使用已有绝对路径的数据库访问
- **给定** `appsettings.json` 中的连接字符串包含绝对路径，如 `"Data Source=C:\CustomPath\MaterialClient.db"`
- **当** 应用初始化数据库连接
- **则** 绝对路径应原样保留
- **且** 数据库文件应在该绝对路径访问
- **且** 不进行路径转换

#### 场景：从 System32 启动时的设置服务初始化
- **给定** MaterialClient 由任务计划程序以工作目录 `C:\Windows\System32` 启动
- **当** 设置服务初始化其缓存
- **则** 数据库应可访问
- **且** 设置应成功加载
- **且** 不得出现 `SQLite Error 14: 'unable to open database file'`
