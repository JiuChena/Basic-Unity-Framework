#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using CoreFramework;

namespace CoreFrameworkEditor
{
    [CustomEditor(typeof(BaseInputProvider), true)]
    public class PlayerInputProviderEditor : Editor
    {
#if ENABLE_INPUT_SYSTEM
        private static readonly (string fieldName, string displayName)[] ActionBindings =
        {
            ("_moveActionName",    "移动 (Move)"),
            ("_lookActionName",    "视角 (Look)"),
            ("_jumpActionName",    "跳跃 (Jump)"),
            ("_sprintActionName",  "冲刺 (Sprint)"),
            ("_crouchActionName",  "下蹲 (Crouch)"),
            ("_attackActionName",  "普攻 (Attack)"),
            ("_aimActionName",     "瞄准 (Aim)"),
            ("_reloadActionName",  "装填 (Reload)"),
            ("_interactActionName","交互 (Interact)"),
        };
#endif

        private static readonly (string fieldName, string displayName)[] LegacyKeyBindings =
        {
            ("_moveForwardKey", "前进"),
            ("_moveBackKey",    "后退"),
            ("_moveLeftKey",    "左移"),
            ("_moveRightKey",   "右移"),
            ("_jumpKey",        "跳跃"),
            ("_sprintKey",      "冲刺"),
            ("_crouchKey",      "下蹲"),
            ("_attackKey",      "普攻"),
            ("_aimKey",         "瞄准"),
            ("_reloadKey",      "装填"),
            ("_interactKey",    "交互"),
        };

        private bool _foldoutActionBindings = true;
        private bool _foldoutLegacy = true;
        private bool _foldoutPipeline = true;
#if ENABLE_INPUT_SYSTEM
        private string[] _availableActions = { };
#endif

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            RefreshActionList();
#endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptHeader();
            DrawSensitivity();
            DrawInputPipeline();
            DrawActionBindings();
            DrawLegacyBindings();
            DrawSubclassProperties();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptHeader()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
        }

        private void DrawSensitivity()
        {
            var sp = serializedObject.FindProperty("lookSensitivity");
            if (sp != null)
                EditorGUILayout.PropertyField(sp);
            EditorGUILayout.Space();
        }

        private void DrawInputPipeline()
        {
            _foldoutPipeline = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPipeline, "逻辑输入管线");
            if (!_foldoutPipeline)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;
            SerializedProperty profile = serializedObject.FindProperty("_bindingProfile");
            if (profile != null)
                EditorGUILayout.PropertyField(profile, new GUIContent("Binding Profile"));

            if (profile == null || profile.objectReferenceValue == null)
                EditorGUILayout.HelpBox("未配置 Profile：将使用下方的兼容动作名称和 Legacy 键位读取链路。", MessageType.Info);
            else
                EditorGUILayout.HelpBox("已配置 Profile：PlayerInputProvider 将使用逻辑动作状态与 Context Mapper 写入领域数据槽。", MessageType.Info);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Input Action 绑定 ──

        private void DrawActionBindings()
        {
#if ENABLE_INPUT_SYSTEM
            _foldoutActionBindings = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutActionBindings, "Input Action 绑定");
            if (!_foldoutActionBindings)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;

            var provider = (BaseInputProvider)target;
            var playerInput = provider.GetComponent<PlayerInput>();
            var hasActions = playerInput != null && playerInput.actions != null;

            EditorGUILayout.HelpBox(
                hasActions ? $"当前输入资产: {playerInput.actions.name}" : "未找到 PlayerInput 组件或 InputActionAsset。绑定无效时将自动使用 Legacy 回退。",
                hasActions ? MessageType.Info : MessageType.Warning);

            RefreshActionList();

