using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount);
    void Heal(float amount);
    void AddShield(float amount);
    void ApplySlow(float percentage, float duration);
}