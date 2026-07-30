using UnityEngine;

/// <summary>使用旧输入系统驱动 CharacterController 的最小移动验证脚本。</summary>
[RequireComponent(typeof(CharacterController))]
public sealed class TestMove : MonoBehaviour
{
    // 角色在水平面上的基础移动速度，单位：米/秒。
    [Tooltip("角色在水平面上的基础移动速度，单位：米/秒")]
    [Min(0f)] [SerializeField] private float _moveSpeed = 5f;
    // 同对象的 CharacterController 移动组件。
    private CharacterController _characterController;

    /// <summary>
    /// 缓存同对象的 CharacterController，避免每帧查找组件。
    /// </summary>
    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    /// <summary>
    /// 使用旧输入系统读取水平和垂直轴，并通过 SimpleMove 执行无跳跃移动。
    /// </summary>
    private void Update()
    {
        // 读取输入后转换为角色本地朝向的水平移动方向。
        Vector3 inputDirection = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical"));
        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        // SimpleMove 负责时间缩放和基础重力处理，因此这里不实现跳跃或额外重力。
        Vector3 movement = transform.TransformDirection(inputDirection) * _moveSpeed;
        _characterController.SimpleMove(movement);
    }
}
