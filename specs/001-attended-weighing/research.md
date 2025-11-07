# Research: 有人值守功能实现

**Date**: 2025-11-06  
**Feature**: [spec.md](./spec.md)

## Research Questions & Findings

### 1. ABP HTTP Host在Avalonia桌面应用中的集成

**Question**: 如何在Avalonia桌面应用中集成ABP HTTP Host，提供最小功能实现并支持Swagger？

**Decision**: 使用ABP的ASP.NET Core集成模块，在Avalonia应用启动时启动后台HTTP服务器

**Rationale**: 
- ABP框架提供`Volo.Abp.AspNetCore`包，支持ASP.NET Core集成
- 可以在桌面应用中同时运行Avalonia UI和ASP.NET Core HTTP服务器
- Swagger支持通过`Swashbuckle.AspNetCore`或ABP内置的Swagger模块实现

**Alternatives Considered**:
- 使用独立的HTTP服务器进程：增加部署复杂度，不采用
- 仅使用ABP模块不启动HTTP服务器：无法满足Swagger需求，不采用

**Implementation Notes**:
- 需要在`Program.cs`中配置`WebApplication`和`WebApplicationBuilder`
- 使用`UseUrls`配置HTTP服务器监听地址（如`http://localhost:5000`）
- 在独立线程或后台任务中运行HTTP服务器，避免阻塞Avalonia UI线程
- 最小功能：仅注册ABP模块、Swagger、硬件接口测试API，不需要Auth、CORS等

**References**:
- ABP Framework Documentation: ASP.NET Core Integration
- ABP Swagger Module

---

### 2. ABP AutofacServiceFactory在Avalonia中的应用

**Question**: 如何在Avalonia应用中使用ABP的AutofacServiceFactory进行依赖注入？

**Decision**: 使用`Volo.Abp.Autofac`包，在应用启动时配置Autofac容器

**Rationale**:
- ABP框架提供`Volo.Abp.Autofac`包，专门用于Autofac集成
- 可以与ABP的模块系统无缝集成
- 支持构造函数注入和属性注入

**Alternatives Considered**:
- 使用Microsoft.Extensions.DependencyInjection：ABP默认支持，但spec要求使用Autofac
- 手动配置Autofac：缺少ABP模块集成，不采用

**Implementation Notes**:
- 在`MaterialClientCommonModule`中已经使用ABP模块系统
- 需要在应用启动时调用`AutofacServiceFactory`相关配置
- 确保Avalonia的依赖注入容器与ABP的Autofac容器集成

**References**:
- ABP Framework Documentation: Dependency Injection
- Volo.Abp.Autofac Package

---

### 3. 称重状态机的实现

**Question**: 如何实现地磅重量的状态监测和状态转换逻辑？

**Decision**: 使用状态机模式，实现VehicleWeightStatus枚举和状态转换逻辑

**Rationale**:
- 地磅有三种明确状态：OffScale（已下称）、OnScale（已上称）、Weighing（称重完成）
- 状态转换有明确的触发条件（重量变化、稳定时间）
- 状态机模式适合这种场景

**Alternatives Considered**:
- 简单if-else判断：代码可读性差，维护困难，不采用
- 事件驱动模式：增加复杂度，当前场景不需要，不采用

**Implementation Notes**:
- 创建`VehicleWeightStatus`枚举：OffScale=0, OnScale=1, Weighing=2
- 在`WeighingService`中实现状态监测逻辑：
  - 定时轮询地磅重量（如每100ms）
  - 根据重量值和偏移范围判断状态转换
  - 使用定时器跟踪稳定时间
- 状态转换规则：
  - OffScale → OnScale: 重量 >= WeightOffsetRange上限
  - OnScale → Weighing: 重量稳定超过偏移范围且持续时间 >= WeightStableDuration
  - OnScale → OffScale: 重量未稳定直接回到偏移范围内（称重失败）
  - Weighing → OffScale: 重量回到偏移范围内

**References**:
- State Machine Pattern
- .NET Timer/BackgroundService

---

### 4. 称重记录匹配算法

**Question**: 如何实现两个称重记录的自动匹配逻辑？

**Decision**: 实现两阶段匹配算法：规则1（时间窗口和车牌）+ 规则2（时间顺序和重量关系）

**Rationale**:
- 匹配规则明确，可以分阶段验证
- 需要处理多个候选匹配的情况（选择时间间隔最短的一对）
- 匹配失败时保持未匹配状态，等待后续匹配

