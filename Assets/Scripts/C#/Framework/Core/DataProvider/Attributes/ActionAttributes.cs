namespace Framework.Core
{
    /// <summary>
    /// 以持久 InputButton 保存边沿版本的动作按钮属性基类。
    /// 构造函数自动创建 InputButton 实例，子类无需关心初始化。
    /// </summary>
    public abstract class ButtonAttribute : BlackboardAttribute<InputButton>
    {
        /// <summary>
        /// 初始化 InputButton 实例。
        /// </summary>
        protected ButtonAttribute()
        {
            Value = new InputButton();
        }

        /// <summary>
        /// 便捷方法：直接从原始采样写入按钮状态。
        /// </summary>
        /// <param name="pressed">本帧是否按下</param>
        /// <param name="held">本帧是否按住</param>
        /// <param name="released">本帧是否抬起</param>
        public void SetState(bool pressed, bool held, bool released)
        {
            Value.SetState(pressed, held, released);
        }
    }

    /// <summary>跳跃动作按钮</summary>
    public sealed class JumpAttribute : ButtonAttribute { }

    /// <summary>下蹲动作按钮</summary>
    public sealed class CrouchAttribute : ButtonAttribute { }

    /// <summary>普攻动作按钮</summary>
    public sealed class AttackAttribute : ButtonAttribute { }

    /// <summary>天赋动作按钮</summary>
    public sealed class TalentAttribute : ButtonAttribute { }

    /// <summary>爆发动作按钮</summary>
    public sealed class BurstAttribute : ButtonAttribute { }

    /// <summary>装填动作按钮</summary>
    public sealed class ReloadAttribute : ButtonAttribute { }

    /// <summary>交互动作按钮</summary>
    public sealed class InteractAttribute : ButtonAttribute { }
}
