using UnityEngine;
using Mirror;
using Mirror.Examples.Common;
public class PlayerMovement : NetworkBehaviour
{
    // --- Public поля для настройки в инспекторе ---

    [Header("References")]
    [SerializeField] private Transform cameraRoot;        // Ссылка на пустышку в голове (куда смотрит камера)
    [SerializeField] private Transform visualRoot;        // Скелет персонажа (будет вращаться)
    [SerializeField] private Animator animator;           // Animator на модели

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float drag = 5f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxVerticalAngle = 80f;

    // --- Приватные переменные ---
    private Rigidbody rb;
    private Camera playerCamera;
    private Vector2 moveInput;        // WASD
    private float verticalLookRotation; // Камера по вертикали
    private Vector3 currentVelocity;    // Для сглаживания движения

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.linearDamping = drag;

        if (isLocalPlayer)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(false);
        }
    }
    void Update()
    {
        if (!isLocalPlayer) return;

        // Читаем ввод
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        HandleLook();
    }
    void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        HandleMovement();
    }
    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Вращаем тело (горизонтально)
        transform.Rotate(Vector3.up * mouseX);

        // Вращаем голову (вертикально)
        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -maxVerticalAngle, maxVerticalAngle);
        cameraRoot.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }
    void HandleMovement()
    {
        // Получаем направление от камеры
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 camRight = playerCamera.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // Обновляем velocity
        Vector3 velocity = new Vector3(moveDir.x * moveSpeed, rb.velocity.y, moveDir.z * moveSpeed);
        rb.velocity = velocity;
    }
}