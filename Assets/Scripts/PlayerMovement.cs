using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float stopThreshold = 0.1f; // Минимальный input для движения
    public float drag = 5f; // Торможение

    [Header("Look Settings")]
    public Transform cameraRoot;
    public float mouseSensitivity = 2f;
    public float maxVerticalAngle = 80f;

    private Rigidbody rb;
    private Camera playerCamera;
    private Vector2 moveInput;
    private float verticalLookRotation = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = drag;

        if (isLocalPlayer)
        {
            playerCamera = GetComponentInChildren<Camera>();
            playerCamera.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

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

    void HandleMovement()
    {
        // Нет ввода — сбрасываем движение по XZ
        if (moveInput.sqrMagnitude < stopThreshold * stopThreshold)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        // Направление движения — относительно камеры
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 camRight = playerCamera.transform.right;

        // Убираем вертикальную составляющую
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // Применяем velocity
        rb.linearVelocity = new Vector3(moveDir.x * moveSpeed, rb.linearVelocity.y, moveDir.z * moveSpeed);
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Поворот тела по горизонтали
        transform.Rotate(Vector3.up * mouseX);

        // Поворот головы по вертикали
        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -maxVerticalAngle, maxVerticalAngle);
        cameraRoot.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }
}