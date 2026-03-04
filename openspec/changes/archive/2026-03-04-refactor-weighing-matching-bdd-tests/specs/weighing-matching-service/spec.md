# weighing-matching-service 规范增量

**变更 ID**：`refactor-weighing-matching-bdd-tests`
**能力**：`weighing-matching-service`
**操作**：MODIFIED

---

## MODIFIED 需求

### 需求：WeighingMatchingService BDD 测试结构

WeighingMatchingService 的 BDD 测试应遵循 StoreSample 测试模式，以保持一致性与可维护性。

#### 场景：用于测试依赖的 TestManager 模式
- **给定** 针对 WeighingMatchingService 的 BDD 测试
- **当** 在步骤定义中访问仓储或服务
- **则** 测试应使用通过依赖注入集中提供访问的 `TestManager` 类
- **且** TestManager 应在测试模块中注册为作用域服务
- **且** 步骤定义应通过 `GetRequiredService<TestManager>()` 访问 TestManager

#### 场景：基于表的数据准备
- **给定** 需要创建测试数据的 BDD 场景
- **当** 准备称重记录或运单
- **则** 测试应使用带 DTO 的基于表的数据准备
- **且** Feature 文件应使用 Reqnroll 表语法作为数据输入
- **且** 步骤定义应使用 `table.CreateSet<DtoType>()` 将表解析为 DTO 对象

#### 场景：简化的步骤定义
- **给定** BDD 测试步骤定义
- **当** 步骤需要访问仓储或服务
- **则** 步骤应使用 TestManager 而非直接访问仓储
- **且** 步骤应避免冗长参数解析，优先采用基于表或 DTO 的方式
- **且** 公共模式应合并到可复用步骤中

#### 场景：Feature 文件一致性
- **给定** WeighingMatchingService 的 BDD Feature 文件
- **当** 定义测试场景
- **则** Feature 文件应使用基于表的数据准备
- **且** Feature 文件应统一使用英文
- **且** Feature 文件应遵循 StoreSample 的表格式模式

---

## 说明

- 本变更仅重构测试结构，不改变被测试的实际场景或业务逻辑
- TestManager 模式通过集中测试依赖提升可维护性
- 基于表的数据准备提升可读性并便于新增用例
- 重构后所有现有测试场景必须保持等价
