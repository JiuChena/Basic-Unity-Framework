using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 行为运行时对单个 HitboxDef 的缓存结果。
    /// </summary>
    internal readonly struct ActiveHitbox
    {
        public HitboxDef Definition { get; }
        public Transform ReferenceTransform { get; }
        public bool UseWorldSpace { get; }

        public ActiveHitbox(HitboxDef definition, Transform referenceTransform)
        {
            Definition = definition;
            ReferenceTransform = referenceTransform;
            UseWorldSpace = definition == null || string.IsNullOrWhiteSpace(definition.referenceBone);
        }

        public bool IsActive(float elapsedTime)
        {
            return elapsedTime >= Definition.startTime && elapsedTime < Definition.startTime + Definition.duration;
        }

        public void GetWorldPose(Transform fallbackRoot, out Vector3 center, out Quaternion rotation, out Vector3 size)
        {
            if (UseWorldSpace)
            {
                center = Definition.positionOffset;
                rotation = Quaternion.Euler(Definition.rotationOffset);
                size = Vector3.Scale(Definition.size, Definition.scaleOffset);
                return;
            }

            Transform reference = ReferenceTransform != null ? ReferenceTransform : fallbackRoot;

            center = reference.TransformPoint(Definition.positionOffset);
            rotation = reference.rotation * Quaternion.Euler(Definition.rotationOffset);
            size = Vector3.Scale(Definition.size, Vector3.Scale(reference.lossyScale, Definition.scaleOffset));
        }
    }
}
