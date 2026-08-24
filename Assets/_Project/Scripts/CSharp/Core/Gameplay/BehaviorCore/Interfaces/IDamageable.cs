using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 可被 BehaviorCore 命中和受伤处理的目标接口。
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ReceiveDamage(float damage, Vector3 knockback, float hitStunDuration, GameObject source);
    }
}
