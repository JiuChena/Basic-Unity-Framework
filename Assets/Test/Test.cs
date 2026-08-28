using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Test : MonoBehaviour
{
    // 当前物体上的角色控制器。
    private CharacterController _characterController;
    // 当前角色的垂直速度，单位：米/秒。
    private float _verticalVelocity;
    // 角色水平移动速度，单位：米/秒。
    [Header("移动")]
    [Tooltip("使用旧输入系统读取 Horizontal 和 Vertical 后的移动速度，单位：米/秒")]
    [Min(0f)] [SerializeField] private float _moveSpeed = 5f;
    // 角色受到的垂直重力加速度，单位：米/秒²。
    [Header("重力")]
    [Tooltip("角色每秒受到的垂直重力加速度，通常使用负值，单位：米/秒²")]
    [SerializeField] private float _gravity = -9.81f;
    // 角色允许达到的最大下落速度，单位：米/秒。
    [Tooltip("角色允许达到的最大下落速度，使用负值表示向下，单位：米/秒")]
    [SerializeField] private float _terminalVelocity = -50f;

    /// <summary>缓存当前物体上的角色控制器。</summary>
    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    /// <summary>读取旧输入系统并执行基础水平移动与重力。</summary>
    private void Update()
    {
        // 读取旧输入系统的水平和垂直轴，并限制斜向移动速度。
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);
        Vector3 movement = transform.TransformDirection(input) * _moveSpeed;

        // 接地时保留少量向下速度，避免角色控制器失去地面接触。
        if (_characterController.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        // 更新并限制垂直下落速度。
        _verticalVelocity = Mathf.Max(_verticalVelocity + _gravity * Time.deltaTime, _terminalVelocity);
        movement.y = _verticalVelocity;

        // CharacterController 统一处理本帧位移和碰撞滑动。
        _characterController.SimpleMove(movement * Time.deltaTime);
    }
}
