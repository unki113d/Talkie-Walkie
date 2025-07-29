using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float _animationBlendSpeed = 8.9f;
    [SerializeField] private Transform _cameraRoot;
    [SerializeField] private Transform _camera;

    [SerializeField] private float _upperLimit = -40f;
    [SerializeField] private float _bottomLimit = 70f;

    [SerializeField] private float _mouseSensitivity = 21.9f;

    private Rigidbody _rb;
    private InputManager _inputManager;
    private Animator _animator;
    private bool _hasAnimator;

    private int _velocityXHash;
    private int _velocityYHash;

    private float _xRotation;
    private float _yRotation; // накопление дл€ поворота по горизонтали

    private float targetSpeed = 0f;
    private float dx = 0f;
    private float dz = 0f;

    [Header("Player Settings")]
    [SerializeField] private float _walkSpeed;
    [SerializeField] private float _runSpeed;

    [Header("Dynamic turn")]
    [SerializeField] private float _maxHeadAngle = 60f;
    private float _headYawOffset;
    private float _bodyYaw;

    private Vector2 _currentVelocity;


    void Start()
    {
        if (!isLocalPlayer)
        {
            GetComponent<InputManager>().enabled = false;
            _camera.gameObject.SetActive(false);
            this.enabled = false;
            return;
        }
        _hasAnimator = TryGetComponent<Animator>(out _animator);
        _rb = GetComponent<Rigidbody>();
        _inputManager = GetComponent<InputManager>();

        _velocityXHash = Animator.StringToHash("X_Velocity");
        _velocityYHash = Animator.StringToHash("Y_Velocity");
    }
    private void FixedUpdate()
    {
        Move();
    }
    private void LateUpdate()
    {
        CamMovements();
    }

    private void Move()
    {
        if (!_hasAnimator) return;

        targetSpeed = _inputManager.Run ? _runSpeed : _walkSpeed;
        if (_inputManager.Move == Vector2.zero) targetSpeed = 0.1f;

        _currentVelocity.x = Mathf.Lerp(_currentVelocity.x, _inputManager.Move.x * targetSpeed, _animationBlendSpeed * Time.fixedDeltaTime);
        _currentVelocity.y = Mathf.Lerp(_currentVelocity.y, _inputManager.Move.y * targetSpeed, _animationBlendSpeed * Time.fixedDeltaTime);

        // 1. ѕолучаем мировую скорость и переводим еЄ в локальные оси
        Vector3 worldVel = _rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformDirection(worldVel);

        // 2. —читаем разницу в локальных координатах
        dx = _currentVelocity.x - localVel.x;
        dz = _currentVelocity.y - localVel.z;
        Vector3 localDelta = new Vector3(dx, 0f, dz);

        // 3. ѕреобразуем обратно в мировой базис и пушим
        Vector3 worldDelta = transform.TransformDirection(localDelta);
        _rb.AddForce(worldDelta, ForceMode.VelocityChange);

        _animator.SetFloat(_velocityXHash, _currentVelocity.x);
        _animator.SetFloat(_velocityYHash, _currentVelocity.y);
    }

    private void CamMovements()
    {
        if (!_hasAnimator) return;

        var Mouse_X = _inputManager.Look.x;
        var Mouse_Y = _inputManager.Look.y;
        _camera.position = _cameraRoot.position;

        _xRotation -= Mouse_Y * _mouseSensitivity * Time.deltaTime;
        _xRotation = Mathf.Clamp(_xRotation, _upperLimit, _bottomLimit);
        _camera.localRotation = Quaternion.Euler(_xRotation, 0, 0);

        _headYawOffset += Mouse_X * _mouseSensitivity * Time.deltaTime;
        _headYawOffset = Mathf.Clamp(_headYawOffset, -_maxHeadAngle, _maxHeadAngle);
        
        _yRotation += Mouse_X * _mouseSensitivity * Time.deltaTime;

        transform.rotation = Quaternion.Euler(0, _yRotation, 0);

        Debug.Log($"Mouse X: {_inputManager.Look.x}, Mouse Y: {_inputManager.Look.y}");
        Debug.Log($"X Rotation: {_xRotation}");
        Debug.Log($"Camera.localRotation: {_camera.localRotation.eulerAngles}");
        Debug.Log($"Player.rotation: {transform.rotation.eulerAngles}");
    }
}
