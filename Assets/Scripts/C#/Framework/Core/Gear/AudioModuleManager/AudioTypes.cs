namespace CoreFramework
{
    /// <summary>
    /// 音频类型。用于区分背景音乐与普通音效，决定读取哪组玩家音量设置。
    /// </summary>
    public enum AudioType
    {
        Music,
        Sound,
    }

    /// <summary>
    /// 音频停止模式。
    /// </summary>
    public enum StopAudioMode
    {
        ClipPause,
        ClipStop,
        ClipMute,
        ClipClear,
    }

    /// <summary>
    /// 音频移除模式。
    /// </summary>
    public enum RemoveAudioMode
    {
        RemoveAudioSource,
        RemoveGameObject,
    }
}
