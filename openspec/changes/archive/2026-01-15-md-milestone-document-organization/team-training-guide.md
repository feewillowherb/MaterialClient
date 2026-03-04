# 团队培训指南：OpenSpec 文档迁移

**培训日期**：待定（与团队协商）
**培训时长**：45–60 分钟
**变更参考**：`md-milestone-document-organization`
**培训师**：技术负责人

---

## 培训目标

培训结束后，团队成员应能够：

1. ✓ 理解文档重组与 OpenSpec 采用
2. ✓ 使用新的文档结构进行导航
3. ✓ 使用 OpenSpec 模板创建新规范
4. ✓ 为修改创建变更提案
5. ✓ 在需要时访问已归档的遗留文档
6. ✓ 遵循文档最佳实践

---

## 培训议程

### 第一部分：概览与动机（10 分钟）

**幻灯片 1：为何要改**

- 旧文档分散在 4 个目录
- 51 份文档有效性不清晰
- 缺乏统一的审阅流程
- 难以找到当前信息

**幻灯片 2：OpenSpec 方案**

- 在 `openspec/` 下的统一结构
- 正式的审阅与批准流程
- 清晰的状态跟踪
- 单一事实来源

**幻灯片 3：具体变化**

```
旧：specs/、ReadOnlyMd/、ReadonlyMd/、docs/（分散）
新：openspec/specs/、openspec/changes/（有序）
已归档：archive/legacy-docs-20260115.tar.gz（保留）
```

**关键数据**：
- 51 份文档已审阅并标注
- 48 份已归档（94%）
- 3 份作为 VALID 迁移
- 通过压缩减少 44% 存储

---

### 第二部分：新文档结构（15 分钟）

**现场演示：浏览 OpenSpec**

```bash
# 展示目录结构
tree openspec/ -L 2

# 浏览关键目录
cd openspec/specs
cd openspec/changes
cd openspec/docs
```

**目录概览**：

1. **`openspec/specs/`** - 能力规范
   - 当前功能与能力
   - 标准化格式
   - 主动维护

2. **`openspec/changes/`** - 变更提案
   - 新功能、缺陷修复、重构
   - 实施前审阅
   - 跟踪至完成

3. **`openspec/docs/`** - 流程文档
   - 指南与最佳实践
   - 含边界指南
   - 团队参考材料

4. **`archive/legacy-docs-20260115.tar.gz`** - 遗留归档
   - 所有历史文档
   - 只读访问
   - 需要时解压

**互动练习**（5 分钟）：

请团队：
1. 进入 `openspec/specs/_template/`
2. 阅读模板结构
3. 进入 `openspec/changes/_template/`
4. 对比两个模板

---

### 第三部分：创建新文档（15 分钟）

**演示 1：创建新规范**

```bash
# 1. 复制模板
cp -r openspec/specs/_template openspec/specs/my-new-feature

# 2. 编辑规范
cd openspec/specs/my-new-feature
# 编辑 spec.md、design.md（可选）、tasks.md

# 3. 删除模板 README
rm _template/README.md

# 4. 提交并创建 PR
git add openspec/specs/my-new-feature
git commit -m "Add spec: My New Feature"
git push
```

**演示 2：创建变更提案**

```bash
# 1. 复制模板
cp -r openspec/changes/_template openspec/changes/fix-my-bug-2025-01-15

# 2. 编辑提案
cd openspec/changes/fix-my-bug-2025-01-15
# 编辑 proposal.md、design.md（可选）、tasks.md

# 3. 删除模板 README
rm _template/README.md

# 4. 提交审阅
git add openspec/changes/fix-my-bug-2025-01-15
git commit -m "Propose fix: My Bug"
git push
```

**最佳实践**：

1. **始终使用模板** - 保证一致性
2. **填写所有必填项** - 规范完整
3. **实施前先审阅** - 尽早发现问题
4. **随进度更新状态** - 让团队知情
5. **已完成变更归档** - 保持结构清晰

---

### 第四部分：访问遗留文档（5 分钟）

**何时访问遗留文档**：

