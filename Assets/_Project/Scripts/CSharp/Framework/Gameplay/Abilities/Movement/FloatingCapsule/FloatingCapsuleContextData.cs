namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>保存浮动胶囊能力向其他能力公开的运行时探测引用。</summary>
    public sealed class FloatingCapsuleContextData : IAbilityContextData
    {
        // 浮动胶囊当前使用的碰撞形状模块。
        public ColliderShapeModule ShapeModule { get; }
        // 浮动胶囊当前使用的接地探测模块。
        public GroundProbeModule GroundProbe { get; }

        /// <summary>创建浮动胶囊共享数据并绑定运行时模块。</summary>
        /// <param name="shapeModule">浮动胶囊碰撞形状模块。</param>
        /// <param name="groundProbe">浮动胶囊接地探测模块。</param>
        public FloatingCapsuleContextData(
            ColliderShapeModule shapeModule,
            GroundProbeModule groundProbe)
        {
            ShapeModule = shapeModule;
            GroundProbe = groundProbe;
        }

        /// <summary>浮动胶囊模块没有可单独清空的共享数据。</summary>
        public void Reset() { }
    }
}
