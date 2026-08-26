---
tags: [home]
created: 2026-07-25
updated: 2026-08-26
---

# Basic Unity Framework — 知识库

## 项目概述

这是一个 Unity 3D 游戏基础框架项目，提供角色移动、输入处理、行为系统、战斗系统、任务系统等通用模块。框架采用分层架构，Gear 层提供通用基础能力，Expand Component 层提供可挂载组件。

主要命名空间：`Framework.Core`（基础工具）、`Framework.ExpandComponent.UnitMover`（移动）、`Framework.ExpandComponent.DataProvider`（数据驱动）、`BehaviorCore`（行为系统）。

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
| `Assets/Scripts/C#/Framework/Expand Component/` | 可挂载组件（UnitMover, DataProvider） |
| `Assets/Editor/Framework/` | 框架编辑器工具 |
| `Knowledge Hub/` | 本知识库 |
