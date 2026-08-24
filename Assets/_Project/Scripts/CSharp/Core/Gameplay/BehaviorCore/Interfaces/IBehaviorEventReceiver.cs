using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// BehaviorCore 与项目层之间的依赖注入接口。
    /// </summary>
    public interface IBehaviorEventReceiver
    {
        void SpawnVFX(int unitId, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float autoRecycleTime);
        int PlayAudio(AudioClip clip, Vector3 position, bool loop, float volume = 1f);
        void StopAudio(int audioHandle);
        void SpawnProjectile(GameObject prefab, Vector3 position, Quaternion rotation, IBehaviorUnit ownerData,
            float damageMultiplier, string numericKey, int targetingScopeId);
        void ApplyEffect(GameObject target, BehaviorEffectAsset effectDefinition, GameObject source);
        void ExecuteEffect(BehaviorEffectAsset effectDefinition, IBehaviorUnit ownerData, Vector3 origin, GameObject source);
        void ShakeCamera(float amplitude, float frequency, float duration);
        float CalculateDamage(IBehaviorUnit attacker, IBehaviorUnit defender, float multiplier, string numericKey);
    }
}
