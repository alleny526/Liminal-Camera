using UnityEngine;

// 玩家控制器，处理移动、跳跃和视角控制
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Camera playerCamera;

    [Header("移动参数")]
    public float walkSpeed = 5.0f;
    public float runSpeed = 10.0f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("鼠标灵敏度")]
    public float mouseSensitivity = 100.0f;

    private CharacterController characterController;
    private Vector3 velocity;
    private float cameraVerticalRotation = 0f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
    }

    // 处理玩家移动和跳跃
    private void HandleMovement()
    {
        bool isGrounded = characterController.isGrounded;
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -1f;
        }

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 moveDirection = transform.forward * verticalInput + transform.right * horizontalInput;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        characterController.Move(moveDirection * currentSpeed * Time.deltaTime);

        bool jumpInput = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);

        if (jumpInput)
        {
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    // 处理鼠标视角控制
    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        cameraVerticalRotation -= mouseY;
        cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraVerticalRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    // 重置玩家速度（用于传送后）
    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }
}
