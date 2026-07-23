using System;
using UnityEngine;

namespace BehaviorCore
{
    [UnityEditor.CustomEditor(typeof(BehaviorTimelineHitboxClipAsset))]
    public sealed class BehaviorTimelineHitboxClipAssetEditor : UnityEditor.Editor
    {
        private const float MinimumHitboxSize = 0.1f;
        private Transform referenceBoneTarget;

        private void OnEnable()
        {
            BehaviorEditorContext.RetainHitboxScenePreview();
        }

        private void OnDisable()
        {
            if (BehaviorEditorContext.SelectedHitboxClipAsset == target)
                BehaviorEditorContext.SelectedHitboxClipAsset = null;

            BehaviorEditorContext.ReleaseHitboxScenePreview();
            UnityEditor.SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            BehaviorEditorContext.SelectedHitboxClipAsset = target as BehaviorTimelineHitboxClipAsset;
            serializedObject.Update();
            UnityEditor.EditorGUI.BeginChangeCheck();

            UnityEditor.SerializedProperty hitboxDataProperty = serializedObject.FindProperty("hitboxData");
            if (hitboxDataProperty == null)
            {
                DrawDefaultInspector();
                bool hasDefaultChanges = serializedObject.ApplyModifiedProperties();
                if (hasDefaultChanges)
                    UnityEditor.SceneView.RepaintAll();
                return;
            }

            UnityEditor.EditorGUILayout.HelpBox(
                "Hitbox 的生效时间和持续时间取自 Timeline 片段本身，这里只编辑形状、挂点、数值和命中效果。",
                UnityEditor.MessageType.None);

            bool showPreview = UnityEditor.EditorGUILayout.ToggleLeft(
                "Show Scene Hitbox Preview",
                BehaviorEditorContext.ShowAuthoringHitboxGizmos);
            if (showPreview != BehaviorEditorContext.ShowAuthoringHitboxGizmos)
            {
                BehaviorEditorContext.ShowAuthoringHitboxGizmos = showPreview;
                UnityEditor.SceneView.RepaintAll();
            }

            if (BehaviorEditorContext.ReferenceRootTransform == null)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "当前没有可用的 Reference Root，Scene 里的 Hitbox 预览不会显示。请先在 Behavior Editor Timeline 窗口中指定 Reference Root 并开始作者期编辑。",
                    UnityEditor.MessageType.Info);
            }

            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("name"));
            DrawShapeField(hitboxDataProperty);
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("hitGroupId"));

            BehaviorTimelineEventClipAssetEditor.DrawTransformBindingFields(
                hitboxDataProperty,
                ref referenceBoneTarget);
            DrawShapeSpecificSizeFields(hitboxDataProperty);

            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Damage", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("numericKey"));
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("damageMultiplier"));
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("hitStunDuration"));
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("knockbackForce"));
            UnityEditor.EditorGUILayout.PropertyField(hitboxDataProperty.FindPropertyRelative("onHitBuff"));

            bool uiChanged = UnityEditor.EditorGUI.EndChangeCheck();
            bool applied = serializedObject.ApplyModifiedProperties();
            if (uiChanged || applied)
                UnityEditor.SceneView.RepaintAll();
        }

        private static void DrawShapeField(UnityEditor.SerializedProperty hitboxDataProperty)
        {
            if (!TryGetShapeAndSizeProperties(
                    hitboxDataProperty,
                    out UnityEditor.SerializedProperty shapeProperty,
                    out UnityEditor.SerializedProperty sizeProperty))
                return;

            UnityEditor.EditorGUI.BeginChangeCheck();
            UnityEditor.EditorGUILayout.PropertyField(shapeProperty);
            if (!UnityEditor.EditorGUI.EndChangeCheck())
                return;

            NormalizeSizeForShape((HitboxShape)shapeProperty.enumValueIndex, sizeProperty);
        }

        private static void DrawShapeSpecificSizeFields(UnityEditor.SerializedProperty hitboxDataProperty)
        {
            if (!TryGetShapeAndSizeProperties(
                    hitboxDataProperty,
                    out UnityEditor.SerializedProperty shapeProperty,
                    out UnityEditor.SerializedProperty sizeProperty))
                return;

            HitboxShape shape = (HitboxShape)shapeProperty.enumValueIndex;
            switch (shape)
            {
                case HitboxShape.Sphere:
                    DrawSphereSizeFields(sizeProperty);
                    break;

                case HitboxShape.Capsule:
                    DrawCapsuleSizeFields(sizeProperty);
                    break;

                case HitboxShape.Box:
                default:
                    DrawBoxSizeFields(sizeProperty);
                    break;
            }
        }

        private static void DrawBoxSizeFields(UnityEditor.SerializedProperty sizeProperty)
        {
            Vector3 currentSize = sizeProperty.vector3Value;
            Vector3 nextSize = UnityEditor.EditorGUILayout.Vector3Field("Box Size", currentSize);
            nextSize.x = Mathf.Max(0f, nextSize.x);
            nextSize.y = Mathf.Max(0f, nextSize.y);
            nextSize.z = Mathf.Max(0f, nextSize.z);
            sizeProperty.vector3Value = nextSize;
        }

        private static bool TryGetShapeAndSizeProperties(
            UnityEditor.SerializedProperty hitboxDataProperty,
            out UnityEditor.SerializedProperty shapeProperty,
            out UnityEditor.SerializedProperty sizeProperty)
        {
            shapeProperty = null;
            sizeProperty = null;
            if (hitboxDataProperty == null)
                return false;

            shapeProperty = hitboxDataProperty.FindPropertyRelative("shape");
            sizeProperty = hitboxDataProperty.FindPropertyRelative("size");
            return shapeProperty != null && sizeProperty != null;
        }

        private static void DrawSphereSizeFields(UnityEditor.SerializedProperty sizeProperty)
        {
            float radius = Mathf.Max(MinimumHitboxSize, sizeProperty.vector3Value.x);
            float nextRadius = Mathf.Max(MinimumHitboxSize, UnityEditor.EditorGUILayout.FloatField("Radius", radius));
            sizeProperty.vector3Value = new Vector3(nextRadius, nextRadius, nextRadius);
        }

        private static void DrawCapsuleSizeFields(UnityEditor.SerializedProperty sizeProperty)
        {
            Vector3 currentSize = sizeProperty.vector3Value;
            float radius = Mathf.Max(MinimumHitboxSize, currentSize.x);
            float height = Mathf.Max(radius * 2f, currentSize.y);

            float nextRadius = Mathf.Max(MinimumHitboxSize, UnityEditor.EditorGUILayout.FloatField("Radius", radius));
            float nextHeight = Mathf.Max(nextRadius * 2f,
                UnityEditor.EditorGUILayout.FloatField("Height", height));

            sizeProperty.vector3Value = new Vector3(nextRadius, nextHeight, nextRadius);
        }

        private static void NormalizeSizeForShape(HitboxShape shape, UnityEditor.SerializedProperty sizeProperty)
        {
            Vector3 size = sizeProperty.vector3Value;
            switch (shape)
            {
                case HitboxShape.Sphere:
                {
                    float radius = Mathf.Max(MinimumHitboxSize, ResolvePrimaryPositiveValue(size));
                    sizeProperty.vector3Value = new Vector3(radius, radius, radius);
                    break;
                }

                case HitboxShape.Capsule:
                {
                    float radius = Mathf.Max(MinimumHitboxSize, ResolvePrimaryPositiveValue(size));
                    float height = Mathf.Max(radius * 2f, Mathf.Abs(size.y));
                    sizeProperty.vector3Value = new Vector3(radius, height, radius);
                    break;
                }

                case HitboxShape.Box:
                default:
                {
                    Vector3 normalizedSize = new Vector3(
                        Mathf.Max(0f, size.x),
                        Mathf.Max(0f, size.y),
                        Mathf.Max(0f, size.z));
                    if (normalizedSize == Vector3.zero)
                        normalizedSize = Vector3.one * MinimumHitboxSize;
                    sizeProperty.vector3Value = normalizedSize;
                    break;
                }
            }
        }

        private static float ResolvePrimaryPositiveValue(Vector3 size)
        {
            if (size.x > 0f)
                return size.x;

            if (size.y > 0f)
                return size.y;

            if (size.z > 0f)
                return size.z;

            return 0f;
        }
    }

}
