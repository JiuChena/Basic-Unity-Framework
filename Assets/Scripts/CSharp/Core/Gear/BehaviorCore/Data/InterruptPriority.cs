namespace Core.Gear
{
    /// <summary>
    /// 行为打断优先级。数值越大越不可被低优先级行为打断。
    /// </summary>
    public enum InterruptPriority
    {
        None = 0,
        Movement = 1,
        Normal = 2,
        Talent = 3,
        Burst = 4,
        HitReaction = 5,
        Death = 6,
    }
}
