using UnityEngine;
using System.Collections;

namespace Framework.Core
{
    /// <summary>
    /// 音频管理器宿主 MonoBehaviour，驱动 Update Tick 并管理播放完成后的自动回收。
    /// </summary>
    public class AudioManagerHost : MonoBehaviour
    {
        /// <summary>
        /// 开始追踪 AudioClip 播放，播放结束后自动释放资源租约。
        /// </summary>
        /// <param name="source">正在播放的 AudioSource</param>
        /// <param name="clipLease">AudioClip 资源租约</param>
        public void StartTrackedClipRelease(AudioSource source, ResourceLease<AudioClip> clipLease)
        {
            if (source == null || clipLease == null)
            {
                clipLease?.Dispose();
                return;
            }

            StartCoroutine(ReleaseWhenPlaybackFinished(source, clipLease));
        }

        private void Update()
        {
            // 每帧驱动 AudioManager 检查播放状态
            AudioManager.Instance.Tick();
        }

        /// <summary>
        /// 等待 AudioSource 播放完成后自动释放 Clip 资源租约。
        /// </summary>
        private static IEnumerator ReleaseWhenPlaybackFinished(AudioSource source, ResourceLease<AudioClip> clipLease)
        {
            // 持续等待直到播放停止或对象销毁
            while (source != null && source.gameObject != null && source.isActiveAndEnabled && source.isPlaying)
                yield return null;

            clipLease.Dispose();
        }
    }
}
