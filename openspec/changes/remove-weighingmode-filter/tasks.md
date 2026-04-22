## 1. 查询逻辑调整

- [x] 1.1 定位 `Provider`、`Material`、`MaterialUnit` 的数据查询入口（API 接口与服务实现），确认当前 `WeighingMode` 过滤位置。
- [x] 1.2 移除三类实体查询中的 `WeighingMode` 过滤条件，保持其它既有过滤条件不变。
- [x] 1.3 检查并清理调用链中的重复二次过滤，确保不会在上层再次按 `WeighingMode` 排除数据。

## 2. 兼容性与验证

- [x] 2.1 验证 Provider/Material/MaterialUnit 的列表加载与搜索在移除过滤后行为一致且无异常。
- [ ] 2.2 补充或更新相关单元测试/集成测试，覆盖“不按 WeighingMode 过滤”的场景。
- [x] 2.3 复核人工称重相关 UI 选择流程，确认结果集变化符合预期并记录回归检查项。
