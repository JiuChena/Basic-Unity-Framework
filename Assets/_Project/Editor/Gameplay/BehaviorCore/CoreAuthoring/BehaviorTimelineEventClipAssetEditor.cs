using System;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 行为事件时间轴片段的自定义 Inspector：按事件类型显示对应字段并支持骨骼路径读取。
    /// </summary>
    [UnityEditor.CustomEditor(typeof(BehaviorTimelineEventClipAsset))]
    public sealed class BehaviorTimelineEventClipAssetEditor : UnityEditor.Editor
    {
        // 当前选中的骨骼 Transform，用于读取骨骼相对路径。
        private Transform referenceBoneTarget;

        // 该轨道支持手动配置的事件类型（音频已改为原生 AudioTrack，不在此列）。
        private static readonly BehaviorEventType[] SupportedEventTypes =
        {
            BehaviorEventType.SpawnVFX,
            BehaviorEventType.SpawnProjectile,
            BehaviorEventType.ApplyBuff,
            BehaviorEventType.ApplySelfBuff,
            BehaviorEventType.ExecuteGameplayEffect,
            BehaviorEventType.CameraShake,
        };

        /// <summary>
        /// 绘制事件属性面板：类型选择、挂点绑定、公共引用和类型专属字段。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UnityEditor.SerializedProperty eventDataProperty = serializedObject.FindProperty("eventData");
            if (eventDataProperty == null)
            {
                // 序列化结构缺失时回退到默认面板。
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // 音频事件已废弃，给出提示并引导使用原生 AudioTrack。
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

        /// <summary>
        /// 绘制受支持事件类型的下拉选择框。
        /// </summary>
        /// <param name="typeProperty">事件类型序列化属性。</param>
        /// <param name="currentType">当前事件类型。</param>
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

            // 音频类型不在支持列表中，选择索引回退到第一个。
            if (currentType == BehaviorEventType.PlayAudio)
                selectedIndex = 0;

            int nextIndex = UnityEditor.EditorGUILayout.Popup("Type", selectedIndex, options);
            typeProperty.intValue = (int)SupportedEventTypes[Mathf.Clamp(nextIndex, 0, SupportedEventTypes.Length - 1)];
        }

        /// <summary>
        /// 绘制挂点绑定字段：骨骼路径、位置/旋转/缩放偏移。
        /// </summary>
        /// <param name="eventDataProperty">事件数据序列化属性。</param>
        /// <param name="referenceBoneTarget">当前选中的骨骼 Transform 引用。</param>
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

        /// <summary>
        /// 绘制事件公共的数值引用字段。
        /// </summary>
        /// <param name="eventDataProperty">事件数据序列化属性。</param>
        private static void DrawCommonReferenceFields(UnityEditor.SerializedProperty eventDataProperty)
        {
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Numeric", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("numericKey"));
            UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("damageMultiplier"));
        }

        /// <summary>
        /// 按事件类型绘制专属负载字段。
        /// </summary>
        /// <param name="eventDataProperty">事件数据序列化属性。</param>
        /// <param name="eventType">当前事件类型。</param>
        private static void DrawTypeSpecificFields(UnityEditor.SerializedProperty eventDataProperty,
            BehaviorEventType eventType)
        {
            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Payload", UnityEditor.EditorStyles.boldLabel);

            switch (eventType)
            {
                case BehaviorEventType.SpawnVFX:
                    // VFX 需要预制体引用和自动回收时间。
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("prefabRef"));
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("autoRecycleTime"));
                    break;

                case BehaviorEventType.SpawnProjectile:
                    // 投射物需要预制体引用。
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("prefabRef"));
                    break;

                case BehaviorEventType.ApplyBuff:
                case BehaviorEventType.ApplySelfBuff:
                    // 施加 Buff 需要效果资产引用。
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("buffRef"));
                    break;

                case BehaviorEventType.ExecuteGameplayEffect:
                    // 执行玩法效果需要效果资产引用。
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("gameplayEffectRef"));
                    break;

                case BehaviorEventType.CameraShake:
                    // 相机震动需要幅度、频率和持续时间。
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("cameraShakeAmplitude"));
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("cameraShakeFrequency"));
                    UnityEditor.EditorGUILayout.PropertyField(eventDataProperty.FindPropertyRelative("cameraShakeDuration"));
                    break;
            }
        }

        /// <summary>
        /// 绘制骨骼路径读取工具：读取目标骨骼路径、使用世界坐标、快速选择下拉。
        /// </summary>
        /// <param name="referenceBoneProperty">骨骼路径序列化属性。</param>
        /// <param name="referenceBoneTarget">当前选中的骨骼 Transform 引用。</param>
        internal static void DrawReferenceBoneAuthoringTools(UnityEditor.SerializedProperty referenceBoneProperty,
            ref Transform referenceBoneTarget)
        {
            Transform referenceRoot = BehaviorEditorContext.ReferenceRootTransform;
            if (referenceBoneProperty == null)
                return;

            // 未指定 Reference Root 时提示先到编辑器窗口指定。
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

            // 目标骨骼不在 Reference Root 层级下时给出警告。
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
                // 从选中骨骼读取相对路径。
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

                // 切换为世界坐标（清空骨骼路径）。
                if (GUILayout.Button("Use World"))
                {
                    referenceBoneProperty.stringValue = string.Empty;
                    referenceBoneTarget = null;
                }
            }

            // 快速选择下拉：列出全部骨骼路径，当前值缺失时附加占位项。
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

        /// <summary>
        /// 在选项数组中查找当前骨骼路径的索引。
        /// </summary>
        /// <param name="options">骨骼路径选项数组。</param>
        /// <param name="currentValue">当前骨骼路径值。</param>
        /// <returns>匹配索引；未找到时返回 -1。</returns>
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

        /// <summary>
        /// 根据选中索引解析骨骼路径值；选中缺失占位项时保留原缺失值。
        /// </summary>
        /// <param name="options">骨骼路径选项数组。</param>
        /// <param name="selectedIndex">选中索引。</param>
        /// <param name="missingValue">当前缺失的骨骼路径值。</param>
        /// <returns>解析后的骨骼路径值。</returns>
        private static string ResolveReferenceBoneOptionValue(string[] options, int selectedIndex, string missingValue)
        {
            if (options == null || options.Length == 0 || selectedIndex < 0 || selectedIndex >= options.Length)
                return string.Empty;

            // 选中缺失占位项时写回原缺失值。
            if (!string.IsNullOrWhiteSpace(missingValue) &&
                selectedIndex == options.Length - 1 &&
                string.Equals(options[selectedIndex], $"(Missing: {missingValue})", StringComparison.Ordinal))
            {
                return missingValue;
            }

            return selectedIndex == 0 ? string.Empty : options[selectedIndex];
        }

        /// <summary>
        /// 将骨骼路径字符串解析为对应 Transform 并同步到缓存字段。
        /// </summary>
        /// <param name="referenceBoneProperty">骨骼路径序列化属性。</param>
        /// <param name="referenceBoneTarget">需要同步的骨骼 Transform 引用。</param>
        internal static void SyncReferenceBoneTarget(UnityEditor.SerializedProperty referenceBoneProperty,
            ref Transform referenceBoneTarget)
        {
            Transform referenceRoot = BehaviorEditorContext.ReferenceRootTransform;
            if (referenceBoneProperty == null || referenceRoot == null)
            {
                referenceBoneTarget = null;
                return;
            }

            // 路径为空表示世界坐标，无骨骼可同步。
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
