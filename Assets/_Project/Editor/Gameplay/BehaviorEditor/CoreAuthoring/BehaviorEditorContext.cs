using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 保存作者期各轨道可共享的 Reference Root。
    /// </summary>
    internal static class BehaviorEditorContext
    {
        // 作者期指定的角色根节点，用于共享骨骼路径解析。
        private static GameObject referenceRootObject;

        /// <summary>获取或设置作者期指定的角色根节点。</summary>
        public static GameObject ReferenceRootObject
        {
            get => referenceRootObject;
            set => referenceRootObject = value;
        }

        /// <summary>获取角色根节点的 Transform；未指定时返回 null。</summary>
        public static Transform ReferenceRootTransform => referenceRootObject != null ? referenceRootObject.transform : null;
    }
}
