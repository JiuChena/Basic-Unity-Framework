using System;
using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 定义 UnitMover 可选择的纯 C# 移动策略；策略只解释通用移动命令，不读取业务输入或数据容器。
    /// </summary>
    [Serializable]
    public abstract class UnitMovementStrategy
    {
        // 由运行时注入的通用水平移动配置，不参与策略自身的序列化。
        [NonSerialized] private LocomotionSettings _locomotionSettings;

        /// <summary>获取 Inspector 和运行时诊断使用的策略名称。</summary>
        public virtual string DisplayName => GetType().Name;

        /// <summary>获取当前运行时注入的水平移动配置。</summary>
        protected LocomotionSettings LocomotionSettings => _locomotionSettings;

        /// <summary>
        /// 由 UnitMover 运行时在首次缓存策略实例时注入通用配置。
        /// </summary>
        /// <param name="locomotionSettings">地面和空中的水平移动配置。</param>
        internal void Initialize(LocomotionSettings locomotionSettings)
        {
            _locomotionSettings = locomotionSettings;
            OnInitialized();
        }

        /// <summary>
        /// 为需要缓存运行时数据的派生策略提供初始化扩展点。
        /// </summary>
        protected virtual void OnInitialized()
        {
        }

        /// <summary>
        /// 根据脚底支撑接触决定本物理步应使用的通用运动状态。
        /// </summary>
        /// <param name="hasGroundContact">当前脚底是否检测到有效支撑面，包含不可行走斜坡。</param>
        /// <returns>本物理步写入运动状态的模式枚举。</returns>
        public abstract MovementMode ResolveMovementMode(bool hasGroundContact);

        /// <summary>
        /// 将通用移动命令转换为尚未经过边缘保护处理的候选平面速度。
        /// </summary>
        /// <param name="state">当前固定步的只读运动状态。</param>
        /// <param name="command">业务层提供的通用移动意图。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>候选世界空间平面速度。</returns>
        public abstract Vector3 BuildPlanarVelocity(
            in UnitMovementState state,
            in UnitMovementCommand command,
            float fixedDeltaTime);

        /// <summary>
        /// 清空当前策略实例持有的全部运行时状态。
        /// </summary>
        public abstract void ClearState();
    }

    /// <summary>
    /// 使用 Profile 中标准地面和空中加减速规则的默认刚体移动策略。
    /// </summary>
    [Serializable]
    public sealed class DefaultRigidbodyMovementStrategy : UnitMovementStrategy
    {
        /// <summary>获取默认刚体策略的显示名称。</summary>
        public override string DisplayName => "默认刚体移动";

        /// <summary>
        /// 根据脚底支撑接触在地面和空中状态之间自动切换。
        /// </summary>
        /// <param name="hasGroundContact">当前脚底是否检测到有效支撑面，包含不可行走斜坡。</param>
        /// <returns>当前应采用的地面或空中状态。</returns>
        public override MovementMode ResolveMovementMode(bool hasGroundContact)
        {
            return hasGroundContact ? MovementMode.Ground : MovementMode.Air;
        }

        /// <summary>
        /// 使用标准配置计算地面或空中的目标平面速度。
        /// </summary>
        /// <param name="state">当前固定步的只读运动状态。</param>
        /// <param name="command">业务层提供的通用移动意图。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>尚未经过边缘保护处理的候选平面速度。</returns>
        public override Vector3 BuildPlanarVelocity(
            in UnitMovementState state,
            in UnitMovementCommand command,
            float fixedDeltaTime)
        {
            if (LocomotionSettings == null) return Vector3.zero;

            if (state.Mode == MovementMode.Ground)
                return BuildGroundVelocity(state, command, fixedDeltaTime);

            return BuildAirVelocity(state, command, fixedDeltaTime);
        }

        /// <summary>
        /// 默认策略没有额外运行时数据，保留空实现以满足所有策略都显式清空状态的契约。
        /// </summary>
        public override void ClearState()
        {
        }

        /// <summary>
        /// 计算沿接地法线切平面的标准地面速度。
        /// </summary>
        /// <param name="state">当前固定步的只读运动状态。</param>
        /// <param name="command">业务层提供的通用移动意图。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>标准地面候选速度。</returns>
        private Vector3 BuildGroundVelocity(
            in UnitMovementState state,
            in UnitMovementCommand command,
            float fixedDeltaTime)
        {
            Vector3 normal = state.GroundNormal.sqrMagnitude > 0.000001f
                ? state.GroundNormal
                : Vector3.up;
            Vector3 direction = Vector3.ProjectOnPlane(command.WorldMoveDirection, normal);
            if (direction.sqrMagnitude > 0.000001f) direction.Normalize();

            float speedScale = Mathf.Max(0f, command.SpeedScale);
            Vector3 targetVelocity = direction * LocomotionSettings.GroundMaxSpeed * speedScale;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(state.CurrentVelocity, normal);
            bool isAccelerating = direction.sqrMagnitude > 0.000001f
                && Vector3.Dot(currentVelocity, targetVelocity) >= 0f;
            float acceleration = isAccelerating
                ? LocomotionSettings.GroundAcceleration
                : LocomotionSettings.GroundDeceleration;
            return Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * fixedDeltaTime);
        }

        /// <summary>
        /// 计算受空中控制系数约束的标准空中速度。
        /// </summary>
        /// <param name="state">当前固定步的只读运动状态。</param>
        /// <param name="command">业务层提供的通用移动意图。</param>
        /// <param name="fixedDeltaTime">当前固定步时长，单位：秒。</param>
        /// <returns>标准空中候选速度。</returns>
        private Vector3 BuildAirVelocity(
            in UnitMovementState state,
            in UnitMovementCommand command,
            float fixedDeltaTime)
        {
            Vector3 direction = Vector3.ProjectOnPlane(command.WorldMoveDirection, Vector3.up);
            if (direction.sqrMagnitude > 0.000001f) direction.Normalize();

            float speedScale = Mathf.Max(0f, command.SpeedScale);
            Vector3 targetVelocity = direction * LocomotionSettings.AirMaxSpeed * speedScale;
            Vector3 currentVelocity = Vector3.ProjectOnPlane(state.CurrentVelocity, Vector3.up);
            float controlAcceleration = LocomotionSettings.AirAcceleration * LocomotionSettings.AirControl;
            return Vector3.MoveTowards(currentVelocity, targetVelocity, controlAcceleration * fixedDeltaTime);
        }
    }
}
