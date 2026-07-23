using System;
using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// AI 移动策略（占位）。由导航系统输出方向，不读取玩家输入。
    /// </summary>
    [Serializable]
    public class AIMovementStrategy : MovementStrategy
    {
        /// <summary>
        /// AI 策略自身提供移动目标，不依赖玩家输入黑板。
        /// </summary>
        public override bool RequiresInputProvider => false;

        [Tooltip("到达目标点的距离阈值")]
        public float arrivalThreshold = 0.5f;
        [Tooltip("巡逻时随机游走半径")]
        public float wanderRadius = 5f;

        private Vector3 _currentWaypoint;

        public override void Execute(Blackboard board, UnitMover mover)
        {
            if (mover == null) return;

            Vector3 toTarget = _currentWaypoint - mover.transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude > arrivalThreshold)
                mover.Move(toTarget.normalized);
        }

        public void SetDestination(Vector3 destination)
        {
            _currentWaypoint = destination;
        }
    }
}
