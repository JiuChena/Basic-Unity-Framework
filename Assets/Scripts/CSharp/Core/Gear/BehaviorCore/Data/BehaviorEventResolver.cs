using System;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 行为事件类型解析与旧数据修正工具。
    /// 兼容历史上因枚举顺序调整导致的错误序列化值。
    /// </summary>
    public static class BehaviorEventResolver
    {
        private static readonly System.Collections.Generic.Dictionary<int, bool> ProjectilePrefabContractCache =
            new System.Collections.Generic.Dictionary<int, bool>(16);

        public static BehaviorEventType ResolveEffectiveType(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent == null)
                return BehaviorEventType.SpawnVFX;

            if (!string.IsNullOrWhiteSpace(behaviorEvent.targetObjectPath))
                return BehaviorEventType.SetObjectActive;

            if (behaviorEvent.audioRef != null)
                return BehaviorEventType.PlayAudio;

            if (behaviorEvent.gameplayEffectRef != null)
                return BehaviorEventType.ExecuteGameplayEffect;

            if (behaviorEvent.buffRef != null)
            {
                if (behaviorEvent.type == BehaviorEventType.ApplySelfBuff)
                    return BehaviorEventType.ApplySelfBuff;

                if (behaviorEvent.type == BehaviorEventType.ApplyBuff)
                    return BehaviorEventType.ApplyBuff;

                return ContainsSelfHint(behaviorEvent.authoringTrackName)
                    ? BehaviorEventType.ApplySelfBuff
                    : BehaviorEventType.ApplyBuff;
            }

            if (behaviorEvent.prefabRef != null && PrefabSupportsProjectileContract(behaviorEvent.prefabRef))
                return BehaviorEventType.SpawnProjectile;

            if (behaviorEvent.type == BehaviorEventType.SpawnProjectile)
                return BehaviorEventType.SpawnProjectile;

            if (behaviorEvent.cameraShakeDuration > 0f ||
                behaviorEvent.cameraShakeAmplitude > 0f ||
                behaviorEvent.cameraShakeFrequency > 0f)
            {
                return BehaviorEventType.CameraShake;
            }

            if (behaviorEvent.prefabRef != null)
                return BehaviorEventType.SpawnVFX;

            return behaviorEvent.type;
        }

        public static bool NormalizeInPlace(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent == null)
                return false;

            BehaviorEventType resolvedType = ResolveEffectiveType(behaviorEvent);
            if (behaviorEvent.type == resolvedType)
                return false;

            behaviorEvent.type = resolvedType;
            return true;
        }

        public static BehaviorEvent CreateNormalizedClone(BehaviorEvent source, float timelineStartTime,
            string trackName = null)
        {
            BehaviorEvent cloned = new BehaviorEvent();
            if (source != null)
            {
                cloned.authoringTrackName = !string.IsNullOrWhiteSpace(trackName)
                    ? trackName
                    : source.authoringTrackName;
                cloned.type = source.type;
                cloned.referenceBone = source.referenceBone;
                cloned.positionOffset = source.positionOffset;
                cloned.rotationOffset = source.rotationOffset;
                cloned.scaleOffset = source.scaleOffset;
                cloned.prefabRef = source.prefabRef;
                cloned.targetObjectPath = source.targetObjectPath;
                cloned.activeState = source.activeState;
                cloned.autoRecycleTime = source.autoRecycleTime;
                cloned.audioRef = source.audioRef;
                cloned.audioLoop = source.audioLoop;
                cloned.audioVolume = source.audioVolume;
                cloned.buffRef = source.buffRef;
                cloned.numericKey = source.numericKey;
                cloned.gameplayEffectRef = source.gameplayEffectRef;
                cloned.damageMultiplier = source.damageMultiplier;
                cloned.cameraShakeAmplitude = source.cameraShakeAmplitude;
                cloned.cameraShakeFrequency = source.cameraShakeFrequency;
                cloned.cameraShakeDuration = source.cameraShakeDuration;
            }

            cloned.time = Mathf.Max(0f, timelineStartTime);
            NormalizeInPlace(cloned);
            return cloned;
        }

        public static bool PrefabSupportsProjectileContract(GameObject prefab)
        {
            if (prefab == null)
                return false;

            int prefabId = prefab.GetInstanceID();
            if (ProjectilePrefabContractCache.TryGetValue(prefabId, out bool cachedResult))
                return cachedResult;

            bool supportsProjectileContract =
                prefab.GetComponent<IBehaviorProjectileContract>() != null &&
                prefab.GetComponent<IProjectileLaunchHandler>() != null;
            ProjectilePrefabContractCache[prefabId] = supportsProjectileContract;
            return supportsProjectileContract;
        }

        private static bool ContainsSelfHint(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            return rawValue.IndexOf("self", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   rawValue.IndexOf("自身", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   rawValue.IndexOf("自施加", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
