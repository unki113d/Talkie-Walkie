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

    [Header("Foot Trigger")]
    [SerializeField] private Collider _footTrigger;

    private InputManager _inputManager;
    private Rigidbody _rb;
    private Animator _animator;
    public bool _isGrounded { get; private set; }
    private bool _jumpRequested;
    private bool _hasLeftGround = false;

    private int _jumpHash;
    private int _inAirHash;
    private int _landHash;

    void Start()
    {
        if (!isLocalPlayer)
        { enabled = false; return; }

        _inputManager = GetComponent<InputManager>();
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _jumpHash = Animator.StringToHash("JumpStart");
        _inAirHash = Animator.StringToHash("InAir");
        _landHash = Animator.StringToHash("Land");

        _isGrounded = CheckGrounded();
        _animator.SetBool(_inAirHash, false);
        _hasLeftGround = false;
    }

    void Update()
    {
        if (_inputManager.Jump && _isGrounded)
        {
            _jumpRequested = true;
        }
    }

    void FixedUpdate()
    {
        // Ground Detection
        _isGrounded = CheckGrounded();

        // Jump
        if (_jumpRequested)
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            _animator.SetTrigger(_jumpHash);
            _jumpRequested = false;
        }

        // InAir 
        if (!_isGrounded && _rb.linearVelocity.y < 0f)
        {

            _animator.SetBool(_inAirHash, true);
            _hasLeftGround = true;
        }
    }

    private bool CheckGrounded()
    {
        Vector3 origin = groundCheck.position + Vector3.up * 0.05f;
        RaycastHit hit;

        float maxDist = groundRadius + 0.1f;
        bool hasHit = Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            maxDist,
            ~0,  // layerMask = ~0 (All layers)
            QueryTriggerInteraction.Ignore
        );

        if (!hasHit)
            return false;

        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        return slopeAngle <= maxSlopeAngle;
    }
    private void OnTriggerEnter(Collider other)
    {

        // тут самый момент приземления
        _animator.SetBool(_inAirHash, false);
        _animator.SetTrigger(_landHash);
        _hasLeftGround = false;
    }
    private void OnDrawGizmosSelected()
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

