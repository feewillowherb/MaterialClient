# Agent 行为准则

## OpenSpec 工作流（可选）

若采用规范驱动流程，可参考：

- **探索** (`/opsx:explore`)：理解问题与上下文
- **提案** (`/opsx:propose`)：产出 proposal、design、tasks、spec delta
- **实施** (`/opsx:apply`)：按 Spec/Design 实施代码
- **归档** (`/opsx:archive`)：变更完成后归档

```
openspec/
├── specs/          ← 功能规范
├── changes/        ← 待实施变更的规范化定义
└── project.md      ← 项目约定和技术栈
```

- 查看活跃变更：`openspec list`
- 查看功能规范：`openspec list --specs`
- 验证变更：`openspec validate [change-id] --strict`

## 项目约定（服务注册）

- **服务注册**：优先采用 **ABP 集成式 + 隐式 + AutoConstructor**。服务实现类实现 ABP 依赖接口（如 `ITransientDependency`、`ISingletonDependency`）并标注 `[AutoConstructor]`，由 ABP 按约定扫描注册，无需在 Module 或扩展方法中显式注册。参考实现：`SoundDeviceService`（实现 `ISoundDeviceService, ISingletonDependency` + `[AutoConstructor]`）。仅在确有“集成一组服务”的跨模块需求时再考虑扩展方法集中注册。