            for (int i = 0; i < ActionBindings.Length; i++)
            {
                DrawActionBindingRow(ActionBindings[i].fieldName, ActionBindings[i].displayName);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
#else
            EditorGUILayout.HelpBox("Input System 未启用 (ENABLE_INPUT_SYSTEM)。安装 com.unity.inputsystem 包以使用 Input Action 绑定。", MessageType.Info);
            EditorGUILayout.Space();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void DrawActionBindingRow(string fieldName, string displayName)
        {
            var sp = serializedObject.FindProperty(fieldName);
            if (sp == null) return;

            string currentValue = sp.stringValue;
            int currentIndex = System.Array.IndexOf(_availableActions, currentValue);
            if (currentIndex < 0) currentIndex = 0;

            EditorGUILayout.BeginHorizontal();

            int newIndex = EditorGUILayout.Popup(displayName, currentIndex, _availableActions);
            if (newIndex != currentIndex)
            {
                sp.stringValue = newIndex == 0 ? "" : _availableActions[newIndex];
            }

            if (!string.IsNullOrEmpty(currentValue) && System.Array.IndexOf(_availableActions, currentValue) < 0)
            {
                var warningRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
                GUI.Label(warningRect, EditorGUIUtility.IconContent("Warning"));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshActionList()
        {
            var provider = (BaseInputProvider)target;
            var playerInput = provider != null ? provider.GetComponent<PlayerInput>() : null;
            var actions = playerInput != null ? playerInput.actions : null;

            var names = new List<string> { "None (使用 Legacy)" };

            if (actions != null)
            {
                foreach (var map in actions.actionMaps)
                {
                    foreach (var action in map.actions)
                    {
                        names.Add(action.name);
                    }
                }
            }

            _availableActions = names.ToArray();
        }
#endif

        // ── Legacy 键位绑定 ──

        private void DrawLegacyBindings()
        {
            _foldoutLegacy = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutLegacy, "Legacy 键位绑定");
            if (!_foldoutLegacy)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;

#if ENABLE_LEGACY_INPUT_MANAGER
            EditorGUILayout.HelpBox("仅在使用 Legacy Input Manager 回退时生效。Input System 路径优先。", MessageType.Info);

            for (int i = 0; i < LegacyKeyBindings.Length; i++)
            {
                var sp = serializedObject.FindProperty(LegacyKeyBindings[i].fieldName);
                if (sp != null)
                {
                    sp.intValue = (int)(KeyCode)EditorGUILayout.EnumPopup(
                        LegacyKeyBindings[i].displayName,
                        (KeyCode)sp.intValue);
                }
            }

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("滚轮滚动", "Mouse Scroll (不可配置)");
            EditorGUILayout.LabelField("角色切换 1-4", "Alpha1-4 (不可配置)");
            EditorGUI.EndDisabledGroup();
#else
            EditorGUILayout.HelpBox("Legacy Input Manager 未启用 (ENABLE_LEGACY_INPUT_MANAGER)。", MessageType.Info);
#endif

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── 子类自有属性 ──

        private bool _foldoutSubclass = true;

        private void DrawSubclassProperties()
        {
            var handled = new HashSet<string>
            {
                "m_Script",
                "lookSensitivity",
                "_bindingProfile",
            };
#if ENABLE_INPUT_SYSTEM
            foreach (var binding in ActionBindings)
                handled.Add(binding.fieldName);
#endif
            foreach (var binding in LegacyKeyBindings)
                handled.Add(binding.fieldName);

            // 收集未被处理的属性
            var remaining = new List<SerializedProperty>();
            SerializedProperty iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true)) return;

            do
            {
                if (!handled.Contains(iterator.name))
                    remaining.Add(iterator.Copy());
            }
            while (iterator.NextVisible(false));

            if (remaining.Count == 0) return;

            // 折叠组包装
            string subclassName = target.GetType().Name;
            _foldoutSubclass = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSubclass, $"{subclassName} 属性");
            if (!_foldoutSubclass)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;

            // 跟踪当前 Header，重现分组
            string currentHeader = null;
            var targetType = target.GetType();

            foreach (var sp in remaining)
            {
                // 反射读取 [Header] 属性
                var field = targetType.GetField(sp.name,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                string header = null;
                if (field != null)
                {
                    var headerAttr = field.GetCustomAttribute<HeaderAttribute>();
                    if (headerAttr != null) header = headerAttr.header;
                }

                // Header 变化时输出标签
                if (header != currentHeader)
                {
                    currentHeader = header;
                    if (!string.IsNullOrEmpty(header))
                        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
                }

                // KeyCode 特殊处理 → EnumPopup
                if (sp.propertyType == SerializedPropertyType.Enum &&
                    field?.FieldType == typeof(KeyCode))
                {
                    sp.intValue = (int)(KeyCode)EditorGUILayout.EnumPopup(
                        sp.displayName,
                        (KeyCode)sp.intValue);
                }
                else
                {
                    EditorGUILayout.PropertyField(sp, true);
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
