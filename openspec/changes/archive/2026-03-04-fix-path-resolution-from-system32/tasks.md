# 实施任务

## 1. 修复数据库连接字符串解析
- [x] 1.1 在 `MaterialClientCommonModule.ConfigureServices()` 中调用 `DatabaseConnectionStringFactory.FixConnectionString()`
- [ ] 1.2 在从 System32 启动时通过日志验证数据库路径解析
- [ ] 1.3 测试数据库迁移是否正常

## 2. 修复附件路径解析
- [x] 2.1 在 `AttachmentPathUtils` 中新增返回绝对路径的 `GetLocalStorageAbsolutePath()` 方法
- [x] 2.2 将 `GetBillPhotoFullPath()` 更新为使用绝对路径
- [x] 2.3 将 `GetMonitoringPhotoFullPath()` 更新为使用绝对路径
- [x] 2.4 确保目录创建使用绝对路径

## 3. 修复票单打印服务路径解析
- [x] 3.1 在 `TicketPrintingService.PrintToPdf()` 入口增加路径规范化
- [x] 3.2 在 `TicketPrintingService.PrintImageToPdf()` 入口增加路径规范化
- [x] 3.3 在 `TicketPrintingService.RenderTicketToImage()` 入口增加路径规范化
- [ ] 3.4 在从 System32 启动时测试相对路径的票单 PDF 生成
- [ ] 3.5 测试绝对路径的票单 PDF 生成（回归）

## 4. 集成测试
- [ ] 4.1 从 System32 目录启动应用进行测试
- [ ] 4.2 验证数据库访问正常
- [ ] 4.3 验证拍照与存储正常（USB 摄像头与磅单照片）
- [ ] 4.4 验证海康摄像头拍照正常
- [ ] 4.5 验证历史记录中的照片加载正常
- [ ] 4.6 验证 OSS 上传服务能找到本地文件
- [ ] 4.7 验证所有 ViewModel 中的照片展示（AttendedWeighing、ManualMatch、PhotoGrid）
- [ ] 4.8 验证票单打印为 PDF 正常（相对路径与绝对路径）
- [ ] 4.9 测试从应用目录正常启动（回归）

## 5. 文档
- [x] 5.1 添加说明路径解析策略的代码注释
- [x] 5.2 记录 `AppContext.BaseDirectory` 使用模式
