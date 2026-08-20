using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;

/// <summary>
/// 全局音频设置管理器。负责音频设置的本地持久化，并向所有已注册的 AudioListener 广播音量/开关变更。
/// </summary>
public class AudioDataManager
{
    private const string SaveFolder = "PlayerData/Setting/";
    private const string SaveFileName = "GlobalAudio";

    private static AudioDataManager instance;
    public static AudioDataManager Instance => instance ??= new AudioDataManager();

    // AudioSource → AudioType 监听者映射，用于音量设置变更时批量刷新
    private readonly Dictionary<AudioSource, Core.Gear.AudioType> listeners =
        new Dictionary<AudioSource, Core.Gear.AudioType>();

    // 复用列表，避免每帧分配
    private readonly List<AudioSource> invalidListeners = new List<AudioSource>(4);

    // 当前音频设置数据
    private AudioData data;

    private AudioDataManager()
    {
        LoadData();
        ApplyRuntimeSettings();
    }

    // 当前设置数据的只读访问
    public AudioData Data
    {
        get
        {
            data ??= new AudioData();
            return data;
        }
    }

    // 设置变更事件，UI 面板等订阅以更新滑块状态
    public event Action<AudioData> SettingsChanged;

    #region 监听者管理

    /// <summary>
    /// 注册一个 AudioSource 为音频监听者，设置变更时自动同步音量/静音。
    /// </summary>
    /// <param name="source">要注册的 AudioSource</param>
    /// <param name="type">音频类型</param>
    public void AddAudioListener(AudioSource source, Core.Gear.AudioType type)
    {
        if (source == null) return;

        listeners[source] = type;
        ApplySoundSettingsToSource(source, type);
    }

    /// <summary>
    /// 移除已注册的 AudioSource 监听者。
    /// </summary>
    public void RemoveAudioListener(AudioSource source)
    {
        if (source == null) return;
        listeners.Remove(source);
    }

    #endregion

    #region 设置读写

    /// <summary>
    /// 批量写入设置数据并保存。
    /// </summary>
    /// <param name="nextData">新的设置数据</param>
    public void PushData(AudioData nextData)
    {
        if (nextData == null) return;

        // 复制设置值
        Data.musicEnabled = nextData.musicEnabled;
        Data.musicVolume = Mathf.Clamp01(nextData.musicVolume);
        Data.soundEnabled = nextData.soundEnabled;
        Data.soundVolume = Mathf.Clamp01(nextData.soundVolume);

        // 应用并保存
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音乐开关。
    /// </summary>
    public void SetMusicEnabled(bool enabled)
    {
        if (Data.musicEnabled == enabled) return;

        Data.musicEnabled = enabled;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音乐音量（0-1）。
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Data.musicVolume, clampedVolume)) return;

        Data.musicVolume = clampedVolume;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音效开关。
    /// </summary>
    public void SetSoundEnabled(bool enabled)
    {
        if (Data.soundEnabled == enabled) return;

        Data.soundEnabled = enabled;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 设置音效音量（0-1）。
    /// </summary>
    public void SetSoundVolume(float volume)
    {
        float clampedVolume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(Data.soundVolume, clampedVolume)) return;

        Data.soundVolume = clampedVolume;
        ApplyRuntimeSettings();
        SaveData();
    }

    /// <summary>
    /// 将当前设置序列化到本地文件。
    /// </summary>
    public void SaveData()
    {
        BinaryDataManager.Instance.Save(SaveFolder, SaveFileName, Data);
    }

    /// <summary>
    /// 从本地文件加载设置数据，文件不存在时使用默认值。
    /// </summary>
    public void LoadData()
    {
        data = BinaryDataManager.Instance.Load<AudioData>(SaveFolder, SaveFileName) ?? new AudioData();
    }

    #endregion

    #region Private

    /// <summary>
    /// 应用当前设置到 AudioManager 并刷新所有监听者。
    /// </summary>
    private void ApplyRuntimeSettings()
    {
        // 通知 AudioManager 更新全局设置
        AudioManager.Instance.ApplyAudioSettings(
            Data.musicEnabled,
            Data.musicVolume,
            Data.soundEnabled,
            Data.soundVolume);

        // 刷新所有已注册的监听者
        RefreshListeners();

        // 广播设置变更事件
        SettingsChanged?.Invoke(Data);
    }

    /// <summary>
    /// 遍历所有监听者，剔除无效引用并同步音量设置。
    /// </summary>
    private void RefreshListeners()
    {
        invalidListeners.Clear();

        foreach (KeyValuePair<AudioSource, Core.Gear.AudioType> pair in listeners)
        {
            AudioSource source = pair.Key;
            if (source == null)
            {
                invalidListeners.Add(source);
                continue;
            }

            ApplySoundSettingsToSource(source, pair.Value);
        }

        // 清理已销毁的 AudioSource
        for (int i = 0; i < invalidListeners.Count; i++) listeners.Remove(invalidListeners[i]);
    }

    /// <summary>
    /// 按类型对单个 AudioSource 应用静音和音量设置。
    /// </summary>
    private void ApplySoundSettingsToSource(AudioSource source, Core.Gear.AudioType type)
    {
        if (source == null) return;

        switch (type)
        {
            case Core.Gear.AudioType.Music:
                source.mute = !Data.musicEnabled;
                source.volume = Data.musicVolume;
                break;

            case Core.Gear.AudioType.Sound:
            default:
                source.mute = !Data.soundEnabled;
                source.volume = Data.soundVolume;
                break;
        }
    }

    #endregion
}
