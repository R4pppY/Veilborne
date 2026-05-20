using UnityEngine;

/// <summary>
/// Bridges PlayerController state to the Animator.
/// Attach to the same GameObject as PlayerController.
/// All methods are null-safe — won't break without an Animator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    Animator _anim;

    // Animator parameter hashes (faster than strings)
    static readonly int SpeedX      = Animator.StringToHash("SpeedX");
    static readonly int SpeedY      = Animator.StringToHash("SpeedY");
    static readonly int IsGrounded  = Animator.StringToHash("IsGrounded");
    static readonly int IsWallSlide = Animator.StringToHash("IsWallSliding");
    static readonly int IsDashing   = Animator.StringToHash("IsDashing");
    static readonly int AttackTrig  = Animator.StringToHash("Attack");

    void Awake() => _anim = GetComponent<Animator>();

    public void SetVelocity(Vector2 velocity)
    {
        _anim.SetFloat(SpeedX, Mathf.Abs(velocity.x));
        _anim.SetFloat(SpeedY, velocity.y);
    }

    public void SetGrounded(bool grounded)     => _anim.SetBool(IsGrounded,  grounded);
    public void SetWallSliding(bool sliding)   => _anim.SetBool(IsWallSlide, sliding);
    public void SetDashing(bool dashing)       => _anim.SetBool(IsDashing,   dashing);
    public void TriggerAttack()                => _anim.SetTrigger(AttackTrig);
}