using Framework.ExpandComponent.DataProvider;

namespace Framework.ExpandComponent.DataProvider.Example
{
    /// <summary>
    /// 可移动角色的基础数据面板。注册所有角色共有的移动和姿态属性。
    /// 具体实体类型（玩家、敌人、NPC）从此类继承并追加领域特有属性。
    /// </summary>
    public abstract class CharacterBlackboard : Blackboard
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
    }
}
