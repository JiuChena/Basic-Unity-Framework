# Framework Layer

这一层是可复用的基础框架，只承载可迁移的通用能力。

## 目录职责

- `CoreFramework`
  - 基础设施与通用服务
  - 例如事件中心、对象池、音频、输入、HSM、二进制数据、基础交互

- `BehaviorCore`
  - 行为编辑器与行为解释器核心
  - 包含行为数据结构、Timeline 作者期核心轨道、导出回填、运行时解释执行

- `Gameplay`
  - 通用战斗玩法承载层
  - 包含 `StatusData`、`UnitDriverBase`、效果系统、投射物、索敌、全局战斗配置
  - 当前已把 `UnitAlignment`、`UnitTargetingModule`、`IUnitDefinition`、`IUnitTargetingProvider` 扶正为主入口
  - 也承载部分角色运行时会复用的通用契约，例如 `UnitAbilityLevelGroup`、`IUnitAbilityLevelProvider`、`IUnitNumericResolver`

## 硬性边界
- 这一层不能依赖任何 `Business/<Project>` 目录
- 这一层可以被新项目整体迁移复用
- 这一层允许保留对 Unity 包和通用第三方基础库的依赖
- 这一层的命名、接口和注释应避免写死当前项目语义
- 对旧 `Character*` 语义类型，优先采用"新增 `Unit*` 兼容入口，再逐步迁移"的方式纯化
- 当前这条策略已进入第二阶段：`Unit*` 主入口已逐步落位，旧 `Character*` 类型开始退回兼容层

## 当前推荐依赖口
新项目如果直接接框架层，优先依赖这些中性入口：

- 单位静态定义：`IUnitDefinition`
- 单位行为定义聚合口：`IUnitBehaviorDefinition`
- 单位索敌提供器：`IUnitTargetingProvider`
- 单位阵营：`UnitAlignment`
- 通用单位索敌组件：`UnitTargetingModule`
- 全局索敌转向配置：`BattleGlobalSettingsSO.rotateHostUnitTowardProjectileTarget`

以下旧名目前仍保留，但应视为兼容层：

- `ICharacterUnitDefinition`
- `IProjectileTargetingProvider`
- `CharacterAlignment`
- `CharacterTargetingModule`
- `BattleGlobalSettingsSO.rotateCharacterTowardProjectileTarget`

说明：
- 这些旧名主要保留在兼容类型、兼容属性或兼容接口层
- `IProjectileTargetingProvider` 当前也不再由主入口 `UnitTargetingModule` 直接实现，而是只挂在旧兼容壳 `CharacterTargetingModule` 中
- 当前主运行时访问口已经继续收敛到 `IUnitDefinition`、`IUnitTargetingProvider`、`UnitAlignment`

## 前置依赖

把 `Framework` 层迁到新项目前，需要先确认这些前置依赖已经满足，否则 asmdef 会直接编译失败：

- `Assets/Plugins/MessagePack/Runtime`
- `Assets/Plugins/MessagePack/Unity`
- Unity Package `com.unity.addressables`
- Unity Package `com.unity.textmeshpro`
- Unity Package `com.unity.inputsystem`
- Unity Package `com.unity.timeline`
- UGUI 运行时程序集 `UnityEngine.UI`

说明：
- 这些依赖不是"当前项目业务逻辑"依赖，而是 `Framework.Runtime.asmdef` 和 `Framework.Editor.asmdef` 当前已经显式引用的编译前置条件
- 如果新项目只想迁"行为编辑器最小闭环"，也仍然要满足这些编译依赖，除非后续继续把 `CoreFramework` 中的持久化、UI、输入等能力再拆分成更细 asmdef
- 其中 `MessagePack` 当前来自 `Assets/Plugins/MessagePack`，不属于 Unity 默认内置包，迁框架时要一并带走

## 新项目迁移建议
新 RPG 项目如果要整包复用当前基础框架，最少可直接迁移：
- `Assets/Scripts/C#/Framework/CoreFramework`
- `Assets/Scripts/C#/Framework/BehaviorCore`
- `Assets/Scripts/C#/Framework/Gameplay`
- `Assets/Editor/Framework`

如果目标只是单独拿走"行为编辑器 + 行为解释器 + 通用战斗承载链路"，则不要简单理解为"只复制 BehaviorCore 和 Gameplay 就够了"。
当前这条最小闭环仍然依赖 `CoreFramework` 中的这些子包：
- `事件中心模块`
- `对象池模块`
- `特效管理模块`

按当前代码状态，它们分别承载：
- `TypedEventBus` / `EventCenter` / `EventNames`
- `ObjectsPool`
- `VFXPool`

这意味着：
- `BehaviorCore + Gameplay` 现在已经可以视为"行为系统核心层"
- 但它还没有完全脱离 `CoreFramework`
- 真正做成独立插件时，应继续把这些强依赖子包单独列为"行为系统必需基础包"，而不是在文档里模糊写成"可完全不带 CoreFramework"

然后在新项目中自行新增：

- `Assets/Scripts/C#/Business/<YourProject>`
- `Assets/Editor/Business/<YourProject>`

由项目侧负责接入单位配置、项目数值表、VFX、音频、相机和具体状态机规则。

## 行为系统接入基线

如果新项目要把行为编辑器和行为解释器接到自己的单位配置资产上，框架当前推荐的最小实现口是：

- `IUnitBehaviorDefinition`

它聚合了两类最基本能力：
- `IUnitDefinition`
- `ICharacterBehaviorCatalog`

这意味着：
- 框架并不要求新项目继续使用 `UnitAssetInformation`
- 也不要求新项目继续使用 `IUnitRuntimeDefinition` 这个项目命名接口
- 新项目只要自己的配置资产实现 `IUnitBehaviorDefinition`，就已经满足行为系统最小接入前提

当前 `MiniatureGarden` 项目里的：
- `IUnitRuntimeDefinition`

只是项目扩展接口，它在框架通用定义之外，又额外挂了：
- `IUnitStrategyDefinition`

用于引用当前项目的条件源、切换策略、攻击解析器等策略资产。
