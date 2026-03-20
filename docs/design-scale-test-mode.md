# 地磅测试模式设计文档

## 一、需求背景

在系统设置的地磅类型选择中，新增一个 **"测试模式"** 类型。当选择测试模式时：
1. 不需要实际的地磅硬件设备（无需串口连接）
2. 通过 MinimalWebHostService 提供的 HTTP POST 接口来模拟设置重量
3. 方便在没有物理地磅设备的环境下进行系统测试和演示

## 二、涉及文件

| 文件 | 修改内容 |
|------|---------|
| `MaterialClient.Common/Entities/Enums/ScaleType.cs` | 新增 `TestMode` 枚举值 |
| `MaterialClient.Common/Services/Hardware/TruckScaleWeightService.cs` | 处理测试模式的初始化逻辑 |
| `MaterialClient.Common/Services/Hardware/ScaleTestWeightPreprocessorService.cs` | 新增测试数据预处理（队列+200ms smoothstep 平滑过渡+稳定保持） |
| `MaterialClient/Services/MinimalWebHostService.cs` | 新增设置重量的 POST API 端点 |
| `MaterialClient/Converters/ScaleTypeConverter.cs` | 添加"测试模式"显示文本 |
| `MaterialClient/ViewModels/SettingsWindowViewModel.cs` | ScaleTypeOptions 添加 TestMode 选项 |

## 三、设计要点

### 3.1 ScaleType 枚举

新增 `TestMode = 2` 枚举值，Description 为"测试模式"。

### 3.2 TruckScaleWeightService 修改

**InitializeAsync 方法**：
- 当 `ScaleType == TestMode` 时，跳过串口初始化，直接返回 `true`
- 记录日志表明测试模式已启用

**IsOnline 属性**：
- 测试模式下始终返回 `true`（模拟设备在线）

**SetWeight 方法**：
- 已存在，无需修改
- 该方法已能将重量推送到 Rx 流，供订阅者接收

### 3.3 MinimalWebHostService 修改

**新增 API 端点**：`POST /api/scale/weight`

| 项目 | 说明 |
|------|------|
| 路径 | `/api/scale/weight` |
| 方法 | POST |
| 请求体 | `{ "weight": 15.5 }` （单位：吨）|
| 成功响应 | `{ "success": true, "message": "...", "weight": 15.5 }` |
| 失败响应 | `{ "success": false, "message": "..." }` |

**处理逻辑**：
1. 验证重量值为非负数
2. 获取当前设置，检查是否为测试模式
3. 非测试模式返回错误
4. 测试模式下调用 `IScaleTestWeightPreprocessorService.Enqueue()` 入队目标重量，由中间层在每 200ms 推送 smoothstep 平滑过渡值（并在到达 B 后保持稳定窗口）
5. 记录操作日志

### 3.4 根路由更新

在 `/` 根路由的端点列表中添加新的 `/api/scale/weight` 端点。

## 四、调用流程

```
外部测试工具 (Postman/curl)
         │
         │  POST /api/scale/weight
         ▼
 MinimalWebHostService
         │
         │  获取 IScaleTestWeightPreprocessorService
         ▼
ScaleTestWeightPreprocessorService（200ms tick）
         │
         │  计算 smoothstep(A->B) 并多次调用 TruckScaleWeightService.SetWeight()
         │
         │  Rx Stream (WeightUpdates)
         ▼
 AttendedWeighingService (订阅者)
```

## 五、安全考虑

| 考虑点 | 说明 |
|--------|------|
| 模式检查 | API 仅在测试模式下可用，非测试模式返回 400 错误 |
| 参数验证 | 重量值必须为非负数 |
| 日志记录 | 所有重量设置操作都记录日志 |
| 服务可用性 | 地磅服务不可用时返回 500 错误 |

## 六、测试要点

| 测试场景 | 预期结果 |
|----------|----------|
| 切换到测试模式 | 串口不被初始化，`IsOnline` 返回 `true` |
| API 设置重量 | POST 请求成功，后续会按 200ms 多次推送重量更新：先 smoothstep 平滑过渡到 B，再保持稳定窗口 |
| Rx 流更新 | AttendedWeighingService 收到重量更新通知 |
| 非测试模式调用 API | 返回错误，提示"当前不是测试模式" |
| 服务重启 | 重量重置为 0 |

## 七、实施任务清单

- [ ] ScaleType 枚举添加 `TestMode`
- [ ] ScaleTypeConverter 添加"测试模式"显示文本
- [ ] SettingsWindowViewModel.ScaleTypeOptions 添加 `TestMode`
- [ ] TruckScaleWeightService.InitializeAsync 处理测试模式
- [ ] TruckScaleWeightService.IsOnline 处理测试模式
- [ ] MinimalWebHostService 添加 `/api/scale/weight` 端点
- [ ] 更新根路由端点列表
- [ ] 新增 `ScaleTestWeightPreprocessorService`（队列+200ms tick+smoothstep+稳定保持）
