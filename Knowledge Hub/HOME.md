---
tags: [home]
created: 2026-07-25
updated: 2026-09-01
---

# Basic Unity Framework — 知识库

## 项目概述

这是一个 Unity 3D 游戏基础框架项目，提供角色移动、输入处理、行为系统、战斗系统、任务系统等通用模块。框架采用分层架构，Gear 层提供通用基础能力，Expand Component 层提供可挂载组件。

主要命名空间：`Framework.Core`（基础工具）、`Framework.Gameplay.Abilities`（能力系统）、`BehaviorEditor`（行为时间线与运行时执行）。移动、输入、浮动胶囊、跳跃和边缘保护等功能现在由独立能力 Runtime 及其纯 C# 模块组合，不再由 `UnitMover` 或 `DataProvider` 组件提供统一调度。

## 快速导航

- [[modules/项目架构总览]] — 完整项目结构与模块清单
- [[modules/DataProvider技能]] — DataProvider 架构与开发约束
- [[modules/UnitMover 边缘防跌落方案]] — 边缘检测与熔断回退
- [[modules/UnitMover 重构方向]] — UnitMover 重构架构方向
- [[modules/GTS 头发高光贴图]] — CH0221 Hair Spec 编码验证与头发高光探索记录
- [[references/跳跃手感优化]] — 跳跃手感技术参考
- [[_conventions/命名规范]] — 命名与编码约定
- [[_conventions/框架设计原则]] — 框架级设计决策记录
- [[decisions/输入系统重构决策]] — 架构决策记录
- [[decisions/UnitMover 审查决策 2026-08-01]] — UnitMover 审查讨论决策（漏洞/架构/算法确认项）
- [[decisions/UnitMover 层级职责重构决策]] — UnitMover 策略层级职责与生命周期决策
- [[decisions/DataProvider 架构审查决策]] — DataProvider 架构审查确认项（E/U/P 项）
- [[decisions/数据驱动能力系统实施计划审查与修订]] — 能力系统破坏式迁移与能力 SO 实施规则

## 最近变更

- 2026-09-01：BehaviorEditor 事件轨删除 VFX/音频/投射物/Buff 等内置业务分类，改为 `BehaviorEventExecuteSO.Execute(BehaviorEventContext)` 的项目侧扩展点；删除原生音频、VFX 控制与激活轨的运行时事件导出，并按轨道收拢运行时、编辑器和编译器文件
- 2026-08-31：BehaviorEditor 删除 `BehaviorClip` 到 Timeline 的反向回填、旧 SO 降级回填与作者期轨道快照；Timeline 现在是唯一作者源，结束编辑时单向导出运行时行为数据；轨道编译器自动发现，运行时由多态轨道数据创建执行器
- 2026-08-28：输入能力配置改用 `InputActionReference` 直接选择动作；旧动作名称仅作为隐藏迁移回退，输入采集与 GAS 能力执行职责保持分离
- 2026-08-28：新增 `Tool/GAS/Create New Ability Scripts` 能力脚本生成工具；修复模板逐字字符串引号导致的 Editor 编译错误，并自动注册、注销能力 RuntimeData
- 2026-08-27：修复 `MovementAbility.asset` 对场景相机的非法 PPtr 引用；移动参考相机改为运行时缓存 `Camera.main`，浮动胶囊、跳跃和边缘保护配置继续保留在移动资产内部
- 2026-08-26：收紧能力系统边界；`AbilityComponent` 不再接管物理组件，`MovementAbilityRuntime` 自行组合浮动胶囊、接地、悬浮、跳跃和边缘保护模块
- 2026-08-25：完成能力系统第一轮破坏式迁移；删除 UnitMover/DataProvider 调度外壳，新增输入监听和移动能力 Runtime
- 2026-08-16：确认 CH0221_Hair_Spec 的 RGB 为近单位方向编码；当前恢复并使用 S 方案各向异性切线场作为高光基线，方案 3 环境反射方向试验已撤回
- 2026-08-04：UnitMover 移除 UnitMovementRuntime/Profile/命令来源注册表；策略直持模块，默认策略改为 NormalGroundMovementStrategy，Inspector 自动绘制策略字段，待 Unity 场景验证
- 2026-08-02：UnitMover A/B/C/D 确认项已实施，待 Unity 场景验证；DataProvider 审查归理为 P1/P3 待实施，E2/E3 不立项，U2 改为字段改名迁移规则
- 2026-08-01：UnitMover 审查决策记录（A1-A6/B1-B7/C1-C5 逐条讨论定稿，代码未改）
- 2026-07-29：UnitMover 重构：模块化架构（Core/Motor/Physics/Profiles）、浮动胶囊脚底 BoxCollider
- 2026-07-27：DataProvider 模块重构：Blackboard 可继承、DataSourceHandler、Delta 属性
- 最近提交：`403a866` UnitMover 重构——边缘防跌落、策略选择、跳跃手感

## 关键目录

| 目录 | 说明 |
|------|------|
| `Assets/Scripts/C#/Framework/Gear/` | 基础工具（EventSystem, ObjectPool, AudioManager...） |
| `Assets/_Project/Scripts/CSharp/Core/Gameplay/GAS/` | 能力系统核心、能力 Runtime 和运行时数据 |
| `Assets/_Project/Editor/Gameplay/GAS/` | 能力脚本生成器和能力编辑器工具 |
| `Assets/Editor/Framework/` | 框架编辑器工具 |
| `Knowledge Hub/` | 本知识库 |
