using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 活跃音频信息，记录正在播放的 AudioSource 及其播放参数。
    /// </summary>
    internal sealed class ActiveAudioInfo
    {
        // 播放中的 AudioSource 组件
        public AudioSource Source { get; }

        // 是否循环播放
        public bool IsLoop { get; }

        // 跟随目标 Transform，null 表示静止音源
        public Transform FollowTarget { get; }

        // 音频类型（音乐 / 音效）
        public AudioType Type { get; }

        // 基准音量，应用全局设置前的原始音量
        public float BaseVolume { get; }

        // AudioClip 的资源租约，用于播放结束后的生命周期管理
        public ResourceLease<AudioClip> ClipLease { get; }

        /// <summary>
        /// 构建活跃音频信息快照。
        /// </summary>
        /// <param name="source">播放组件</param>
        /// <param name="isLoop">是否循环</param>
        /// <param name="followTarget">跟随目标</param>
        /// <param name="type">音频类型</param>
        /// <param name="baseVolume">基准音量（0-1）</param>
        /// <param name="clipLease">资源租约</param>
        public ActiveAudioInfo(AudioSource source, bool isLoop, Transform followTarget, AudioType type, float baseVolume,
            ResourceLease<AudioClip> clipLease = null)
        {
            Source = source;
            IsLoop = isLoop;
            FollowTarget = followTarget;
            Type = type;
            BaseVolume = Mathf.Clamp01(baseVolume);
            ClipLease = clipLease;
        }
    }
}
