using UnityEngine;

/// <summary>
/// Minimal bridge between gameplay runtime and presentation/pooling services.
/// </summary>
public interface IGameplayPresentationBridge
{
    void ReturnPooledObject(GameObject target);
    void SpawnOwnerVfx(int ownerId, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale,
        float autoRecycleTime);
    void ClearOwnerVfx(int ownerId);
}
