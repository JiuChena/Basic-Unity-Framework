using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// BehaviorEditor 对“投射物事件预制体”的最小作者期契约。
    /// 任何需要被 SpawnProjectile 事件识别的预制体，都应挂载实现此接口的组件。
    /// </summary>
    public interface IBehaviorProjectileContract
    {
    }
}
