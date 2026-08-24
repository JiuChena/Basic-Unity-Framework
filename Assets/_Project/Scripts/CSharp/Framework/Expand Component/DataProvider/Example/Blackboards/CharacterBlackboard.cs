using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.UnitMover;
using UnityEngine;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// 可移动角色的基础数据面板。注册所有角色共有的移动和姿态属性。
    /// 具体实体类型（玩家、敌人、NPC）从此类继承并追加领域特有属性。
    /// </summary>
    public abstract class CharacterBlackboard : Blackboard, IUnitMovementInput, IUnitMovementReferenceFrame
    {
        /// <summary>平面移动输入（自动归一化）。</summary>
        public MoveAttribute Move { get; }

        /// <summary>视角增量（多消费者独立消费）。</summary>
        public LookAttribute Look { get; }

        /// <summary>是否按住冲刺键。</summary>
        public SprintAttribute Sprint { get; }

        /// <summary>下蹲按钮。</summary>
        public CrouchAttribute Crouch { get; }

        /// <summary>跳跃按钮。</summary>
        public JumpAttribute Jump { get; }

        // 由 UnitMover 注入的世界空间移动参考；为空时回退主摄像机。
        private Transform _movementReference;
        // 未显式注入参考时缓存的主摄像机，避免每个固定步重复查找。
        private Camera _fallbackCamera;

        /// <summary>获取或设置用于将平面输入转换为世界方向的参考 Transform。</summary>
        public Transform MovementReference
        {
            get => _movementReference;
            set => _movementReference = value;
        }

        /// <summary>获取相对于主摄像机朝向的世界空间平面移动方向。</summary>
        public Vector3 WorldMoveDirection
        {
            get
            {
                Vector2 input = Move.Value;
                if (input.sqrMagnitude <= 0.0001f) return Vector3.zero;

                Transform reference = ResolveMovementReference();
                if (reference == null) return new Vector3(input.x, 0f, input.y);

                Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
                return (forward * input.y + right * input.x).normalized;
            }
        }

        /// <summary>获取由当前冲刺输入决定的通用移动速度倍率。</summary>
        public float SpeedScale => Sprint.Value ? 1.6f : 1f;

        /// <summary>获取当前跳跃输入是否仍处于按住状态。</summary>
        public bool IsJumpHeld => Jump.IsHeld;

        /// <summary>
        /// 构造时自动注册全部角色共有属性。
        /// </summary>
        protected CharacterBlackboard()
        {
            Move = Register(new MoveAttribute());
            Look = Register(new LookAttribute());
            Sprint = Register(new SprintAttribute());
            Crouch = Register(new CrouchAttribute());
            Jump = Register(new JumpAttribute());
        }

        /// <summary>
        /// 初始化一个独立消费者的跳跃按下事件游标。
        /// </summary>
        /// <param name="pressedVersion">需要初始化的消费者游标。</param>
        public void InitializeJumpPressedCursor(ref uint pressedVersion)
        {
            Jump.InitializePressedCursor(ref pressedVersion);
        }

        /// <summary>
        /// 消费从指定游标之后产生的跳跃按下事件。
        /// </summary>
        /// <param name="pressedVersion">消费者上次读取到的事件版本。</param>
        /// <param name="pressed">是否存在未消费的跳跃按下事件。</param>
        /// <returns>是否成功读取跳跃输入。</returns>
        public bool ConsumeJumpPressed(ref uint pressedVersion, out bool pressed)
        {
            return Jump.ConsumePressed(ref pressedVersion, out pressed);
        }

        /// <summary>
        /// 优先返回 UnitMover 注入的参考 Transform；未注入时缓存并回退主摄像机。
        /// </summary>
        /// <returns>可用于转换移动输入的参考 Transform；没有可用摄像机时返回 null。</returns>
        private Transform ResolveMovementReference()
        {
            if (_movementReference != null) return _movementReference;

            // 仅在没有缓存或缓存对象已销毁时查找主摄像机。
            if (_fallbackCamera == null) _fallbackCamera = Camera.main;
            return _fallbackCamera != null ? _fallbackCamera.transform : null;
        }
    }
}
