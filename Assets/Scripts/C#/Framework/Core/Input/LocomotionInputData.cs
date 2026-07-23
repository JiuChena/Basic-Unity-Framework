using System;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 移动、视角和角色机动相关的输入数据槽。
    /// </summary>
    [Serializable]
    public sealed class LocomotionInputData : IBlackboardData
    {
        /// <summary>
        /// 平面移动输入，范围通常为 -1 到 1。
        /// </summary>
        public Vector2 Move { get; set; }

        /// <summary>
        /// 本帧视角增量。
        /// </summary>
        public Vector2 Look { get; set; }

        /// <summary>
        /// 当前是否处于冲刺输入状态。
        /// </summary>
        public bool IsSprinting { get; set; }

        /// <summary>
        /// 跳跃按钮状态。
        /// </summary>
        public InputButton Jump { get; } = new InputButton();

        /// <summary>
        /// 下蹲按钮状态。
        /// </summary>
        public InputButton Crouch { get; } = new InputButton();

        /// <summary>
        /// 清空全部移动输入状态。
        /// </summary>
        public void Clear()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            IsSprinting = false;
            Jump.Clear();
            Crouch.Clear();
        }
    }
}
