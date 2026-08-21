using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Gear
{
    /// <summary>
    /// 拥有者特效池。按 owner ID 和预制体实例 ID 分组缓存运行时特效对象。
    /// owner 可以是角色、区域、世界机关等任意具有明确生命周期的表现拥有者。
    /// </summary>
    public class VFXPool
    {
        private static readonly VFXPool instance = new VFXPool();
        public static VFXPool Instance => instance;

        private readonly Dictionary<int, Dictionary<int, Queue<GameObject>>> cachedVfxByOwner =
            new Dictionary<int, Dictionary<int, Queue<GameObject>>>();

        private readonly Dictionary<int, List<GameObject>> activeVfxByOwner =
            new Dictionary<int, List<GameObject>>();

        private Transform root;

        private VFXPool() { }

        public void Spawn(int ownerId, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float autoRecycleTime, UnityAction<GameObject> callback = null)
        {
            if (prefab == null)
                return;

            EnsureRoot();

            int prefabKey = prefab.GetInstanceID();
            GameObject instance = GetOrCreateInstance(ownerId, prefabKey, prefab);
            if (instance == null)
                return;

            instance.transform.SetParent(null, true);
            ApplySpawnTransform(instance.transform, prefab.transform, position, rotation, scale);
            instance.SetActive(true);
            RestartSpawnedVfx(instance);

            VfxPoolItem item = instance.GetComponent<VfxPoolItem>();
            if (item == null)
                item = instance.AddComponent<VfxPoolItem>();

            item.Bind(this, ownerId, prefabKey, Mathf.Max(0.01f, autoRecycleTime));

            if (!activeVfxByOwner.TryGetValue(ownerId, out List<GameObject> activeList))
            {
                activeList = new List<GameObject>();
                activeVfxByOwner.Add(ownerId, activeList);
            }

            if (!activeList.Contains(instance))
                activeList.Add(instance);
            
            callback?.Invoke(instance);
        }

        public void ClearOwner(int ownerId)
        {
            if (activeVfxByOwner.TryGetValue(ownerId, out List<GameObject> activeList))
            {
                for (int i = activeList.Count - 1; i >= 0; i--)
                {
                    if (activeList[i] != null)
                        Object.Destroy(activeList[i]);
                }

                activeVfxByOwner.Remove(ownerId);
            }

            if (cachedVfxByOwner.TryGetValue(ownerId, out Dictionary<int, Queue<GameObject>> cachedMap))
            {
                foreach (KeyValuePair<int, Queue<GameObject>> pair in cachedMap)
                {
                    while (pair.Value.Count > 0)
                    {
                        GameObject cached = pair.Value.Dequeue();
                        if (cached != null)
                            Object.Destroy(cached);
                    }
                }

                cachedVfxByOwner.Remove(ownerId);
            }
        }

        internal void Recycle(GameObject instance, int ownerId, int prefabKey)
        {
            if (instance == null)
                return;

            EnsureRoot();
            instance.transform.SetParent(root, false);
            instance.SetActive(false);

            if (activeVfxByOwner.TryGetValue(ownerId, out List<GameObject> activeList))
                activeList.Remove(instance);

            if (!cachedVfxByOwner.TryGetValue(ownerId, out Dictionary<int, Queue<GameObject>> cachedMap))
            {
                cachedMap = new Dictionary<int, Queue<GameObject>>();
                cachedVfxByOwner.Add(ownerId, cachedMap);
            }

            if (!cachedMap.TryGetValue(prefabKey, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                cachedMap.Add(prefabKey, queue);
            }

            queue.Enqueue(instance);
        }

        private GameObject GetOrCreateInstance(int ownerId, int prefabKey, GameObject prefab)
        {
            if (cachedVfxByOwner.TryGetValue(ownerId, out Dictionary<int, Queue<GameObject>> cachedMap) &&
                cachedMap.TryGetValue(prefabKey, out Queue<GameObject> queue))
            {
                while (queue.Count > 0)
                {
                    GameObject cached = queue.Dequeue();
                    if (cached != null)
                        return cached;
                }
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            return instance;
        }

        private static void ApplySpawnTransform(Transform instanceTransform, Transform prefabTransform, Vector3 position,
            Quaternion rotation, Vector3 scale)
        {
            if (instanceTransform == null || prefabTransform == null)
                return;

            Vector3 composedPosition = Matrix4x4.TRS(position, rotation, scale).MultiplyPoint3x4(prefabTransform.localPosition);
            Quaternion composedRotation = rotation * prefabTransform.localRotation;
            Vector3 composedScale = Vector3.Scale(prefabTransform.localScale, scale);

            instanceTransform.position = composedPosition;
            instanceTransform.rotation = composedRotation;
            instanceTransform.localScale = composedScale;
        }

        private static void RestartSpawnedVfx(GameObject instance)
        {
            if (instance == null)
                return;

            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            TrailRenderer[] trailRenderers = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                TrailRenderer trailRenderer = trailRenderers[i];
                if (trailRenderer == null)
                    continue;

                trailRenderer.Clear();
            }
        }

        private void EnsureRoot()
        {
            if (root != null)
                return;

            GameObject rootObject = new GameObject("VFXPool");
            Object.DontDestroyOnLoad(rootObject);
            root = rootObject.transform;
        }
    }
}
