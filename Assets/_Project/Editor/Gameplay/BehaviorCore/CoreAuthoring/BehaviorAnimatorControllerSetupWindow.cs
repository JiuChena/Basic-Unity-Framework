#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// Behavior 正式 AnimatorController 创建工具。
    /// 提供低约束的正式创建流程：在任意目录创建或刷新一个自包含 Controller，并可直接分配到 Animator。
    /// </summary>
    internal sealed class BehaviorAnimatorControllerSetupWindow : EditorWindow
    {
        private string outputFolder = "Assets/Animations/Behavior";
        private string controllerName = "BehaviorController";
        private int layerCount = BehaviorAnimatorControllerConvention.DefaultLayerCount;
        private int slotsPerLayer = BehaviorAnimatorControllerConvention.DefaultSlotsPerLayer;

        [MenuItem("Framework/Behavior Editor/Animator Controller Setup")]
        private static void Open()
        {
            BehaviorAnimatorControllerSetupWindow window =
                GetWindow<BehaviorAnimatorControllerSetupWindow>("Behavior Controller");
            window.minSize = new Vector2(520f, 520f);
        }

        [MenuItem("Assets/Create/Framework/Behavior Editor/Authoring/Animator Controller", priority = 305)]
        private static void CreateControllerFromAssetsMenu()
        {
            string targetFolder = ResolveSelectedFolderPath();
            string uniqueControllerPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{targetFolder}/BehaviorController.controller");
            string controllerName = Path.GetFileNameWithoutExtension(uniqueControllerPath);
            string outputFolder = Path.GetDirectoryName(uniqueControllerPath)?.Replace("\\", "/") ?? targetFolder;

            AnimatorController controller = BehaviorAnimatorControllerAssetUtility.CreateOrUpdateController(
                outputFolder,
                controllerName,
                BehaviorAnimatorControllerConvention.DefaultLayerCount,
                BehaviorAnimatorControllerConvention.DefaultSlotsPerLayer);

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
            Debug.Log($"已创建 Behavior AnimatorController：{AssetDatabase.GetAssetPath(controller)}", controller);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Behavior AnimatorController", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "正式方案下，Behavior 的前 N 个 Animator Layer 作为保留槽位层使用。" +
                "这个工具只负责生成或刷新槽位外壳与占位动画；真正的行为内容仍然由项目侧行为配置与 BehaviorClip 决定。" +
                "它不会删除无关层，也不会触碰不匹配 Behavior 命名规则的其他状态。",
                MessageType.Info);

            GUILayout.Space(8f);
            DrawCreationSection();
        }

        private void DrawCreationSection()
        {
            EditorGUILayout.LabelField("Create Or Refresh", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "会在目标目录下生成一个自包含 Controller 和它自己的占位动画文件夹。" +
                "你可以按角色、按类别、按实验版本自由创建多个，不区分“通用”或“专属”。",
                MessageType.None);

            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
            layerCount = Mathf.Max(1, EditorGUILayout.IntField("Layer Count", layerCount));
            slotsPerLayer = Mathf.Clamp(EditorGUILayout.IntField("Slots Per Layer", slotsPerLayer), 1, 32);

            if (GUILayout.Button("Create Or Refresh Controller", GUILayout.Height(32f)))
            {
                AnimatorController controller = BehaviorAnimatorControllerAssetUtility.CreateOrUpdateController(
                    outputFolder,
                    controllerName,
                    layerCount,
                    slotsPerLayer);
                Selection.activeObject = controller;
                EditorGUIUtility.PingObject(controller);
            }
        }

        private static string ResolveSelectedFolderPath()
        {
            UnityEngine.Object activeObject = Selection.activeObject;
            if (activeObject == null)
                return "Assets";

            string assetPath = AssetDatabase.GetAssetPath(activeObject);
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Assets";

            if (AssetDatabase.IsValidFolder(assetPath))
                return assetPath;

            string folderPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            return string.IsNullOrWhiteSpace(folderPath) ? "Assets" : folderPath;
        }
    }

    internal static class BehaviorAnimatorControllerAssetUtility
    {
        public static AnimatorController CreateOrUpdateController(
            string outputFolder,
            string controllerName,
            int layerCount,
            int slotsPerLayer)
        {
            outputFolder = EnsureFolder(string.IsNullOrWhiteSpace(outputFolder)
                ? BehaviorAnimatorControllerConvention.DefaultSharedControllerFolder
                : outputFolder);
            controllerName = string.IsNullOrWhiteSpace(controllerName)
                ? BehaviorAnimatorControllerConvention.DefaultSharedControllerName
                : controllerName.Trim();
            layerCount = Mathf.Max(1, layerCount);
            slotsPerLayer = Mathf.Clamp(slotsPerLayer, 1, 32);

            string controllerPath = $"{outputFolder}/{controllerName}.controller";
            string placeholderFolder = EnsureFolder($"{outputFolder}/{controllerName}_Placeholders");

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            AnimationClip[,] placeholders = new AnimationClip[layerCount, slotsPerLayer];
            for (int layer = 0; layer < layerCount; layer++)
            {
                for (int slot = 0; slot < slotsPerLayer; slot++)
                    placeholders[layer, slot] = CreatePlaceholderClip(placeholderFolder, layer, slot);
            }

            EnsureBehaviorLayers(controller, placeholders, layerCount, slotsPerLayer);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Behavior AnimatorController 已准备完成。\n" +
                $"Controller: {controllerPath}\n" +
                $"Placeholder Folder: {placeholderFolder}",
                controller);

            return controller;
        }
        private static void EnsureBehaviorLayers(
            AnimatorController controller,
            AnimationClip[,] placeholders,
            int layerCount,
            int slotsPerLayer)
        {
            List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(controller.layers);
            if (layers.Count == 0)
                layers.Add(CreateControllerLayer(controller, 0));

            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                AnimatorControllerLayer layer;
                if (layerIndex < layers.Count)
                {
                    layer = layers[layerIndex];
                    if (layer.stateMachine == null)
                    {
                        layer.stateMachine = new AnimatorStateMachine
                        {
                            name = layerIndex == 0 ? "Base Layer StateMachine" : $"BehaviorLayer{layerIndex}StateMachine"
                        };
                        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
                    }
                }
                else
                {
                    layer = CreateControllerLayer(controller, layerIndex);
                    layers.Add(layer);
                }

                if (string.IsNullOrWhiteSpace(layer.name))
                    layer.name = layerIndex == 0 ? "Base Layer" : $"Layer {layerIndex}";
                SyncBehaviorStates(layer.stateMachine, placeholders, layerIndex, slotsPerLayer);
                layers[layerIndex] = layer;
            }

            controller.layers = layers.ToArray();
        }

        private static AnimatorControllerLayer CreateControllerLayer(AnimatorController controller, int layerIndex)
        {
            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = layerIndex == 0 ? "Base Layer StateMachine" : $"BehaviorLayer{layerIndex}StateMachine"
            };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);

            return new AnimatorControllerLayer
            {
                name = layerIndex == 0 ? "Base Layer" : $"Layer {layerIndex}",
                defaultWeight = 1f,
                stateMachine = stateMachine,
                blendingMode = AnimatorLayerBlendingMode.Override
            };
        }

        private static void SyncBehaviorStates(
            AnimatorStateMachine stateMachine,
            AnimationClip[,] placeholders,
            int layerIndex,
            int slotsPerLayer)
        {
            Dictionary<int, AnimatorState> existingStates = new Dictionary<int, AnimatorState>();
            ChildAnimatorState[] childStates = stateMachine.states;

            for (int i = childStates.Length - 1; i >= 0; i--)
            {
                AnimatorState state = childStates[i].state;
                if (state == null)
                    continue;

                if (!TryParseBehaviorSlotStateName(state.name, layerIndex, out int slotIndex))
                    continue;

                if (slotIndex < 0 || slotIndex >= slotsPerLayer || existingStates.ContainsKey(slotIndex))
                {
                    stateMachine.RemoveState(state);
                    continue;
                }

                existingStates.Add(slotIndex, state);
            }

            for (int slotIndex = 0; slotIndex < slotsPerLayer; slotIndex++)
            {
                if (!existingStates.TryGetValue(slotIndex, out AnimatorState state) || state == null)
                {
                    state = stateMachine.AddState(
                        BehaviorAnimatorControllerConvention.GetStateName(layerIndex, slotIndex));
                    existingStates[slotIndex] = state;
                }

                state.name = BehaviorAnimatorControllerConvention.GetStateName(layerIndex, slotIndex);
                state.motion = placeholders[layerIndex, slotIndex];
                if (slotIndex == 0)
                    stateMachine.defaultState = state;
            }
        }

        private static bool TryParseBehaviorSlotStateName(string stateName, int layerIndex, out int slotIndex)
        {
            slotIndex = -1;
            if (string.IsNullOrWhiteSpace(stateName))
                return false;

            string prefix = $"L{layerIndex}_Segment_";
            if (!stateName.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string suffix = stateName.Substring(prefix.Length);
            return int.TryParse(suffix, out slotIndex);
        }

        private static AnimationClip CreatePlaceholderClip(string placeholderFolder, int layerIndex, int slotIndex)
        {
            string placeholderName = BehaviorAnimatorControllerConvention.GetPlaceholderClipName(layerIndex, slotIndex);
            string clipPath = $"{placeholderFolder}/{placeholderName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null)
                return clip;

            clip = new AnimationClip
            {
                name = placeholderName
            };
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        private static string EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                folderPath = "Assets";

            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return folderPath;

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, parts[i]);

                currentPath = nextPath;
            }

            return folderPath;
        }
    }
}
#endif
