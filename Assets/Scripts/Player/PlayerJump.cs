using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Utils;

public class PlayerJump : NetworkBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 7f;

    [Header("Ground Detection")]
    [SerializeField, Range(0f, 90f)] private float maxSlopeAngle = 60f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Foot Trigger")]
    [SerializeField] private Collider _footTrigger;

    private InputManager _inputManager;
    private Rigidbody _rb;
    private Animator _animator;
    public bool _isGrounded;
    private bool _jumpRequested;
    public bool _hasLeftGround = false;
    private bool _prevJumpInput;
    private bool _wasGrounded;
    bool cur;

    private int _jumpHash;
    private int _inAirHash;
    private int _landHash;
    void Awake()
    {
        // инициализируем хэши до любых вызовов SetBool/SetTrigger
        _jumpHash = Animator.StringToHash("JumpStart");
        _inAirHash = Animator.StringToHash("InAir");
        _landHash = Animator.StringToHash("Land");
    }
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        _inputManager = GetComponent<InputManager>();
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        _isGrounded = CheckGrounded();
        _animator.SetBool(_inAirHash, false);
        _hasLeftGround = false;
        _jumpRequested = false;
        _wasGrounded = _isGrounded;
        _prevJumpInput = false;
    }

    void Start()
    {
        if (!isLocalPlayer)
        { enabled = false; return; }
    }

    void Update()
    {
        cur = _inputManager.Jump;
        if (cur && !_prevJumpInput && _isGrounded)   // только при нажатии
            _jumpRequested = true;

        _prevJumpInput = cur;
    }

    void FixedUpdate()
    {
        // Ground Detection
        _isGrounded = CheckGrounded();

        // Jump
        if (_jumpRequested && _isGrounded)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            _animator.SetTrigger(_jumpHash);
            _jumpRequested = false;
        }

        if (_wasGrounded && !_isGrounded)
        {
            // уход с земли вниз — считаем, что реально оторвались
            _animator.SetBool(_inAirHash, true);
            _hasLeftGround = true;
        }

        if (!_wasGrounded && _isGrounded && _hasLeftGround)
        {
            // приземление
            _animator.SetTrigger(_landHash);
            _animator.SetBool(_inAirHash, false);
            _hasLeftGround = false;
        }

        // обновляем предыдущее состояние
        _wasGrounded = _isGrounded;
    }

    private bool CheckGrounded()
    {
        // Позиция от которой начинаем сферокаст чуть выше ноги
        float maxDist = groundRadius + 0.1f;
        if (Physics.SphereCast(groundCheck.position, groundRadius, Vector3.down, out RaycastHit hit, maxDist, groundLayer, QueryTriggerInteraction.Ignore))
        {
            // Проверяем угол наклона
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle <= maxSlopeAngle;
        }

        return false;
    }
    private void OnTriggerEnter(Collider other)
    {

        // тут самый момент приземления
        _animator.SetBool(_inAirHash, false);
        _animator.SetTrigger(_landHash);
        _hasLeftGround = false;
    }
    private void OnDrawGizmos()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        // Для визуализации направления SphereCast:
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            groundCheck.position,
            groundCheck.position + Vector3.down * (groundRadius + 0.1f)
        );
    }

}

