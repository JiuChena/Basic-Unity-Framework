#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace CoreFramework
{
    /// <summary>
    /// 读取 Unity Input System Action，并写入 InputActionStateStore。
    /// </summary>
    public sealed class InputSystemReader : IInputReader
    {
        private readonly PlayerInput _playerInput;
        private readonly InputBindingProfile _profile;
        private readonly Dictionary<InputActionId, InputAction> _runtimeActions =
            new Dictionary<InputActionId, InputAction>();

        public InputSystemReader(PlayerInput playerInput, InputBindingProfile profile)
        {
            _playerInput = playerInput;
            _profile = profile;
        }

        public void RegisterActions(InputActionStateStore stateStore)
        {
            foreach (InputBindingProfile.InputSystemBinding binding in _profile.InputSystemBindings)
            {
                if (!binding.actionId.IsValid) continue;
                stateStore.Register(binding.actionId, binding.valueType);
                InputAction action = ResolveRuntimeAction(binding);
                if (action != null) _runtimeActions[binding.actionId] = action;
            }
        }

        public void Tick(InputActionStateStore stateStore)
        {
            foreach (InputBindingProfile.InputSystemBinding binding in _profile.InputSystemBindings)
            {
                if (!binding.actionId.IsValid) continue;
                if (!_runtimeActions.TryGetValue(binding.actionId, out InputAction action))
                {
                    action = ResolveRuntimeAction(binding);
                    if (action == null)
                    {
                        WriteDefault(stateStore, binding);
                        continue;
                    }

                    _runtimeActions[binding.actionId] = action;
                }

                switch (binding.valueType)
                {
                    case InputValueType.Button:
                        stateStore.SetButton(
                            binding.actionId,
                            action.WasPressedThisFrame(),
                            action.IsPressed(),
                            action.WasReleasedThisFrame());
                        break;
                    case InputValueType.Value1D:
                        stateStore.SetFloat(binding.actionId, action.ReadValue<float>());
                        break;
                    case InputValueType.Value2D:
                        stateStore.SetVector2(binding.actionId, action.ReadValue<UnityEngine.Vector2>());
                        break;
                }
            }
        }

        private InputAction ResolveRuntimeAction(InputBindingProfile.InputSystemBinding binding)
        {
            InputAction referenceAction = binding.inputAction != null ? binding.inputAction.action : null;
            if (referenceAction == null) return null;

            InputActionAsset runtimeAsset = _playerInput != null ? _playerInput.actions : _profile.inputActionAsset;
            return runtimeAsset != null
                ? runtimeAsset.FindAction(referenceAction.id.ToString(), false)
                : referenceAction;
        }

        private static void WriteDefault(InputActionStateStore stateStore, InputBindingProfile.InputSystemBinding binding)
        {
            switch (binding.valueType)
            {
                case InputValueType.Button:
                    stateStore.SetButton(binding.actionId, false, false, false);
                    break;
                case InputValueType.Value1D:
                    stateStore.SetFloat(binding.actionId, 0f);
                    break;
                case InputValueType.Value2D:
                    stateStore.SetVector2(binding.actionId, UnityEngine.Vector2.zero);
                    break;
            }
        }
    }
}
#endif
