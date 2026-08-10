using System;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 为策略一级序列化模块字段提供独立于变量名的 Inspector 标题。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class UnitMovementModuleNameAttribute : Attribute
    {
        /// <summary>获取模块在 UnitMover Inspector 折叠栏中显示的标题。</summary>
        public string DisplayName { get; }

        /// <summary>
        /// 创建一个策略模块中文标题特性。
        /// </summary>
        /// <param name="displayName">显示在策略模块折叠栏中的非空标题。</param>
        public UnitMovementModuleNameAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
