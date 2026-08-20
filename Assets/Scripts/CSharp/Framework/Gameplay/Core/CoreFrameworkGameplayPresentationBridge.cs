using Core.Gear;
using UnityEngine;

/// <summary>
/// Default bridge backed by CoreFramework object and VFX pools.
/// </summary>
public sealed class CoreFrameworkGameplayPresentationBridge : IGameplayPresentationBridge
{
    public static readonly CoreFrameworkGameplayPresentationBridge Instance = new CoreFrameworkGameplayPresentationBridge();

    private CoreFrameworkGameplayPresentationBridge()
    {
    }

    public void ReturnPooledObject(GameObject target)
    {
        if (target == null)
            return;

        ObjectsPool.Instance.Put(target);
    }

    public void SpawnOwnerVfx(int ownerId, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale,
        float autoRecycleTime)
    {
        if (prefab == null)
            return;

        VFXPool.Instance.Spawn(ownerId, prefab, position, rotation, scale, autoRecycleTime);
    }

    public void ClearOwnerVfx(int ownerId)
    {
        VFXPool.Instance.ClearOwner(ownerId);
    }
}
