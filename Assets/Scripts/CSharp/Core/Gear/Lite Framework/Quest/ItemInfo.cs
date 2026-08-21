using System;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 道具基础配置（ScriptableObject），定义道具的显示名称、价格、图标等。
    /// </summary>
    [CreateAssetMenu(menuName = "Framework/CoreFramework/Items/Item Info")]
    public class ItemInfo : ScriptableObject
    {
        public string assetID;
        public string displayName;
        public ItemType itemType;
        public int buyPrice;
        public int sellPrice;
        public Sprite icon;

        [TextArea]
        public string description;
    }
}
