using System.Collections;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// UI 面板抽象基类。生命周期：EventInit（Awake）→ ComponentInit（Start）→ OnUpdate（Update）。
    /// 关闭时通过 hideDelay 延迟销毁，给退出动画留出播放时间。
    /// </summary>
    public abstract class PanelBase : MonoBehaviour
    {
        /// <summary>
        /// 隐藏面板后延迟销毁的时间（秒）。
        /// </summary>
        [SerializeField, Tooltip("隐藏面板后延迟销毁的时间（秒），给 Animator 退出动画留出播放时间。")]
        protected float hideDelay = 2f;

        /// <summary>
        /// 面板上的 Animator 组件（延迟缓存，首次访问时 GetComponent）。
        /// </summary>
        protected Animator animator
        {
            get
            {
                if (_animator == null)
                    _animator = GetComponent<Animator>();
                return _animator;
            }
        }
        private Animator _animator;

        private void Awake() { EventInit(); }
        private void Start() { ComponentInit(); }
        private void Update() { OnUpdate(); }

        /// <summary>
        /// 面板显示时由 PanelManager 调用。子类可覆写以播放入场动画。
        /// </summary>
        public virtual void DisplayPanel() { }

        /// <summary>
        /// 面板隐藏时由 PanelManager 调用。默认等待 <see cref="hideDelay"/> 秒后销毁。
        /// 子类可覆写以播放退出动画，覆写后需调用 base.HidePanel() 以确保最终销毁。
        /// </summary>
        public virtual void HidePanel()
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(DelayedDestroy(hideDelay));
            else
                DestroyPanel();
        }

        private IEnumerator DelayedDestroy(float delay)
        {
            yield return new WaitForSeconds(delay);
            DestroyPanel();
        }

        /// <summary>
        /// 立即销毁当前面板 GameObject。
        /// </summary>
        /// <param name="delay">延迟秒数，0 表示立即销毁</param>
        public void DestroyPanel(float delay = 0f)
        {
            Destroy(gameObject, delay);
        }

        /// <summary>
        /// Awake 时调用，子类在此注册事件监听（EventCenter.AddEventListener），此时子物体可能尚未创建。
        /// </summary>
        protected abstract void EventInit();

        /// <summary>
        /// Start 时调用，子类在此查找组件引用（GetComponent、Find），此时子物体已就绪。
        /// </summary>
        protected abstract void ComponentInit();

        /// <summary>
        /// Update 时调用，子类按需覆写以执行逐帧刷新。
        /// </summary>
        protected virtual void OnUpdate() { }

        /// <summary>
        /// ESC 键按下时由 PanelManager 调用。返回 false 表示面板自行处理了 ESC，
        /// 不会被栈式关闭。子类可覆写以实现自定义返回逻辑（如 GamePanel→MainMenu）。
        /// </summary>
        public virtual bool OnEscapePressed() => true;
    }
}