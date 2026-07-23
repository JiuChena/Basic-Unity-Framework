using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CoreFramework
{
    /// <summary>
    /// 音频管理器，统一管理 AudioSource 池化、音频播放/停止及全局音量设置。
    /// </summary>
    public class AudioManager
    {
        private static readonly AudioManager instance = new AudioManager();
        public static AudioManager Instance => instance;

        // 空闲 AudioSource 池，避免频繁创建销毁
        private readonly Queue<AudioSource> idleSources = new Queue<AudioSource>();

        // 活跃音频句柄表：handle → ActiveAudioInfo
        private readonly Dictionary<int, ActiveAudioInfo> activeAudios = new Dictionary<int, ActiveAudioInfo>();

        // 复用列表，Tick 时收集已完成的 handle
        private readonly List<int> completedAudioHandles = new List<int>();

        // 宿主 GameObject 上的 MonoBehaviour
        private AudioManagerHost host;

        // 所有池化 AudioSource 的父节点
        private Transform root;

        // 自增句柄计数器
        private int nextHandle = 1;

        // 全局音频设置，ApplyAudioSettings 更新，ApplyTypeSettings 消费
        private readonly AudioData globalSettings = new AudioData();

        private AudioManager() { }

        #region Play / Stop（句柄式）

        /// <summary>
        /// 播放 AudioClip 并返回句柄，可通过句柄停止或由 Tick 自动回收。
        /// </summary>
        /// <param name="clip">AudioClip 资源</param>
        /// <param name="type">音频类型（音乐/音效）</param>
        /// <param name="position">世界空间播放位置</param>
        /// <param name="loop">是否循环</param>
        /// <param name="volume">音量倍率（0-1）</param>
        /// <param name="followTarget">跟随目标，null 为静止音源</param>
        /// <returns>音频句柄，-1 表示 clip 为空</returns>
        public int Play(AudioClip clip, AudioType type, Vector3 position, bool loop = false, float volume = 1f,
            Transform followTarget = null)
        {
            if (clip == null) return -1;

            EnsureHost();

            // 从池中获取或创建 AudioSource
            AudioSource source = GetPooledSource();
            source.transform.position = position;
            source.loop = loop;
            ApplyTypeSettings(source, type, Mathf.Clamp01(volume));
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.gameObject.SetActive(true);
            source.Play();

            // 分配句柄并记录活跃音频信息
            int handle = nextHandle++;
            activeAudios[handle] = new ActiveAudioInfo(source, loop, followTarget, type, Mathf.Clamp01(volume));
            return handle;
        }

        /// <summary>
        /// 在指定父对象下播放 AudioClip（挂载式），音源挂为父对象子节点，位于父对象本地原点。
        /// </summary>
        /// <param name="clip">AudioClip 资源</param>
        /// <param name="type">音频类型</param>
        /// <param name="parent">挂载的父对象 Transform</param>
        /// <param name="loop">是否循环</param>
        /// <param name="volume">音量倍率（0-1）</param>
        /// <returns>音频句柄，-1 表示 clip 或 parent 为空</returns>
        public int Play(AudioClip clip, AudioType type, Transform parent, bool loop = false, float volume = 1f)
        {
            if (clip == null || parent == null) return -1;

            EnsureHost();

            AudioSource source = GetPooledSource();
            source.transform.SetParent(parent);
            source.transform.localPosition = Vector3.zero;
            source.loop = loop;
            ApplyTypeSettings(source, type, Mathf.Clamp01(volume));
            source.clip = clip;
            source.spatialBlend = 0f;
            source.gameObject.SetActive(true);
            source.Play();

            int handle = nextHandle++;
            activeAudios[handle] = new ActiveAudioInfo(source, loop, null, type, Mathf.Clamp01(volume));
            return handle;
        }

        /// <summary>
        /// 在指定父对象下以相对位置播放 AudioClip。
        /// </summary>
        /// <param name="clip">AudioClip 资源</param>
        /// <param name="type">音频类型</param>
        /// <param name="parent">挂载的父对象 Transform</param>
        /// <param name="localPosition">相对于父对象的本地坐标</param>
        /// <param name="loop">是否循环</param>
        /// <param name="volume">音量倍率（0-1）</param>
        /// <returns>音频句柄，-1 表示 clip 或 parent 为空</returns>
        public int Play(AudioClip clip, AudioType type, Transform parent, Vector3 localPosition, bool loop = false,
            float volume = 1f)
        {
            if (clip == null || parent == null) return -1;

            EnsureHost();

            AudioSource source = GetPooledSource();
            source.transform.SetParent(parent);
            source.transform.localPosition = localPosition;
            source.loop = loop;
            ApplyTypeSettings(source, type, Mathf.Clamp01(volume));
            source.clip = clip;
            source.spatialBlend = 0f;
            source.gameObject.SetActive(true);
            source.Play();

            int handle = nextHandle++;
            activeAudios[handle] = new ActiveAudioInfo(source, loop, null, type, Mathf.Clamp01(volume));
            return handle;
        }

        /// <summary>
        /// 按句柄停止正在播放的音频。
        /// </summary>
        /// <param name="audioHandle">Play 返回的句柄</param>
        public void Stop(int audioHandle)
        {
            if (activeAudios.TryGetValue(audioHandle, out ActiveAudioInfo info))
                ReleaseHandle(audioHandle, info);
        }

        /// <summary>
        /// 停止所有使用指定 AudioClip 的播放实例。
        /// </summary>
        /// <param name="clip">要停止的 AudioClip</param>
        public void Stop(AudioClip clip)
        {
            if (clip == null) return;

            // 收集匹配指定 clip 的所有句柄
            completedAudioHandles.Clear();
            foreach (KeyValuePair<int, ActiveAudioInfo> pair in activeAudios)
            {
                if (pair.Value.Source != null && pair.Value.Source.clip == clip)
                    completedAudioHandles.Add(pair.Key);
            }

            for (int i = 0; i < completedAudioHandles.Count; i++) Stop(completedAudioHandles[i]);
        }

        #endregion

        #region SetAudio / RemoveAudio（组件绑定式）

        /// <summary>
        /// 通过 Addressable Key 异步加载 AudioClip 并绑定到指定 GameObject 的 AudioSource 上播放。
        /// </summary>
        /// <param name="addressableKey">AudioClip 的 Addressable key</param>
        /// <param name="obj">挂载 AudioSource 的目标对象</param>
        /// <param name="type">音频类型</param>
        /// <param name="callback">播放开始后的回调</param>
        /// <param name="open3D">是否启用 3D 空间音效</param>
        /// <param name="rolloffMode">音量衰减模式</param>
        public async void SetAudio(string addressableKey, GameObject obj, AudioType type,
            UnityAction<AudioSource> callback = null,
            bool open3D = false, AudioRolloffMode rolloffMode = AudioRolloffMode.Linear)
        {
            if (string.IsNullOrWhiteSpace(addressableKey)) return;

            // 异步加载 AudioClip 资源
            ResourceLease<AudioClip> clipLease =
                await AddressableManager.Instance.AcquireAssetAsync<AudioClip>(addressableKey);
            if (clipLease == null || clipLease.Asset == null)
            {
                Debug.LogError($"音频资源加载失败：{addressableKey}");
                return;
            }

            SetAudioInternal(clipLease.Asset, clipLease, obj, type, callback, open3D, rolloffMode);
        }

        /// <summary>
        /// 直接使用已加载的 AudioClip 播放音频。
        /// </summary>
        /// <param name="clip">已加载的 AudioClip</param>
        /// <param name="obj">挂载 AudioSource 的目标对象</param>
        /// <param name="type">音频类型</param>
        /// <param name="callback">播放开始后的回调</param>
        /// <param name="open3D">是否启用 3D 空间音效</param>
        /// <param name="rolloffMode">音量衰减模式</param>
        public void SetAudio(AudioClip clip, GameObject obj, AudioType type, UnityAction<AudioSource> callback = null,
            bool open3D = false, AudioRolloffMode rolloffMode = AudioRolloffMode.Linear)
        {
            SetAudioInternal(clip, null, obj, type, callback, open3D, rolloffMode);
        }

        /// <summary>
        /// SetAudio 内部实现。根据是否传入 obj 决定使用对象上的 AudioSource 还是池化 AudioSource。
        /// </summary>
        private void SetAudioInternal(AudioClip clip, ResourceLease<AudioClip> clipLease, GameObject obj, AudioType type,
            UnityAction<AudioSource> callback, bool open3D, AudioRolloffMode rolloffMode)
        {
            if (clip == null)
            {
                clipLease?.Dispose();
                return;
            }

            // 有目标对象：使用对象上的 AudioSource
            if (obj != null)
            {
                AudioSource audioSource = obj.GetComponent<AudioSource>() ?? obj.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.clip = clip;
                audioSource.spatialBlend = open3D ? 1f : 0f;
                audioSource.rolloffMode = rolloffMode;
                audioSource.loop = false;
                ApplyTypeSettings(audioSource, type, 1f);
                AudioDataManager.Instance.AddAudioListener(audioSource, type);
                audioSource.Play();
                callback?.Invoke(audioSource);

                // 有资源租约时启动协程跟踪播放完成后自动释放
                if (clipLease != null)
                {
                    EnsureHost();
                    host.StartTrackedClipRelease(audioSource, clipLease);
                }

                return;
            }

            // 无目标对象：使用池化 AudioSource
            EnsureHost();
            AudioSource pooledSource = GetPooledSource();
            pooledSource.transform.position = Vector3.zero;
            pooledSource.playOnAwake = false;
            pooledSource.clip = clip;
            pooledSource.spatialBlend = open3D ? 1f : 0f;
            pooledSource.rolloffMode = rolloffMode;
            pooledSource.loop = false;
            ApplyTypeSettings(pooledSource, type, 1f);
            pooledSource.gameObject.SetActive(true);
            pooledSource.Play();
            callback?.Invoke(pooledSource);

            // 记录活跃音频，Tick 时自动回收
            int handle = nextHandle++;
            activeAudios[handle] = new ActiveAudioInfo(pooledSource, false, null, type, 1f, clipLease);
        }

        /// <summary>
        /// 对指定 AudioSource 执行停止/暂停/静音/清除操作。
        /// </summary>
        /// <param name="audioSource">目标 AudioSource</param>
        /// <param name="mode">停止模式</param>
        /// <param name="callback">操作完成后的回调</param>
        public void RemoveAudio(AudioSource audioSource, StopAudioMode mode = StopAudioMode.ClipClear,
            UnityAction<AudioSource> callback = null)
        {
            if (audioSource == null) return;

            // 按模式执行对应操作
            if (mode == StopAudioMode.ClipPause) audioSource.Pause();
            else if (mode == StopAudioMode.ClipStop) audioSource.Stop();
            else if (mode == StopAudioMode.ClipMute) audioSource.mute = true;
            else if (mode == StopAudioMode.ClipClear)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }

            callback?.Invoke(audioSource);
        }

        /// <summary>
        /// 移除 AudioSource 组件或销毁其所在 GameObject。
        /// </summary>
        /// <param name="audioSource">目标 AudioSource</param>
        /// <param name="mode">移除模式</param>
        /// <param name="callback">操作完成后的回调</param>
        public void RemoveAudio(AudioSource audioSource, RemoveAudioMode mode = RemoveAudioMode.RemoveAudioSource,
            UnityAction callback = null)
        {
            if (audioSource == null) return;

            // 先从设置监听者中移除
            AudioDataManager.Instance.RemoveAudioListener(audioSource);

            // 按模式销毁 AudioSource 或整个 GameObject
            if (mode == RemoveAudioMode.RemoveAudioSource) Object.Destroy(audioSource);
            else Object.Destroy(audioSource.gameObject);

            callback?.Invoke();
        }

        #endregion

        #region Tick / 全局设置

        /// <summary>
        /// 每帧由 AudioManagerHost.Update 调用。检查活跃音频的播放状态，回收已完成播放的句柄。
        /// </summary>
        internal void Tick()
        {
            if (activeAudios.Count == 0) return;

            completedAudioHandles.Clear();

            foreach (KeyValuePair<int, ActiveAudioInfo> pair in activeAudios)
            {
                AudioSource source = pair.Value.Source;

                // AudioSource 已销毁 → 标记回收
                if (source == null)
                {
                    completedAudioHandles.Add(pair.Key);
                    continue;
                }

                // 跟随目标位置
                if (pair.Value.FollowTarget != null)
                    source.transform.position = pair.Value.FollowTarget.position;

                // 非循环且已停止播放 → 标记回收
                if (!pair.Value.IsLoop && !source.isPlaying)
                    completedAudioHandles.Add(pair.Key);
            }

            // 释放所有已完成的句柄
            for (int i = 0; i < completedAudioHandles.Count; i++)
            {
                int handle = completedAudioHandles[i];
                if (activeAudios.TryGetValue(handle, out ActiveAudioInfo info))
                    ReleaseHandle(handle, info);
            }
        }

        /// <summary>
        /// 应用全局音频设置，并同步所有活跃 AudioSource 的音量/静音状态。
        /// </summary>
        public void ApplyAudioSettings(bool musicEnabledValue, float musicVolumeValue, bool soundEnabledValue,
            float soundVolumeValue)
        {
            globalSettings.musicEnabled = musicEnabledValue;
            globalSettings.musicVolume = Mathf.Clamp01(musicVolumeValue);
            globalSettings.soundEnabled = soundEnabledValue;
            globalSettings.soundVolume = Mathf.Clamp01(soundVolumeValue);

            // 同步所有活跃音频
            foreach (KeyValuePair<int, ActiveAudioInfo> pair in activeAudios)
            {
                AudioSource source = pair.Value.Source;
                if (source == null) continue;

                ApplyTypeSettings(source, pair.Value.Type, pair.Value.BaseVolume);
            }
        }

        #endregion

        #region Private

        /// <summary>
        /// 按音频类型对 AudioSource 应用当前全局音量与静音设置。
        /// </summary>
        private void ApplyTypeSettings(AudioSource source, AudioType type, float baseVolume)
        {
            if (source == null) return;

            switch (type)
            {
                case AudioType.Music:
                    source.mute = !globalSettings.musicEnabled;
                    source.volume = Mathf.Clamp01(baseVolume) * globalSettings.musicVolume;
                    break;

                case AudioType.Sound:
                default:
                    source.mute = !globalSettings.soundEnabled;
                    source.volume = Mathf.Clamp01(baseVolume) * globalSettings.soundVolume;
                    break;
            }
        }

        /// <summary>
        /// 确保宿主 GameObject 和 AudioManagerHost 组件存在。
        /// </summary>
        private void EnsureHost()
        {
            if (host != null) return;

            GameObject hostObject = new GameObject("AudioManager");
            Object.DontDestroyOnLoad(hostObject);
            root = hostObject.transform;
            host = hostObject.AddComponent<AudioManagerHost>();
        }

        /// <summary>
        /// 从对象池获取 AudioSource，池空则创建新实例。
        /// </summary>
        private AudioSource GetPooledSource()
        {
            EnsureHost();

            // 优先复用空闲 AudioSource
            while (idleSources.Count > 0)
            {
                AudioSource cached = idleSources.Dequeue();
                if (cached != null) return cached;
            }

            // 池空：创建新的 AudioSource
            GameObject sourceObject = new GameObject("PooledAudioSource");
            sourceObject.transform.SetParent(root, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        /// <summary>
        /// 释放音频句柄：停止播放、清除状态、归还 AudioSource 到池、释放资源租约。
        /// </summary>
        private void ReleaseHandle(int audioHandle, ActiveAudioInfo info)
        {
            activeAudios.Remove(audioHandle);

            AudioSource source = info.Source;
            if (source == null) return;

            // 停止并重置状态
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.mute = false;

            // 归还到空闲池
            source.transform.SetParent(root, false);
            source.gameObject.SetActive(false);
            idleSources.Enqueue(source);

            // 释放资源租约
            info.ClipLease?.Dispose();
        }

        #endregion
    }
}