- 了解设计决策的历史背景
- 理解实施历史
- 查阅以往问题与方案
- 更新旧功能时作参考

**如何访问**：

```bash
# 解压整个归档
tar -xzf archive/legacy-docs-20260115.tar.gz

# 解压指定目录
tar -xzf archive/legacy-docs-20260115.tar.gz specs/001-attended-weighing/

# 解压指定文件
tar -xzf archive/legacy-docs-20260115.tar.gz "ReadOnlyMd/状态机设计评估报告.md"
```

**重要**：遗留文档为只读。请勿修改。若有更正，请创建新的 OpenSpec 文档。

---

### 第五部分：问答与讨论（10–15 分钟）

**常见问题**：

**问：若需更新遗留文档怎么办？**
答：不要直接改。创建新的 OpenSpec 文档，并在其中引用遗留文档作为背景。

**问：缺陷修复文档放哪里？**
答：在 `openspec/changes/<change-id>/` 下创建变更提案。

**问：还能在旧目录里写文档吗？**
答：不能。所有新文档必须放在 OpenSpec 结构中。

**问：在归档里找不到某份文档怎么办？**
答：查 Git 历史或联系技术负责人。

**问：是否要把所有遗留文档都转成 OpenSpec 格式？**
答：不需要。遗留文档按原样归档。仅在需要时创建新的 OpenSpec 文档。

---

## 培训材料

###  handout：快速参考卡

分发来自 `openspec/docs/documentation-boundary-guidelines.md` 的**决策树**：

```
我要记录一些内容
├─ 新功能？ → openspec/specs/<feature>/
├─ 变更/缺陷修复？ → openspec/changes/<id>/
├─ 流程文档？ → openspec/docs/<topic>.md
└─ 更新遗留？ → 不要——创建新的 OpenSpec 文档
```

### 演示幻灯片

幻灯片应包含：
- 变更概览
- 前后对比
- 新目录结构
- 工作流示意
- 示例

### 练习手册

提供动手练习：
1. 创建一份简单规范
2. 创建一份变更提案
3. 从归档中解压一份文档
4. 在 OpenSpec 结构中定位指定文档

---

## 培训后跟进

### 第一周内

1. **团队沟通**
   - 发送通知邮件（见下方模板）
   - 在团队聊天中发布摘要
   - 在项目 README 中加入边界指南链接

2. **监督**
   - 确认新文档进入 OpenSpec 结构
   - 及时回答问题
   - 收集对流程的反馈

3. **支持**
   - 安排答疑时间
   - 前几份规范可结对编写
   - 一起审阅首批变更提案

### 第一个月内

1. **每周同步**
   - 新结构使用中是否有问题？
   - 流程是否需要澄清？
   - 有无改进建议？

2. **质量保证**
   - 检查新规范是否符合模板
   - 确保变更提案遵循流程
   - 确认已完成变更正确归档

3. **流程优化**
   - 收集团队反馈
   - 根据使用情况更新模板
   - 按需完善文档

---

## 通知邮件模板

**主题**：🔔 重要：文档重组 - OpenSpec 采用

**各位**，

我们已完成项目文档的重大重组，以提升清晰度与可维护性。请知悉以下内容：

### 变更概要

✅ **文档新归属**：所有新文档现统一放在 `openspec/` 目录
✅ **统一流程**：标准化模板与审阅工作流
✅ **结构更清晰**：遗留文档已归档，当前信息更易查找

### 您需要做的

1. **📚 阅读指南**：`openspec/docs/documentation-boundary-guidelines.md`
2. **🎯 使用模板**：始终从 `_template/` 目录中的模板开始
3. **❓ 有疑问**：需要帮助使用新结构时请随时联系

### 培训安排

**时间**：[日期与时间]
**地点**：[地点/链接]
**时长**：45–60 分钟

内容包含：
- 新文档结构
- 如何创建规范与变更提案
- 如何访问已归档遗留文档
- 问答

### 快速参考

