namespace Framework.Core
{
    /// <summary>
    /// 黑板属性的非泛型标记基类。
    /// 仅用于 Blackboard 字典的存储约束，不暴露任何数据读写方法。
    /// </summary>
    public abstract class BlackboardAttribute
    {
    }

    /// <summary>
    /// 具有强类型值的黑板属性基类。
    /// 子类可在 Value 的 setter 中维护自身不变量（如自动归一化、边界 Clamp）。
    /// </summary>
    /// <typeparam name="T">属性存储的数据类型</typeparam>
    public abstract class BlackboardAttribute<T> : BlackboardAttribute
    {
        /// <summary>
        /// 类型安全的属性值。外部直接 get/set，无装箱。
        /// </summary>
        public virtual T Value { get; set; }
    }
}
