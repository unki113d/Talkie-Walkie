using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float acceleration = 10f;      // Насколько быстро игрок ускоряется
    public float drag = 5f;               // Торможение

    [Header("Look Settings")]
    public Transform cameraRoot;
    public float mouseSensitivity = 2f;
    public float maxVerticalAngle = 80f;

    [Header("Tilt Settings")]
    public Transform visualRoot;
    public float tiltIntensity = 10f;     // Насколько сильно тело наклоняется
    public float tiltSpeed = 5f;          // Насколько быстро меняется наклон

    private Rigidbody rb;
    private Camera playerCamera;
    private Vector2 moveInput;
    private Vector3 currentVelocity = Vector3.zero;
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
        HandleTilt(); // Наклоны тела
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        HandleMovement();
    }

    void HandleMovement()
    {
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 camRight = playerCamera.transform.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 targetDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        // Целевая скорость
        Vector3 targetVelocity = targetDir * moveSpeed;

        // Плавное приближение к нужной скорости
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * acceleration);

        // Применяем к Rigidbody
        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -maxVerticalAngle, maxVerticalAngle);
        cameraRoot.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
    }

    void HandleTilt()
    {
        // Простой угол наклона по Z и X
        float targetTiltZ = -moveInput.x * tiltIntensity; // наклон вбок
        float targetTiltX = moveInput.y * tiltIntensity;  // наклон вперёд/назад

        Quaternion targetTilt = Quaternion.Euler(targetTiltX, 0f, targetTiltZ);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetTilt, Time.deltaTime * tiltSpeed);
    }
}