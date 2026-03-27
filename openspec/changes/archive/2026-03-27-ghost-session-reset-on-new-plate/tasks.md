## 1. GateIoSession 数据模型变更

- [x] 1.1 在 `GateIoSession` 中新增 `PlateNumber` 属性（`string`，默认空字符串）
- [x] 1.2 在 `Reset()` 方法中添加 `PlateNumber = string.Empty` 清理逻辑
- [x] 1.3 在 `GetStatus()` 方法中输出中包含 `Plate={PlateNumber}`

## 2. 幽灵会话检测方法

- [x] 2.1 新增私有方法 `TryResetGhostSession(string newPlateNumber, string newDeviceName)`，返回 `bool` 表示是否重置了幽灵会话
- [x] 2.2 实现幽灵会话判断逻辑：会话激活 + 车牌不同(忽略大小写) + 出口未开 + 称重状态为离秤 → 重置并记录警告日志
- [x] 2.3 实现同一车牌跳过逻辑：会话激活 + 车牌相同 → 记录调试日志，返回 `false`（不重置）
- [x] 2.4 实现正在称重拒绝逻辑：不满足幽灵会话条件 → 记录信息日志，返回 `false`（不重置）

## 3. HandlePlateRecognizedAsync 集成

- [x] 3.1 将 `HandlePlateRecognizedAsync` 中会话激活时的"直接拒绝"逻辑替换为调用 `TryResetGhostSession`
- [x] 3.2 在创建新会话时设置 `_session.PlateNumber = message.PlateNumber`
- [x] 3.3 确认替换后 `HandlePlateRecognizedAsync` 的整体职责清晰、无膨胀

## 4. 验证

- [x] 4.1 编译通过，无错误
- [x] 4.2 检查日志输出格式符合 spec 要求（警告日志包含旧车牌、旧入口侧、旧时长、新车牌、新设备名）
