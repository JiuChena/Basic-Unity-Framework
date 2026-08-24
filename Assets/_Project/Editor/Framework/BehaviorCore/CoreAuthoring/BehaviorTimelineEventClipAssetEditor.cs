using System;
using UnityEngine;

namespace BehaviorCore
{
    [UnityEditor.CustomEditor(typeof(BehaviorTimelineEventClipAsset))]
    public sealed class BehaviorTimelineEventClipAssetEditor : UnityEditor.Editor
    {
        private Transform referenceBoneTarget;

        private static readonly BehaviorEventType[] SupportedEventTypes =
        {
            BehaviorEventType.SpawnVFX,
            BehaviorEventType.SpawnProjectile,
            BehaviorEventType.ApplyBuff,
            BehaviorEventType.ApplySelfBuff,
            BehaviorEventType.ExecuteGameplayEffect,
            BehaviorEventType.CameraShake,
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UnityEditor.SerializedProperty eventDataProperty = serializedObject.FindProperty("eventData");
            if (eventDataProperty == null)
            {
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            UnityEditor.SerializedProperty typeProperty = eventDataProperty.FindPropertyRelative("type");
            BehaviorEventType currentType = (BehaviorEventType)typeProperty.intValue;
            if (currentType == BehaviorEventType.PlayAudio)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "Behavior Events 轨道已不再支持手动配置音频事件。请改用原生 AudioTrack；当前片段即使保留为 PlayAudio，导出时也会被跳过。",
                    UnityEditor.MessageType.Warning);
            }

            DrawSupportedEventTypePopup(typeProperty, currentType);

            DrawTransformBindingFields(eventDataProperty, ref referenceBoneTarget);
            DrawCommonReferenceFields(eventDataProperty);
            DrawTypeSpecificFields(eventDataProperty, (BehaviorEventType)typeProperty.intValue);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSupportedEventTypePopup(UnityEditor.SerializedProperty typeProperty,
            BehaviorEventType currentType)
        {
            string[] options = new string[SupportedEventTypes.Length];
            int selectedIndex = 0;
            for (int i = 0; i < SupportedEventTypes.Length; i++)
            {
                options[i] = SupportedEventTypes[i].ToString();
                if (SupportedEventTypes[i] == currentType)
                    selectedIndex = i;
            }

            if (currentType == BehaviorEventType.PlayAudio)
                selectedIndex = 0;

            int nextIndex = UnityEditor.EditorGUILayout.Popup("Type", selectedIndex, options);
            typeProperty.intValue = (int)SupportedEventTypes[Mathf.Clamp(nextIndex, 0, SupportedEventTypes.Length - 1)];
        }

        internal static void DrawTransformBindingFields(UnityEditor.SerializedProperty eventDataProperty,
            ref Transform referenceBoneTarget)
        {
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Binding", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.SerializedProperty referenceBoneProperty =
                eventDataProperty.FindPropertyRelative("referenceBone");
            SyncReferenceBoneTarget(referenceBoneProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.PropertyField(referenceBoneProperty);
            DrawReferenceBoneAuthoringTools(referenceBoneProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("positionOffset"));
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("rotationOffset"));
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("scaleOffset"));
        }

