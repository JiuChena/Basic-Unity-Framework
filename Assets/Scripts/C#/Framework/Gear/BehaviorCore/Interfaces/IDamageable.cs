using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// BehaviorCore 运行时可识别的最小单位数据抽象。
    /// 具体项目可由角色、敌人或其他战斗单位实现。
    /// </summary>
    public interface IBehaviorUnit
    {
        int UnitId { get; }
        bool IsDead { get; }
        bool IsTargetable { get; }
        float CurrentHealth { get; }
        GameObject RuntimeGameObject { get; }
        Transform RuntimeTransform { get; }
        string DebugName { get; }
    }

    /// <summary>
    /// 可被 BehaviorCore 命中和受伤处理的目标接口。
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ReceiveDamage(float damage, Vector3 knockback, float hitStunDuration, GameObject source);
    }
}
