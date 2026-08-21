using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// Behavior AnimatorController 约定。
    /// 运行时播放器、作者工具和正式创建工具都应基于这套命名规则工作。
    /// </summary>
    public static class BehaviorAnimatorControllerConvention
    {
        public const string DefaultSharedControllerFolder = "Assets/BehaviorCore/Animator";
        public const string DefaultSharedControllerName = "BehaviorBaseController";
        public const int DefaultLayerCount = 2;
        public const int DefaultSlotsPerLayer = 8;

        public static string GetStateName(int layer, int slotIndex)
        {
            return $"L{layer}_Segment_{slotIndex}";
        }

        public static string GetPlaceholderClipName(int layer, int slotIndex)
        {
            return $"L{layer}_Placeholder_{slotIndex}";
        }
    }
}
