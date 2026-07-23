using System;

namespace CoreFramework
{
    /// <summary>
    /// 战斗动作相关的输入数据槽。
    /// </summary>
    [Serializable]
    public sealed class CombatInputData : IBlackboardData
    {
        /// <summary>
        /// 普攻按钮状态。
        /// </summary>
        public InputButton Attack { get; } = new InputButton();

        /// <summary>
        /// 天赋按钮状态。
        /// </summary>
        public InputButton Talent { get; } = new InputButton();

        /// <summary>
        /// 爆发按钮状态。
        /// </summary>
        public InputButton Burst { get; } = new InputButton();

        /// <summary>
        /// 装填按钮状态。
        /// </summary>
        public InputButton Reload { get; } = new InputButton();

        /// <summary>
        /// 当前是否处于瞄准状态。
        /// </summary>
        public bool IsAiming { get; set; }

        /// <summary>
        /// 清空全部战斗输入状态。
        /// </summary>
        public void Clear()
        {
            Attack.Clear();
            Talent.Clear();
            Burst.Clear();
            Reload.Clear();
            IsAiming = false;
        }
    }
}
