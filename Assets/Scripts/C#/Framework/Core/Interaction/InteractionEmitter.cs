using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// 交互发射器，挂载到场景中可交互的物体 / NPC 上。
    /// 通过 Trigger 检测玩家身上的 InteractionReceiver，注册 / 移除 IInteractable。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractionEmitter : MonoBehaviour
    {
        [SerializeField, Tooltip("目标交互层（用于过滤玩家）")]
        private LayerMask targetLayer = ~0;

        [SerializeField, Tooltip("是否为一次性交互（交互后自动禁用）")]
        private bool disposable;

        private IInteractable _interactable;
        private InteractionReceiver _currentReceiver;
        private bool _interacted;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _interactable = GetComponent<IInteractable>();
        }

        private void OnEnable()
        {
            if (disposable && _interacted)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & targetLayer) == 0) return;
            if (_interactable == null) return;

            _currentReceiver = other.GetComponent<InteractionReceiver>();
            if (_currentReceiver == null) return;

            if (!_interactable.CanInteract(other.gameObject)) return;

            _currentReceiver.Register(_interactable);
            _interactable.OnEnterRange(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_currentReceiver == null) return;
            if (((1 << other.gameObject.layer) & targetLayer) == 0) return;

            _currentReceiver.Unregister(_interactable);
            _interactable?.OnExitRange(other.gameObject);
            _currentReceiver = null;
        }

        /// <summary>
        /// 交互执行后的回调（由 Receiver 在调用 IInteractable.OnInteract 之后调用）。
        /// 用于一次性交互物的自动禁用。
        /// </summary>
        public void NotifyInteracted()
        {
            _interacted = true;
            if (disposable && _currentReceiver != null)
            {
                _currentReceiver.Unregister(_interactable);
                _currentReceiver = null;
            }

            if (disposable)
                gameObject.SetActive(false);
        }
    }
}
