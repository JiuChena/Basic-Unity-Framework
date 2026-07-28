using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Framework.Core
{
    /// <summary>
    /// 交互接收器，挂在玩家身上，统一管理范围内可交互对象与选项 UI。
    /// </summary>
    public class InteractionReceiver : MonoBehaviour
    {
        [Header("交互设置")]
        [SerializeField, Tooltip("交互按键")] private KeyCode interactKey = KeyCode.F;
        [SerializeField, Tooltip("选项预制体，需挂 InteractionOption 组件与 Animator。")] private GameObject optionPrefab;
        [SerializeField, Tooltip("选项 UI 的父节点。")] private Transform optionsRoot;
        [SerializeField, Tooltip("当前选中项的箭头指示。")] private Transform arrow;
        [SerializeField, Tooltip("滚轮切换冷却时间，单位秒。")] private float scrollCooldown = 0.15f;

        private readonly List<IInteractable> _interactables = new List<IInteractable>();
        private readonly List<InteractionOption> _optionViews = new List<InteractionOption>();
        private int _selectedIndex;
        private float _cooldownTimer;

        public int Count => _interactables.Count;

        private void Update()
        {
            HandleScrollInput();
            UpdateSelectionVisual();
            UpdateArrowPosition();
            HandleInteractInput();
        }

        public void Register(IInteractable interactable)
        {
            if (interactable == null || _interactables.Contains(interactable))
                return;

            _interactables.Add(interactable);
            _interactables.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            RebuildOptionsUI();
        }

        public void Unregister(IInteractable interactable)
        {
            if (interactable == null)
                return;

            int index = _interactables.IndexOf(interactable);
            if (index < 0)
                return;

            _interactables.RemoveAt(index);
            RebuildOptionsUI();

            if (_selectedIndex >= _interactables.Count && _interactables.Count > 0)
                _selectedIndex = _interactables.Count - 1;

            BroadcastCurrentChanged();
        }

        private void HandleScrollInput()
        {
            if (_interactables.Count <= 1)
                return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f)
                return;

            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            scroll /= 120f;
            if (scroll > 0f && _selectedIndex > 0)
            {
                _selectedIndex--;
                _cooldownTimer = scrollCooldown;
            }
            else if (scroll < 0f && _selectedIndex < _interactables.Count - 1)
            {
                _selectedIndex++;
                _cooldownTimer = scrollCooldown;
            }
        }

        private void UpdateSelectionVisual()
        {
            for (int i = 0; i < _optionViews.Count; i++)
            {
                InteractionOption optionView = _optionViews[i];
                if (optionView == null || optionView.animator == null)
                    continue;

                optionView.animator.SetBool("Selected", i == _selectedIndex);
            }
        }

        private void UpdateArrowPosition()
        {
            if (arrow == null)
                return;

            bool hasOptions = _optionViews.Count > 0 && _selectedIndex < _optionViews.Count;
            arrow.gameObject.SetActive(hasOptions);
            if (!hasOptions || _optionViews[_selectedIndex] == null)
                return;

            Vector3 targetPosition = _optionViews[_selectedIndex].transform.position;
            arrow.position = new Vector3(arrow.position.x, targetPosition.y, arrow.position.z);
        }

        private void HandleInteractInput()
        {
            if (!Input.GetKeyDown(interactKey))
                return;

            if (_interactables.Count == 0 || _selectedIndex >= _interactables.Count)
                return;

            IInteractable current = _interactables[_selectedIndex];
            if (!current.CanInteract(gameObject))
                return;

            if (_selectedIndex < _optionViews.Count && _optionViews[_selectedIndex] != null && _optionViews[_selectedIndex].animator != null)
                _optionViews[_selectedIndex].animator.SetTrigger("Click");

            current.OnInteract(gameObject);
            EventCenter.Instance.SetEventTrigger(EventNames.InteractionPerformed, current.InteractionID);
        }

        private void RebuildOptionsUI()
        {
            ClearOptionViews();

            for (int i = 0; i < _interactables.Count; i++)
                CreateOptionView(_interactables[i]);

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _interactables.Count - 1);
            if (arrow != null)
                arrow.gameObject.SetActive(_interactables.Count > 0);

            BroadcastCurrentChanged();
        }

        private void CreateOptionView(IInteractable interactable)
        {
            if (optionPrefab == null || optionsRoot == null)
                return;

            ObjectsPool.Instance.Get(optionPrefab, optionsRoot, pooledObject =>
            {
                InteractionOption view = pooledObject.GetComponent<InteractionOption>();
                if (view == null)
                    return;

                if (view.icon != null && interactable.Icon != null)
                    view.icon.sprite = interactable.Icon;

                if (view.text != null)
                    view.text.text = interactable.PromptText;

                _optionViews.Add(view);
            });
        }

        private void ClearOptionViews()
        {
            for (int i = _optionViews.Count - 1; i >= 0; i--)
            {
                if (_optionViews[i] != null)
                    ObjectsPool.Instance.Put(_optionViews[i].gameObject);
            }

            _optionViews.Clear();
        }

        private void BroadcastCurrentChanged()
        {
            IInteractable current = null;
            if (_interactables.Count > 0 && _selectedIndex < _interactables.Count)
                current = _interactables[_selectedIndex];

            EventCenter.Instance.SetEventTrigger(EventNames.InteractionChanged, current);
        }
    }
}
