---
tags: [home]
created: 2026-07-25
updated: 2026-08-02
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
- [[references/跳跃手感优化]] — 跳跃手感技术参考
- [[_conventions/命名规范]] — 命名与编码约定
- [[_conventions/框架设计原则]] — 框架级设计决策记录
- [[decisions/输入系统重构决策]] — 架构决策记录
- [[decisions/UnitMover 审查决策 2026-08-01]] — UnitMover 审查讨论决策（漏洞/架构/算法确认项）
- [[decisions/DataProvider 架构审查决策]] — DataProvider 架构审查确认项（E/U/P 项）

## 最近变更

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
