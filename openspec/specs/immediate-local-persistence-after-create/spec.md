# Immediate local persistence after create Spec

## Purpose
Define requirements to persist Material and Provider entities to local database immediately after remote create succeeds, so same-session local queries can hit newly created records.

## Requirements

### Requirement: Create success SHALL persist Material to local database immediately
系统在调用远端创建材料接口成功后，MUST 在当前服务流程中立即将返回的 Material 数据持久化到本地数据库（插入或更新），使后续本地查询可立即读取。

#### Scenario: Material create success inserts new local record
- **WHEN** `CreateMaterialByNameAsync` 返回成功且本地不存在同 ID Material
- **THEN** 系统 MUST 在返回前将该 Material 插入本地数据库

#### Scenario: Material create success updates existing local record
- **WHEN** `CreateMaterialByNameAsync` 返回成功且本地已存在同 ID Material
- **THEN** 系统 MUST 更新本地记录而不是抛出主键冲突异常

### Requirement: Create success SHALL persist Provider to local database immediately
系统在调用远端创建供应商接口成功后，MUST 在当前服务流程中立即将返回的 Provider 数据持久化到本地数据库（插入或更新），使后续本地查询可立即读取。

#### Scenario: Provider create success inserts new local record
- **WHEN** `CreateProviderAsync` 返回成功且本地不存在同 ID Provider
- **THEN** 系统 MUST 在返回前将该 Provider 插入本地数据库

#### Scenario: Provider create success updates existing local record
- **WHEN** `CreateProviderAsync` 返回成功且本地已存在同 ID Provider
- **THEN** 系统 MUST 更新本地记录而不是抛出主键冲突异常
