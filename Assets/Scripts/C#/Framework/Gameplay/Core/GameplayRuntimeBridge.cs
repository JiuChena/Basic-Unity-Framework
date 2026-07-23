/// <summary>
/// Access point for pooled objects and owner-scoped VFX from gameplay systems.
/// </summary>
public static class GameplayPresentationBridge
{
    private static IGameplayPresentationBridge presentationBridge = CoreFrameworkGameplayPresentationBridge.Instance;

    public static IGameplayPresentationBridge PresentationBridge => presentationBridge;

    public static void Configure(IGameplayPresentationBridge customPresentationBridge = null)
    {
        if (customPresentationBridge != null)
            presentationBridge = customPresentationBridge;
    }

    public static void ResetToDefaults()
    {
        presentationBridge = CoreFrameworkGameplayPresentationBridge.Instance;
    }

    public static void ReturnPooledObject(UnityEngine.GameObject target)
    {
        presentationBridge.ReturnPooledObject(target);
    }

    public static void SpawnOwnerVfx(int ownerId, UnityEngine.GameObject prefab, UnityEngine.Vector3 position,
        UnityEngine.Quaternion rotation, UnityEngine.Vector3 scale,
        float autoRecycleTime)
    {
        presentationBridge.SpawnOwnerVfx(ownerId, prefab, position, rotation, scale, autoRecycleTime);
    }

    public static void ClearOwnerVfx(int ownerId)
    {
        presentationBridge.ClearOwnerVfx(ownerId);
    }
}
