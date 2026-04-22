# manual-data-sync-trigger

## Purpose

在有人值守界面中提供手动数据同步触发能力，允许用户通过点击按钮依次执行物料、物料类型、供应商、运单推送和附件上传同步操作，并通过消息对话框反馈同步结果。

## Requirements

### Requirement: 数据同步按钮绑定命令

系统 SHALL 在有人值守界面（`AttendedWeighingWindow`）顶部菜单栏的"数据同步"按钮上绑定 `SyncDataCommand` 命令，使用户可通过点击按钮触发数据同步操作。

#### Scenario: 数据同步按钮可点击
- **WHEN** 用户打开有人值守界面
- **THEN** 顶部菜单栏 SHALL 显示"数据同步"按钮，且该按钮 SHALL 可点击

#### Scenario: 数据同步按钮在同步过程中禁用
- **WHEN** 用户点击"数据同步"按钮且同步操作正在执行
- **THEN** 按钮 SHALL 处于禁用状态（`IsEnabled = false`），防止用户重复触发

#### Scenario: 同步完成后按钮恢复可用
- **WHEN** 同步操作执行完毕（无论成功或失败）
- **THEN** 按钮 SHALL 恢复为可用状态（`IsEnabled = true`）

### Requirement: 手动触发完整同步流程

系统 SHALL 在用户点击"数据同步"按钮后，依次执行以下同步操作：

1. 物料数据同步（`SyncMaterialAsync`）
2. 物料类型数据同步（`SyncMaterialTypeAsync`）
3. 供应商数据同步（`SyncProviderAsync`）
4. 运单数据推送（`PushWaybillAsync`）
5. 运单附件上传（`SyncPendingAttachmentsToOssAsync`）

#### Scenario: 成功执行完整同步流程
- **WHEN** 用户点击"数据同步"按钮
- **THEN** 系统 SHALL 依次执行物料同步、物料类型同步、供应商同步、运单推送、附件上传 5 个步骤

#### Scenario: 单个步骤失败不中断后续步骤
- **WHEN** 同步流程中某个步骤执行失败
- **THEN** 系统 SHALL 捕获该步骤的异常，继续执行后续同步步骤

#### Scenario: 不执行许可证验证
- **WHEN** 用户手动触发数据同步
- **THEN** 系统 SHALL NOT 执行许可证验证步骤（`VerifyAuthAsync`），仅执行数据同步相关步骤

### Requirement: 同步操作结果反馈

系统 SHALL 在同步流程全部执行完毕后，通过消息对话框（`MessageBox`）向用户显示同步结果摘要。

#### Scenario: 全部同步成功时显示成功信息
- **WHEN** 所有同步步骤均执行成功
- **THEN** 系统 SHALL 显示消息对话框，内容为"数据同步完成：5 项成功"

#### Scenario: 部分同步失败时显示混合结果
- **WHEN** 5 个同步步骤中有 N 项失败（N > 0）
- **THEN** 系统 SHALL 显示消息对话框，内容包含成功数量和失败数量，例如"数据同步完成：3 项成功，2 项失败"

#### Scenario: 消息对话框关闭后按钮恢复
- **WHEN** 用户关闭同步结果消息对话框
- **THEN** "数据同步"按钮 SHALL 恢复为可用状态
