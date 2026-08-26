using Framework.ExpandComponent.DataProvider;
using UnityEngine;

namespace Framework.Gameplay.Abilities
{
    /// <summary>提供能力访问所属单位和共享黑板的最小运行时上下文。</summary>
    public sealed class AbilityContext
    {
        // 能力所属单位对象。
        public GameObject Owner { get; }
        // 能力所属单位 Transform。
        public Transform Transform { get; }
        // 单位独占的运行时数据黑板。
        public Blackboard Blackboard { get; private set; }

        /// <summary>创建单位能力上下文。</summary>
        /// <param name="owner">能力所属单位对象。</param>
        public AbilityContext(GameObject owner)
        {
            Owner = owner;
            Transform = owner != null ? owner.transform : null;
        }

        /// <summary>绑定能力系统创建的单位独占黑板。</summary>
        /// <param name="blackboard">输入监听能力创建的单位黑板；传入 null 时解除当前引用。</param>
        public void SetBlackboard(Blackboard blackboard)
        {
            Blackboard = blackboard;
        }
    }
}
