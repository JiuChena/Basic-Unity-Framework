using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 封装 UnitMover 对刚体的唯一写入边界。
    /// </summary>
    public interface IUnitBody
    {
        /// <summary>刚体是否仍然有效。</summary>
        bool IsValid { get; }

        /// <summary>当前刚体世界位置。</summary>
        Vector3 Position { get; }

        /// <summary>当前刚体世界旋转。</summary>
        Quaternion Rotation { get; }

        /// <summary>当前刚体线速度。</summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// 将本物理步经所有运动模块合并后的最终线速度写入底层刚体。
        /// </summary>
        /// <param name="velocity">已由所有运动模块合并完成的最终线速度。</param>
        void Commit(Vector3 velocity);

        /// <summary>
        /// 恢复到安全位置并清空惯性，用于异常跌落回退。
        /// </summary>
        /// <param name="position">需要恢复到的世界位置。</param>
        /// <param name="rotation">需要恢复到的世界旋转。</param>
        void RestoreSafePosition(Vector3 position, Quaternion rotation);

        /// <summary>
        /// 接管刚体前记录的物理设置恢复给外部系统。
        /// </summary>
        void RestoreInitialSettings();
    }

    /// <summary>
    /// 隔离 Physics 静态查询，使物理模块不直接依赖场景查找和 Unity 生命周期。
    /// </summary>
    public interface IPhysicsQuery
    {
        /// <summary>
        /// 执行无分配射线检测。
        /// </summary>
        /// <param name="origin">射线起点。</param>
        /// <param name="direction">射线方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        int RaycastNonAlloc(Vector3 origin, Vector3 direction, float distance, int layerMask, RaycastHit[] results);

        /// <summary>
        /// 执行无分配球体检测，用于确认可跨越窄缝后的脚底支撑。
        /// </summary>
        /// <param name="origin">球体检测起点。</param>
        /// <param name="radius">球体半径。</param>
        /// <param name="direction">检测方向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float distance, int layerMask, RaycastHit[] results);

        /// <summary>
        /// 执行无分配方体检测，用于 BoxCollider 的实际接地检测。
        /// </summary>
        /// <param name="center">方体中心点。</param>
        /// <param name="halfExtents">方体在三个轴向上的半尺寸。</param>
        /// <param name="direction">检测方向。</param>
        /// <param name="orientation">方体的世界空间朝向。</param>
        /// <param name="distance">最大检测距离。</param>
        /// <param name="layerMask">参与检测的物理层。</param>
        /// <param name="results">复用的命中结果缓冲区。</param>
        /// <returns>写入缓冲区的命中数量。</returns>
        int BoxCastNonAlloc(
            Vector3 center,
            Vector3 halfExtents,
            Vector3 direction,
            Quaternion orientation,
            float distance,
            int layerMask,
            RaycastHit[] results);
    }

    /// <summary>
    /// 由业务层实现的纯 C# 命令来源，例如玩家输入、AI 或网络回放。
    /// </summary>
    public interface IUnitMovementCommandSource
    {
        /// <summary>
        /// 命令来源注册到运行时时调用一次。
        /// </summary>
        /// <param name="runtime">接收该来源命令的运行时实例。</param>
        void OnRegistered(UnitMovementRuntime runtime);

        /// <summary>
        /// 命令来源成为当前激活来源时接收状态快照。
        /// </summary>
        /// <param name="state">切换时的当前运动状态。</param>
        void OnActivated(in UnitMovementState state);

        /// <summary>
        /// 生成本物理步的通用移动命令。
        /// </summary>
        /// <param name="state">本物理步开始时的运动状态。</param>
        /// <param name="command">需要写入或覆盖的命令数据。</param>
        void BuildCommand(in UnitMovementState state, ref UnitMovementCommand command);

        /// <summary>
        /// 命令来源不再是当前激活来源时调用。
        /// </summary>
        void OnDeactivated();

        /// <summary>
        /// 命令来源从运行时注册表移除时调用。
        /// </summary>
        void OnUnregistered();
    }

    /// <summary>
    /// 由数据黑板实现的通用移动输入读取契约。
    /// UnitMover 只依赖该契约，不依赖任何具体 Provider、Blackboard 或 Attribute 类型。
    /// </summary>
    public interface IUnitMovementInput
    {
        /// <summary>获取已转换到世界空间的当前平面移动方向。</summary>
        Vector3 WorldMoveDirection { get; }

        /// <summary>获取当前移动速度倍率。</summary>
        float SpeedScale { get; }

        /// <summary>获取当前跳跃输入是否仍处于按住状态。</summary>
        bool IsJumpHeld { get; }

        /// <summary>
        /// 初始化一个独立消费者的跳跃按下事件游标。
        /// </summary>
        /// <param name="pressedVersion">需要初始化的消费者游标。</param>
        void InitializeJumpPressedCursor(ref uint pressedVersion);

        /// <summary>
        /// 消费从指定游标之后产生的跳跃按下事件。
        /// </summary>
        /// <param name="pressedVersion">消费者上次读取到的事件版本。</param>
        /// <param name="pressed">是否存在未消费的跳跃按下事件。</param>
        /// <returns>是否成功读取跳跃输入。</returns>
        bool ConsumeJumpPressed(ref uint pressedVersion, out bool pressed);
    }

}