| 需求 | 位置 |
|------|------|
| 新功能规范 | `openspec/specs/<feature>/spec.md` |
| 缺陷修复/变更 | `openspec/changes/<id>/proposal.md` |
| 流程文档 | `openspec/docs/<topic>.md` |
| 遗留文档 | 从 `archive/legacy-docs-20260115.tar.gz` 解压 |

**请勿**在旧目录（`specs/`、`ReadOnlyMd/`、`docs/`）中创建新文档。

### 资源

- 📖 边界指南：`openspec/docs/documentation-boundary-guidelines.md`
- 📋 模板：`openspec/specs/_template/` 与 `openspec/changes/_template/`
- 🗂️ 归档清单：`archive/LEGACY_DOCS_MANIFEST.md`

有问题请联系 [技术负责人] 或参加培训。

谢谢，
[您的姓名]

---

## 评估与反馈

### 培训评估表

培训后收集反馈：

1. **讲解清晰度**（1–5）：___
2. **动手练习是否有用？**（是/否）：___
3. **使用 OpenSpec 的信心**（1–5）：___
4. **最有帮助的部分**：_______________
5. **还需澄清的部分**：_______________
6. **今后培训希望增加的主题**：_______________

### 成功指标

- ✓ 团队 100% 参加培训
- ✓ 30 天内无新文档写在遗留目录
- ✓ 30 天内至少新增 5 份规范/变更提案
- ✓ 80% 以上团队反馈积极
- ✓ 减少对文档位置的困惑

---

## 培训师清单

### 培训前

- [ ] 审阅所有培训材料
- [ ] 准备演示幻灯片
- [ ] 打印快速参考卡
- [ ] 准备演示环境
- [ ] 测试目录导航示例
- [ ] 准备练习手册
- [ ] 安排会议并发送日历邀请
- [ ] 发送通知邮件

### 培训中

- [ ] 签到
- [ ] 概览介绍（10 分钟）
- [ ] 演示新结构（15 分钟）
- [ ] 演示创建流程（15 分钟）
- [ ] 演示归档访问（5 分钟）
- [ ] 主持问答（10–15 分钟）
- [ ] 分发材料
- [ ] 收集初步反馈

### 培训后

- [ ] 发送含幻灯片的跟进邮件
- [ ] 发布录像（若为线上）
- [ ] 安排答疑时间
- [ ] 关注新文档创建情况
- [ ] 收集评估表
- [ ] 处理提出的问题
- [ ] 按需安排复习培训

---

## 补充资源

### 面向团队成员

- **边界指南**：`openspec/docs/documentation-boundary-guidelines.md`
- **模板**：`openspec/specs/_template/`、`openspec/changes/_template/`
- **归档清单**：`archive/LEGACY_DOCS_MANIFEST.md`
- **本培训指南**：`openspec/changes/md-milestone-document-organization/team-training-guide.md`

### 面向培训师

- **有效性评估报告**：`openspec/changes/md-milestone-document-organization/validity-assessment-report.md`
- **依赖分析**：`openspec/changes/md-milestone-document-organization/dependency-analysis-report.md`
- **团队审阅指南**：`openspec/changes/md-milestone-document-organization/team-review-guide.md`
- **提案**：`openspec/changes/md-milestone-document-organization/proposal.md`

---

## 常见问题处理

### 问题：仍有人在旧位置写文档

**处理**：
- 温和提醒新流程
- 指向边界指南
- 主动协助将文档移到正确位置

### 问题：不清楚何时用规范 vs 变更提案

**处理**：
- 使用边界指南中的决策树
- 拿不准时用变更提案
- 向技术负责人确认

### 问题：在归档里找不到某文档

**处理**：
- 查归档清单中的文件列表
- 用 `tar -tzf` 列出内容
- 若不在归档中则查 Git 历史

### 问题：模板太复杂

**处理**：
- 先关注必填项
- 可选部分可后续补充
- 提供简化示例

---

**培训指南版本**：1.0
**创建日期**：2026-01-15
**创建方**：Claude（OpenSpec 迁移 Agent）
**状态**：可供培训使用
**下次审阅**：培训后根据反馈改进