        private static void DrawCommonReferenceFields(UnityEditor.SerializedProperty eventDataProperty)
        {
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Numeric", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("numericKey"));
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("damageMultiplier"));
        }

        private static void DrawTypeSpecificFields(UnityEditor.SerializedProperty eventDataProperty,
            BehaviorEventType eventType)
        {
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Payload", UnityEditor.EditorStyles.boldLabel);

            switch (eventType)
            {
                case BehaviorEventType.SpawnVFX:
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("prefabRef"));
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("autoRecycleTime"));
                    break;

                case BehaviorEventType.SpawnProjectile:
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("prefabRef"));
                    break;

                case BehaviorEventType.ApplyBuff:
                case BehaviorEventType.ApplySelfBuff:
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("buffRef"));
                    break;

                case BehaviorEventType.ExecuteGameplayEffect:
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("gameplayEffectRef"));
                    break;

                case BehaviorEventType.CameraShake:
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("cameraShakeAmplitude"));
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("cameraShakeFrequency"));
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("cameraShakeDuration"));
                    break;
            }
        }

        internal static void DrawReferenceBoneAuthoringTools(UnityEditor.SerializedProperty referenceBoneProperty,
            ref Transform referenceBoneTarget)
        {
            Transform referenceRoot = BehaviorEditorContext.ReferenceRootTransform;
            if (referenceBoneProperty == null)
                return;

            if (referenceRoot == null)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "当前没有可用的 Reference Root。请先到 Behavior Editor Timeline 窗口里指定角色根节点，再回到片段属性栏读取骨骼路径。",
                    UnityEditor.MessageType.Info);
                return;
            }

            SyncReferenceBoneTarget(referenceBoneProperty, ref referenceBoneTarget);
            UnityEditor.EditorGUILayout.LabelField("Reference Root", referenceRoot.name);
            referenceBoneTarget = (Transform)UnityEditor.EditorGUILayout.ObjectField(
                "Target Bone", referenceBoneTarget, typeof(Transform), true);

            if (referenceBoneTarget != null &&
                referenceBoneTarget != referenceRoot &&
                !referenceBoneTarget.IsChildOf(referenceRoot))
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "当前目标骨骼不在 Reference Root 的层级下，无法生成相对骨骼路径。",
                    UnityEditor.MessageType.Warning);
            }

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Read Path From Target"))
                {
                    if (referenceBoneTarget == null)
                    {
                        Debug.LogWarning("没有选择 Target Bone，无法读取骨骼路径。");
                    }
                    else if (BehaviorReferenceBoneEditorUtility.TryBuildRelativeBonePath(
                                 referenceRoot, referenceBoneTarget, out string resolvedPath))
                    {
                        referenceBoneProperty.stringValue = resolvedPath;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"目标骨骼 '{referenceBoneTarget.name}' 不在 Reference Root '{referenceRoot.name}' 的层级下，无法生成路径。");
                    }
                }

                if (GUILayout.Button("Use World"))
                {
                    referenceBoneProperty.stringValue = string.Empty;
                    referenceBoneTarget = null;
                }
            }

            string[] options = BehaviorReferenceBoneEditorUtility.BuildReferenceBoneOptions(referenceRoot);
            string missingValue = null;
            int currentIndex = ResolveReferenceBoneOptionIndex(options, referenceBoneProperty.stringValue);
            if (currentIndex < 0 && !string.IsNullOrWhiteSpace(referenceBoneProperty.stringValue))
            {
                missingValue = referenceBoneProperty.stringValue;
                Array.Resize(ref options, options.Length + 1);
                currentIndex = options.Length - 1;
                options[currentIndex] = $"(Missing: {missingValue})";
            }

            if (currentIndex < 0)
                currentIndex = 0;
            int nextIndex = UnityEditor.EditorGUILayout.Popup("Quick Select", currentIndex, options);
            if (nextIndex != currentIndex)
            {
                referenceBoneProperty.stringValue = ResolveReferenceBoneOptionValue(options, nextIndex, missingValue);
                SyncReferenceBoneTarget(referenceBoneProperty, ref referenceBoneTarget);
            }
        }

        private static int ResolveReferenceBoneOptionIndex(string[] options, string currentValue)
        {
            if (options == null || options.Length == 0)
                return -1;

            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], currentValue, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static string ResolveReferenceBoneOptionValue(string[] options, int selectedIndex, string missingValue)
        {
            if (options == null || options.Length == 0 || selectedIndex < 0 || selectedIndex >= options.Length)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(missingValue) &&
                selectedIndex == options.Length - 1 &&
                string.Equals(options[selectedIndex], $"(Missing: {missingValue})", StringComparison.Ordinal))
            {
                return missingValue;
            }

            return selectedIndex == 0 ? string.Empty : options[selectedIndex];
        }

        internal static void SyncReferenceBoneTarget(UnityEditor.SerializedProperty referenceBoneProperty,
            ref Transform referenceBoneTarget)
        {
            Transform referenceRoot = BehaviorEditorContext.ReferenceRootTransform;
            if (referenceBoneProperty == null || referenceRoot == null)
            {
                referenceBoneTarget = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(referenceBoneProperty.stringValue))
            {
                referenceBoneTarget = null;
                return;
            }

            referenceBoneTarget = BehaviorReferenceBoneEditorUtility.FindChildByPath(
                referenceRoot,
                referenceBoneProperty.stringValue);
        }
    }

}
