using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 特效实例的自动回收组件。
    /// </summary>
    public class VfxPoolItem : MonoBehaviour
    {
        private VFXPool owner;
        private int ownerId;
        private int prefabKey;
        private float timeLeft;

        public void Bind(VFXPool poolOwner, int poolOwnerId, int ownerPrefabKey, float lifetime)
        {
            owner = poolOwner;
            ownerId = poolOwnerId;
            prefabKey = ownerPrefabKey;
            timeLeft = lifetime;
        }

        private void Update()
        {
            if (owner == null)
                return;

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
                owner.Recycle(gameObject, ownerId, prefabKey);
        }
    }
}
