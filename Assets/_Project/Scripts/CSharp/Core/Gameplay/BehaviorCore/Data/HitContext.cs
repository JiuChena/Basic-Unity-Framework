using System.Collections.Generic;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// HitBox 检测完成后交给命中执行配置的可写上下文。
    /// </summary>
    public sealed class HitContext
    {
        // 命中对象列表；索引 0 固定为调用者自身，后续项为本次物理查询返回的碰撞体所属对象。
        public List<GameObject> GameObjects = new List<GameObject>();

        // 与 GameObjects 索引一一对应的 CharacterController；目标没有该组件时对应项为 null。
        public List<CharacterController> CharacterControllers = new List<CharacterController>();

        /// <summary>
        /// 按当前集中规则从 <see cref="GameObjects"/> 提取执行所需的组件信息。
        /// </summary>
        /// <remarks>
        /// 子类 Execute 修改 GameObjects 后若仍需使用并行组件列表，应自行再次调用本方法同步数据。
        /// </remarks>
        public void Extract()
        {
            // 预留并清空并行数据，确保每个对象位置都保持索引对齐。
            CharacterControllers.Clear();
            if (CharacterControllers.Capacity < GameObjects.Count)
                CharacterControllers.Capacity = GameObjects.Count;

            // 从每个对象集中提取当前约定的上下文组件。
            for (int index = 0; index < GameObjects.Count; index++)
            {
                GameObject gameObject = GameObjects[index];
                CharacterControllers.Add(gameObject != null ? gameObject.GetComponent<CharacterController>() : null);
            }
        }
    }
}
