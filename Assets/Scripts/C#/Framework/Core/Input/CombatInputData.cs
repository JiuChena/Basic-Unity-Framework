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
        /// 普攻 — 本帧按下。
        /// </summary>
        public bool AttackPressed => Attack.Pressed;

        /// <summary>
        /// 普攻 — 按住中。
        /// </summary>
        public bool Attacking => Attack.IsHeld;

        /// <summary>
        /// 普攻 — 本帧抬起。
        /// </summary>
        public bool AttackEnd => Attack.Released;

        /// <summary>
        /// 天赋按钮状态。
        /// </summary>
        public InputButton Talent { get; } = new InputButton();

        /// <summary>
        /// 天赋 — 本帧按下。
        /// </summary>
        public bool TalentPressed => Talent.Pressed;

        /// <summary>
        /// 天赋 — 按住中。
        /// </summary>
        public bool Talenting => Talent.IsHeld;

        /// <summary>
        /// 天赋 — 本帧抬起。
        /// </summary>
        public bool TalentEnd => Talent.Released;

        /// <summary>
        /// 爆发按钮状态。
        /// </summary>
        public InputButton Burst { get; } = new InputButton();

        /// <summary>
        /// 爆发 — 本帧按下。
        /// </summary>
        public bool BurstPressed => Burst.Pressed;

        /// <summary>
        /// 爆发 — 按住中。
        /// </summary>
        public bool Bursting => Burst.IsHeld;

        /// <summary>
        /// 爆发 — 本帧抬起。
        /// </summary>
        public bool BurstEnd => Burst.Released;

        /// <summary>
        /// 装填按钮状态。
        /// </summary>
        public InputButton Reload { get; } = new InputButton();

        /// <summary>
        /// 装填 — 本帧按下。
        /// </summary>
        public bool ReloadPressed => Reload.Pressed;

        /// <summary>
        /// 装填 — 按住中。
        /// </summary>
        public bool Reloading => Reload.IsHeld;

        /// <summary>
        /// 装填 — 本帧抬起。
        /// </summary>
        public bool ReloadEnd => Reload.Released;

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
