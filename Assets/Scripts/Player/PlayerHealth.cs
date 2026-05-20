using System;
using UnityEngine;

/// <summary>
/// Manages player health, invincibility frames, hit reaction, and death.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] int   _maxHealth      = 5;
    [SerializeField] float _invincibleTime = 1.2f;

    public int  MaxHealth     => _maxHealth;
    public int  CurrentHealth { get; private set; }
    public bool IsInvincible  { get; private set; }
    public bool IsDead        { get; private set; }

    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action           OnPlayerDied;

    float _invincibleTimer;

    void Start()
    {
        CurrentHealth = _maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
    }

    void Update()
    {
        if (!IsInvincible) return;
        _invincibleTimer -= Time.deltaTime;
        if (_invincibleTimer <= 0) IsInvincible = false;
    }

    public void TakeDamage(int amount)
    {
        if (IsInvincible || IsDead) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        if (CurrentHealth <= 0) Die();
        else StartInvincibility();
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
    }

    void StartInvincibility()
    {
        IsInvincible     = true;
        _invincibleTimer = _invincibleTime;
    }

    void Die()
    {
        IsDead = true;
        OnPlayerDied?.Invoke();
        Invoke(nameof(Respawn), 1.5f);
    }

    void Respawn() => GameManager.Instance?.RespawnAtCheckpoint();
}