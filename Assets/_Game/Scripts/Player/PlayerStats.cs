using UnityEngine;
using System.Collections;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("--- VITA ---")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("--- STAMINA ---")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaRegenRate = 15f;
    [Tooltip("Quanto tempo resti esausto (senza rigenerare) dopo aver toccato 0 stamina.")]
    public float exhaustionDuration = 2.0f;

    [Header("--- STATI ---")]
    public bool isExhausted = false; // Se true, sei rallentato e non puoi attaccare
    public bool isShielded = false; // Gestito dal PlayerController (Giallo)

    // Eventi per aggiornare la UI
    public System.Action OnStatsChanged;

    private float regenTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        if (OnStatsChanged != null) OnStatsChanged.Invoke();
    }

    void Update()
    {
        HandleStaminaRegen();
    }

    void HandleStaminaRegen()
    {
        // Se siamo esausti, aspettiamo che finisca il timer
        if (isExhausted)
        {
            regenTimer -= Time.deltaTime;
            if (regenTimer <= 0)
            {
                isExhausted = false; // Recupero completato
                OnStatsChanged?.Invoke();
            }
            return;
        }

        // Rigenerazione normale se non stiamo spendendo stamina (controllato esternamente o implicitamente)
        // Nota: Il PlayerController bloccherà la regen se sta tenendo premuto lo scudo
        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            if (currentStamina > maxStamina) currentStamina = maxStamina;
            OnStatsChanged?.Invoke();
        }
    }

    // --- API PER PLAYER CONTROLLER ---

    public bool HasStamina(float amount)
    {
        return !isExhausted && currentStamina >= amount;
    }

    public void ConsumeStamina(float amount)
    {
        if (isExhausted) return;

        currentStamina -= amount;

        // Se andiamo a 0 o sotto zero (Overdraw)
        if (currentStamina <= 0)
        {
            currentStamina = 0; // Clampa a 0
            StartExhaustion();  // Triggera lo stato grigio/lento
        }

        OnStatsChanged?.Invoke();
    }

    public void ConsumeStaminaOverTime(float amountPerSecond)
    {
        if (isExhausted) return;

        currentStamina -= amountPerSecond * Time.deltaTime;
        if (currentStamina <= 0)
        {
            currentStamina = 0;
            StartExhaustion();
        }
        OnStatsChanged?.Invoke();
    }

    void StartExhaustion()
    {
        isExhausted = true;
        regenTimer = exhaustionDuration;
        Debug.Log("<color=red>PLAYER ESAUSTO!</color>");
        // Qui potresti suonare un audio di respiro affannoso
    }

    // --- INTERFACCIA IDAMAGEABLE (Per i nemici) ---

    public void TakeDamage(float amount)
    {
        if (isShielded)
        {
            // Se scudato, il danno scala dalla stamina invece che dalla vita!
            float staminaDmg = amount * 0.5f; // Esempio: para il danno costa metà in stamina
            ConsumeStamina(staminaDmg);
            Debug.Log("Danno parato con Stamina!");
            return;
        }

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        OnStatsChanged?.Invoke();
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        OnStatsChanged?.Invoke();
    }

    public void AddShield(float amount) { } // Gestito diversamente, o implementabile se vuoi scudi extra
    public void ApplySlow(float percentage, float duration) { } // Implementare se i nemici ti rallentano

    void Die()
    {
        Debug.Log("PLAYER MORTO");
        // Logica Game Over
    }
}