**Alternatives Considered**:
- 使用复杂匹配引擎：过度设计，当前需求简单明确，不采用
- 手动匹配：不符合自动化需求，不采用

**Implementation Notes**:
- 在`WeighingMatchingService`中实现匹配逻辑
- 匹配规则1：车牌相同 + 创建时间间隔 <= WeighingMatchDuration
  - 查询所有未匹配记录（WeighingRecordType == Unmatch）
  - 按车牌分组
  - 对每组记录，计算时间间隔，找出所有可能的匹配对
  - 如果有多个匹配对，选择时间间隔最短的一对
- 匹配规则2：时间顺序 + 重量关系
  - 验证Join记录创建时间 < Out记录创建时间
  - 根据DeliveryType验证重量关系：
    - 收料：Join.Weight > Out.Weight
    - 发料：Join.Weight < Out.Weight
- 匹配成功后：
  - 创建Waybill
  - 更新两个WeighingRecord的WeighingRecordType（Join和Out）
  - 提取Provider和Material（任意不为空，都为空时保持null）

**References**:
- LINQ grouping and aggregation
- DateTime calculations

---

### 5. 硬件接口模拟实现

**Question**: 如何实现硬件接口的模拟，支持HTTP接口修改测试值？

**Decision**: 使用内存变量存储测试值，通过HTTP API暴露修改接口

**Rationale**:
- 当前阶段不需要实际硬件，只需返回固定测试值
- HTTP接口允许手动修改测试值，便于测试
- 简单直接，符合YAGNI原则

**Alternatives Considered**:
- 使用配置文件：修改需要重启应用，不便于测试，不采用
- 使用数据库存储：增加复杂度，不采用

**Implementation Notes**:
- 创建硬件服务接口（ITruckScaleWeightService等）
- 实现类中存储静态或实例变量作为测试值
- 创建HTTP API控制器（使用ABP的API控制器基类）
- API端点：
  - GET /api/hardware/truck-scale/weight: 获取地磅重量
  - POST /api/hardware/truck-scale/weight: 设置地磅重量测试值
  - GET /api/hardware/plate-number: 获取车牌号
  - POST /api/hardware/plate-number: 设置车牌号测试值
  - GET /api/hardware/vehicle-photos: 获取车辆照片（返回4张固定图片URL）
  - GET /api/hardware/bill-photo: 获取票据照片（返回1张固定图片URL）
- 所有实现类添加"待对接设备"注释

**References**:
- ABP API Controllers
- HTTP REST API design

---

### 6. Avalonia UI页面实现

**Question**: 如何实现有人值守页面，显示称重记录和运单列表？

**Decision**: 使用Avalonia MVVM模式，创建AttendedWeighingWindow和AttendedWeighingViewModel

**Rationale**:
- 项目已使用CommunityToolkit.Mvvm，遵循MVVM模式
- 分页或虚拟化列表支持大量数据（100条记录）
- 数据绑定简化UI更新

**Alternatives Considered**:
- 使用代码隐藏：违反MVVM模式，不采用
- 使用其他UI框架：不符合技术栈要求，不采用

**Implementation Notes**:
- 创建`AttendedWeighingWindow.axaml`和对应的code-behind
- 创建`AttendedWeighingViewModel`：
  - 使用`ObservableCollection`存储未匹配记录和已完成的运单
  - 使用ABP仓储模式（IRepository）查询数据
  - 实现列表项点击命令，显示详情页面
- UI布局：
  - 左侧：未完成列表（WeighingRecordType == Unmatch）
  - 右侧：已完成列表（Waybill）
  - 底部：详情区域（显示选中项的详细信息）
- 详情页面：
  - 称重记录：显示毛重、进场时间，其他字段为空
  - 运单：显示完整信息
  - 车辆照片和票据照片：使用Image控件显示

**References**:
- Avalonia MVVM Documentation
- CommunityToolkit.Mvvm
- ABP Repository Pattern

---

## Summary

所有关键技术问题已研究并确定实现方案：
1. ✅ ABP HTTP Host集成：使用ASP.NET Core在后台运行
2. ✅ AutofacServiceFactory：使用Volo.Abp.Autofac包
3. ✅ 称重状态机：使用状态机模式实现状态转换
4. ✅ 匹配算法：两阶段匹配规则，选择时间间隔最短的一对
5. ✅ 硬件接口模拟：内存变量 + HTTP API
6. ✅ Avalonia UI：MVVM模式 + ABP仓储

所有NEEDS CLARIFICATION已解决，可以进入Phase 1设计阶段。

