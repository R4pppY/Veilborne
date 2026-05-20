using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hollow Knight-style 2D platformer controller.
/// Features: acceleration/deceleration, coyote time, jump buffering,
/// variable jump height, wall jump (ability-gated), dash (ability-gated).
/// Requires: Rigidbody2D, CapsuleCollider2D, PlayerInputActions
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    // ─── Movement Settings ────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] float _moveSpeed       = 8f;
    [SerializeField] float _acceleration    = 60f;
    [SerializeField] float _deceleration    = 80f;
    [SerializeField] float _airAcceleration = 35f;

    [Header("Jump")]
    [SerializeField] float _jumpForce          = 18f;
    [SerializeField] float _jumpCutMultiplier  = 0.4f;
    [SerializeField] float _coyoteTime         = 0.12f;
    [SerializeField] float _jumpBufferTime     = 0.15f;
    [SerializeField] float _fallGravityScale   = 4.5f;
    [SerializeField] float _jumpGravityScale   = 2.5f;

    [Header("Wall Jump")]
    [SerializeField] float _wallJumpForceX     = 10f;
    [SerializeField] float _wallJumpForceY     = 15f;
    [SerializeField] float _wallSlideSpeed     = 2f;
    [SerializeField] LayerMask _wallLayer;

    [Header("Dash")]
    [SerializeField] float _dashSpeed          = 20f;
    [SerializeField] float _dashDuration       = 0.18f;
    [SerializeField] float _dashCooldown       = 0.6f;

    [Header("Ground Check")]
    [SerializeField] Transform _groundCheck;
    [SerializeField] float     _groundCheckRadius = 0.1f;
    [SerializeField] LayerMask _groundLayer;

    [Header("Wall Check")]
    [SerializeField] Transform _wallCheckRight;
    [SerializeField] Transform _wallCheckLeft;
    [SerializeField] float     _wallCheckDistance = 0.1f;

    // ─── Components ───────────────────────────────────────────────────────────
    Rigidbody2D        _rb;
    PlayerInputActions _input;
    PlayerAnimator     _animator;

    // ─── State ────────────────────────────────────────────────────────────────
    float _moveInput;
    bool  _isGrounded;
    bool  _isTouchingWallRight;
    bool  _isTouchingWallLeft;
    bool  _isTouchingWall  => _isTouchingWallRight || _isTouchingWallLeft;
    bool  _isWallSliding;
    bool  _isDashing;
    bool  _canDash         = true;
    float _dashCooldownTimer;
    float _coyoteTimer;
    float _jumpBufferTimer;
    bool  _isJumping;
    bool  _isAttacking;
    bool  _facingRight     = true;
    int   _airJumpsLeft;

    // Ability flags
    bool _hasWallJump   => GameManager.Instance != null && GameManager.Instance.HasAbility(0);
    bool _hasDash       => GameManager.Instance != null && GameManager.Instance.HasAbility(1);
    bool _hasDoubleJump => GameManager.Instance != null && GameManager.Instance.HasAbility(2);

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────
    void Awake()
    {
        _rb       = GetComponent<Rigidbody2D>();
        _animator = GetComponent<PlayerAnimator>();
        _input    = new PlayerInputActions();
        _input.Player.Jump.performed   += _ => OnJumpPressed();
        _input.Player.Jump.canceled    += _ => OnJumpReleased();
        _input.Player.Dash.performed   += _ => OnDashPressed();
        _input.Player.Attack.performed += _ => OnAttackPressed();
    }

    void OnEnable()  => _input.Enable();
    void OnDisable() => _input.Disable();

    void Update()
    {
        _moveInput = _input.Player.Move.ReadValue<Vector2>().x;
        CheckGrounded();
        CheckWalls();
        HandleCoyoteTime();
        HandleJumpBuffer();
        HandleWallSlide();
        HandleDashTimer();
        HandleFlip();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (_isDashing) return;
        HandleMovement();
        HandleGravity();
    }

    // ─── Detection ────────────────────────────────────────────────────────────
    void CheckGrounded()
    {
        bool wasGrounded = _isGrounded;
        _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);
        if (_isGrounded && !wasGrounded)
        {
            _isJumping    = false;
            _airJumpsLeft = _hasDoubleJump ? 1 : 0;
            _canDash      = true;
        }
    }

    void CheckWalls()
    {
        _isTouchingWallRight = Physics2D.Raycast(_wallCheckRight.position, Vector2.right, _wallCheckDistance, _wallLayer);
        _isTouchingWallLeft  = Physics2D.Raycast(_wallCheckLeft.position,  Vector2.left,  _wallCheckDistance, _wallLayer);
    }

    // ─── Coyote & Buffer ──────────────────────────────────────────────────────
    void HandleCoyoteTime()
    {
        if (_isGrounded) _coyoteTimer = _coyoteTime;
        else             _coyoteTimer -= Time.deltaTime;
    }

    void HandleJumpBuffer()
    {
        _jumpBufferTimer -= Time.deltaTime;
        if (_jumpBufferTimer > 0 && (_coyoteTimer > 0 || CanWallJump()))
        {
            ExecuteJump();
            _jumpBufferTimer = 0;
        }
    }

    // ─── Input ────────────────────────────────────────────────────────────────
    void OnJumpPressed()
    {
        _jumpBufferTimer = _jumpBufferTime;
        if (_coyoteTimer > 0)
            ExecuteJump();
        else if (CanWallJump())
            ExecuteWallJump();
        else if (_hasDoubleJump && _airJumpsLeft > 0)
        {
            ExecuteJump();
            _airJumpsLeft--;
        }
    }

    void OnJumpReleased()
    {
        if (_rb.linearVelocity.y > 0 && _isJumping)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * _jumpCutMultiplier);
    }

    void OnDashPressed()
    {
        if (!_hasDash || !_canDash || _isDashing || _dashCooldownTimer > 0) return;
        StartCoroutine(ExecuteDash());
    }

    void OnAttackPressed()
    {
        if (_isAttacking) return;
        _isAttacking = true;
        _animator?.TriggerAttack();
        StartCoroutine(ResetAttack());
    }

    IEnumerator ResetAttack()
    {
        // Wait for Attack1 + Attack1End clips to finish
        yield return new WaitForSeconds(0.6f);
        _isAttacking = false;
    }

    // ─── Actions ──────────────────────────────────────────────────────────────
    void ExecuteJump()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
        _isJumping   = true;
        _coyoteTimer = 0;
    }

    bool CanWallJump() => _hasWallJump && _isTouchingWall && !_isGrounded;

    void ExecuteWallJump()
    {
        float dir = _isTouchingWallRight ? -1f : 1f;
        _rb.linearVelocity = new Vector2(dir * _wallJumpForceX, _wallJumpForceY);
        _isJumping = true;
        Flip();
    }

    System.Collections.IEnumerator ExecuteDash()
    {
        _isDashing         = true;
        _canDash           = false;
        _dashCooldownTimer = _dashCooldown;
        float dir          = _facingRight ? 1f : -1f;
        _rb.gravityScale   = 0f;
        _rb.linearVelocity = new Vector2(dir * _dashSpeed, 0f);
        yield return new WaitForSeconds(_dashDuration);
        _rb.gravityScale   = _jumpGravityScale;
        _isDashing         = false;
    }

    void HandleDashTimer()
    {
        if (_dashCooldownTimer > 0) _dashCooldownTimer -= Time.deltaTime;
    }

    // ─── Physics ──────────────────────────────────────────────────────────────
    void HandleMovement()
    {
        float targetSpeed  = _moveInput * _moveSpeed;
        float currentSpeed = _rb.linearVelocity.x;
        float accel        = _isGrounded ? _acceleration    : _airAcceleration;
        float decel        = _isGrounded ? _deceleration    : _airAcceleration;
        float speedDiff    = targetSpeed - currentSpeed;
        float force        = Mathf.Abs(_moveInput) > 0.01f ? speedDiff * accel : -currentSpeed * decel;

        _rb.AddForce(new Vector2(force, 0f), ForceMode2D.Force);
        _rb.linearVelocity = new Vector2(
            Mathf.Clamp(_rb.linearVelocity.x, -_moveSpeed, _moveSpeed),
            _rb.linearVelocity.y);
    }

    void HandleGravity()
    {
        if (_isWallSliding)
        {
            _rb.gravityScale   = 0f;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x,
                Mathf.Max(_rb.linearVelocity.y, -_wallSlideSpeed));
            return;
        }
        _rb.gravityScale = _rb.linearVelocity.y < 0 ? _fallGravityScale : _jumpGravityScale;
    }

    void HandleWallSlide()
    {
        _isWallSliding = _hasWallJump && _isTouchingWall && !_isGrounded
                         && _rb.linearVelocity.y < 0 && Mathf.Abs(_moveInput) > 0.01f;
    }

    // ─── Flip ─────────────────────────────────────────────────────────────────
    void HandleFlip()
    {
        if (_moveInput > 0.01f  && !_facingRight) Flip();
        if (_moveInput < -0.01f &&  _facingRight) Flip();
    }

    void Flip()
    {
        _facingRight         = !_facingRight;
        transform.localScale = new Vector3(_facingRight ? 1f : -1f,
            transform.localScale.y, transform.localScale.z);
    }

    void UpdateAnimator()
    {
        _animator?.SetGrounded(_isGrounded);
        _animator?.SetVelocity(_rb.linearVelocity);
        _animator?.SetWallSliding(_isWallSliding);
        _animator?.SetDashing(_isDashing);
    }

    // ─── Editor Gizmos ────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (_groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_groundCheck.position, _groundCheckRadius);
        }
        if (_wallCheckRight != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_wallCheckRight.position, Vector2.right * _wallCheckDistance);
        }
        if (_wallCheckLeft != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_wallCheckLeft.position, Vector2.left * _wallCheckDistance);
        }
    }
}