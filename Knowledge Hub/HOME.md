---
tags: [home]
created: 2026-07-25
updated: 2026-07-26
---

# Basic Unity Framework — 知识库

## 项目概述

这是一个 Unity 3D 游戏基础框架项目（CoreFramework），提供角色移动、输入处理、行为系统、战斗系统、任务系统等通用模块。框架采用分层架构，Core 层提供通用基础能力，Gameplay 层提供游戏玩法抽象。

## 快速导航

- [[modules/项目架构总览]] — 完整项目结构与模块清单
- [[modules/输入系统整改建议]] — Input → DataProvider 重构完整设计
- [[modules/DataProvider技能]] — DataProvider 开发规范与约束
- [[_conventions/命名规范]] — 命名与编码约定
- [[_conventions/框架设计原则]] — 框架级设计决策记录
- [[decisions/输入系统重构决策]] — 5 项架构决策记录

## 最近变更

- 2026-07-26：创建 DataProvider 开发技能，全局 skill 更新至最新版本
- 2026-07-25：输入系统整改方案讨论定稿，知识库初始化建立
- 最近提交：`86150e8` 修改 UnitMover 和 Input 模块

## 关键目录

| 目录 | 说明 |
|------|------|
| `Assets/Scripts/C#/Framework/Core/` | 核心框架（平台无关） |
| `Assets/Scripts/C#/Framework/Gameplay/` | 游戏玩法框架 |
| `Assets/Editor/Framework/` | 框架编辑器工具 |
| `Knowledge Hub/` | 本知识库 |
