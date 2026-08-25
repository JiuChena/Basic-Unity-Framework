using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorCore
{
    internal static class BehaviorEditorContext
    {
        private static GameObject referenceRootObject;
        private static bool showAuthoringHitboxGizmos = true;
        private static BehaviorTimelineHitboxClipAsset selectedHitboxClipAsset;
        private static int hitboxScenePreviewRetainCount;

        public static GameObject ReferenceRootObject
        {
            get => referenceRootObject;
            set => referenceRootObject = value;
        }

        public static Transform ReferenceRootTransform => referenceRootObject != null ? referenceRootObject.transform : null;

        public static bool ShowAuthoringHitboxGizmos
        {
            get => showAuthoringHitboxGizmos;
            set => showAuthoringHitboxGizmos = value;
        }

        public static BehaviorTimelineHitboxClipAsset SelectedHitboxClipAsset
        {
            get => selectedHitboxClipAsset;
            set => selectedHitboxClipAsset = value;
        }

        public static void RetainHitboxScenePreview()
        {
            hitboxScenePreviewRetainCount = Mathf.Max(0, hitboxScenePreviewRetainCount) + 1;
            BehaviorHitboxScenePreview.SetRegistered(true);
        }

        public static void ReleaseHitboxScenePreview()
        {
            hitboxScenePreviewRetainCount = Mathf.Max(0, hitboxScenePreviewRetainCount - 1);
            if (hitboxScenePreviewRetainCount == 0)
                BehaviorHitboxScenePreview.SetRegistered(false);
        }
    }

    internal static class BehaviorHitboxScenePreview
    {
        private static bool isRegistered;

        public static void SetRegistered(bool shouldRegister)
        {
            if (shouldRegister)
            {
                if (isRegistered)
                    return;

                UnityEditor.SceneView.duringSceneGui += OnSceneGui;
                isRegistered = true;
                return;
            }

            if (!isRegistered)
                return;

            UnityEditor.SceneView.duringSceneGui -= OnSceneGui;
            isRegistered = false;
        }

        private static void OnSceneGui(UnityEditor.SceneView sceneView)
        {
            if (!BehaviorEditorContext.ShowAuthoringHitboxGizmos)
                return;

            BehaviorTimelineHitboxClipAsset hitboxClipAsset = BehaviorEditorContext.SelectedHitboxClipAsset;
            if (hitboxClipAsset == null || hitboxClipAsset.hitboxData == null)
                return;

            Transform referenceRoot = BehaviorEditorContext.ReferenceRootTransform;
            if (referenceRoot == null)
                return;

            DrawHitboxPreview(hitboxClipAsset.hitboxData, referenceRoot);
        }

        private static void DrawHitboxPreview(HitboxDef hitbox, Transform referenceRoot)
        {
            if (hitbox == null || referenceRoot == null)
                return;

            ResolvePreviewPose(hitbox, referenceRoot, out Vector3 center, out Quaternion rotation, out Vector3 size);

            Color fillColor = new Color(1f, 0.25f, 0.25f, 0.08f);
            Color wireColor = new Color(1f, 0.3f, 0.3f, 0.95f);

            using (new UnityEditor.Handles.DrawingScope(wireColor, Matrix4x4.TRS(center, rotation, Vector3.one)))
            {
                UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                switch (hitbox.shape)
                {
                    case HitboxShape.Sphere:
                    {
                        float radius = Mathf.Abs(size.x);
                        UnityEditor.Handles.color = fillColor;
                        UnityEditor.Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, radius * 2f, EventType.Repaint);
                        UnityEditor.Handles.color = wireColor;
                        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
                        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.right, radius);
                        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
                        break;
                    }

                    case HitboxShape.Capsule:
                    {
                        DrawWireCapsule(size, wireColor, fillColor);
                        break;
                    }

                    case HitboxShape.Box:
                    default:
                    {
                        UnityEditor.Handles.color = fillColor;
                        UnityEditor.Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                        UnityEditor.Handles.color = wireColor;
                        UnityEditor.Handles.DrawWireCube(Vector3.zero, size);
                        break;
                    }
                }
            }

            string label = string.IsNullOrWhiteSpace(hitbox.name) ? "Hitbox Preview" : hitbox.name;
            UnityEditor.Handles.Label(center, $"{label}\n{hitbox.shape}  Size={size}");
        }

        private static void ResolvePreviewPose(HitboxDef hitbox, Transform referenceRoot,
            out Vector3 center, out Quaternion rotation, out Vector3 size)
        {
            if (string.IsNullOrWhiteSpace(hitbox.referenceBone))
            {
                center = hitbox.positionOffset;
                rotation = Quaternion.Euler(hitbox.rotationOffset);
                size = Vector3.Scale(hitbox.size, hitbox.scaleOffset);
                return;
            }

            Transform referenceTransform = BehaviorReferenceBoneEditorUtility.FindChildByPath(referenceRoot, hitbox.referenceBone);
            Transform resolvedTransform = referenceTransform != null ? referenceTransform : referenceRoot;

            center = resolvedTransform.TransformPoint(hitbox.positionOffset);
            rotation = resolvedTransform.rotation * Quaternion.Euler(hitbox.rotationOffset);
            size = Vector3.Scale(hitbox.size, Vector3.Scale(resolvedTransform.lossyScale, hitbox.scaleOffset));
        }

        private static void DrawWireCapsule(Vector3 size, Color wireColor, Color fillColor)
        {
            float radius = Mathf.Abs(size.x);
            float totalHeight = Mathf.Max(radius * 2f, Mathf.Abs(size.y));
            float cylinderHeight = Mathf.Max(0f, totalHeight - radius * 2f);
            Vector3 topCenter = Vector3.up * (cylinderHeight * 0.5f);
            Vector3 bottomCenter = Vector3.down * (cylinderHeight * 0.5f);

            UnityEditor.Handles.color = wireColor;
            UnityEditor.Handles.DrawWireDisc(topCenter, Vector3.up, radius);
            UnityEditor.Handles.DrawWireDisc(bottomCenter, Vector3.up, radius);
            UnityEditor.Handles.DrawWireArc(topCenter, Vector3.forward, Vector3.left, 180f, radius);
            UnityEditor.Handles.DrawWireArc(topCenter, Vector3.right, Vector3.forward, 180f, radius);
            UnityEditor.Handles.DrawWireArc(bottomCenter, Vector3.forward, Vector3.right, 180f, radius);
            UnityEditor.Handles.DrawWireArc(bottomCenter, Vector3.right, Vector3.back, 180f, radius);

            Vector3[] sideOffsets =
            {
                Vector3.left * radius,
                Vector3.right * radius,
                Vector3.forward * radius,
                Vector3.back * radius
            };

            for (int i = 0; i < sideOffsets.Length; i++)
                UnityEditor.Handles.DrawLine(topCenter + sideOffsets[i], bottomCenter + sideOffsets[i]);

            if (cylinderHeight <= 0f)
            {
                UnityEditor.Handles.color = fillColor;
                UnityEditor.Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, radius * 2f, EventType.Repaint);
            }
        }
    }

    public static class BehaviorReferenceBoneEditorUtility
    {
        public const string WorldOptionLabel = "<World>";

        public static string FormatReferenceBoneLabel(string referenceBone)
        {
            return string.IsNullOrWhiteSpace(referenceBone) ? WorldOptionLabel : referenceBone;
        }

        public static string[] BuildReferenceBoneOptions(Transform root)
        {
            if (root == null)
                return new[] { WorldOptionLabel };

            List<string> options = new List<string> { WorldOptionLabel, root.name };
            AppendReferenceBoneOptions(root, root.name, options);
            return options.ToArray();
        }

        public static bool TryBuildRelativeBonePath(Transform root, Transform target, out string referenceBone)
        {
            if (target == null)
            {
                referenceBone = string.Empty;
                return false;
            }

            if (root == null)
            {
                referenceBone = string.Empty;
                return true;
            }

            referenceBone = BuildRelativeBonePath(root, target);
            return !string.IsNullOrWhiteSpace(referenceBone);
        }

        public static string BuildRelativeBonePath(Transform root, Transform target)
        {
            if (root == null || target == null)
                return string.Empty;

            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Add(current.name);
                if (current == root)
                    break;

                current = current.parent;
            }

            if (current != root)
                return string.Empty;

            parts.Reverse();
            return string.Join("/", parts);
        }

        public static Transform FindChildByPath(Transform root, string path)
        {
            if (root == null)
                return null;

            if (string.IsNullOrWhiteSpace(path))
                return root;

            string[] parts = path.Split('/');
            int startIndex = 0;
            if (parts.Length > 0 && string.Equals(parts[0], root.name, StringComparison.Ordinal))
                startIndex = 1;

            Transform current = root;
            for (int i = startIndex; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                current = current.Find(parts[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        private static void AppendReferenceBoneOptions(Transform current, string currentPath, List<string> options)
        {
            if (current == null || options == null)
                return;

            for (int i = 0; i < current.childCount; i++)
            {
                Transform child = current.GetChild(i);
                string childPath = $"{currentPath}/{child.name}";
                options.Add(childPath);
                AppendReferenceBoneOptions(child, childPath, options);
            }
        }
    }

}
