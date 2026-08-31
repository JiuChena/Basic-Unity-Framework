using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// BehaviorEditor 运行时可识别的最小单位数据抽象。
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
}
