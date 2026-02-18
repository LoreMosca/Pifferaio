using UnityEngine;
using System.Collections;
using System;

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
    public bool isExhausted = false;
    public bool isShielded = false;

    // --- NUOVO: MOLTIPLICATORI BUFF ---
    [Header("--- MODIFICATORI BUFF ---")]
    public float damageMultiplier = 1.0f; // 1.0 = 100% (Normale)
    public float speedMultiplier = 1.0f;  // 1.0 = 100% (Normale)

    [Header("--- FEEDBACK VISIVO ---")]
    [Tooltip("Assegna qui il prefab PF_FloatingText per vedere i danni.")]
    public GameObject floatingTextPrefab;
    [Tooltip("Punto sopra la testa dove spawnare il testo.")]
    public Transform popupSpawnPoint;

    // Eventi
    public Action OnStatsChanged;
    public Action OnTakeDamage; // Usato dal Controller per il Camera Shake
    public Action OnDeath;

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
        if (isExhausted)
        {
            regenTimer -= Time.deltaTime;
            if (regenTimer <= 0)
            {
                isExhausted = false;
                Debug.Log("<color=green>STAMINA RECUPERATA!</color>");
                OnStatsChanged?.Invoke();
            }
        }
        else
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                if (currentStamina > maxStamina) currentStamina = maxStamina;
                OnStatsChanged?.Invoke();
            }
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
        if (currentStamina <= 0)
        {
            currentStamina = 0;
            StartExhaustion();
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
        SpawnPopup("ESAUSTO!", Color.gray);
    }

    // --- INTERFACCIA IDAMAGEABLE ---

    public void TakeDamage(float amount)
    {
        if (isShielded)
        {
            float staminaDmg = amount * 0.5f;
            ConsumeStamina(staminaDmg);
            SpawnPopup("PARATO!", Color.yellow);
            return;
        }

        currentHealth -= amount;

        // FEEDBACK: Numeri rossi + Evento Shake
        SpawnPopup($"-{amount:F0}", Color.red);
        OnTakeDamage?.Invoke();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        OnStatsChanged?.Invoke();
    }

    public void Heal(float amount)
    {
        float effectiveHeal = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += effectiveHeal;

        if (effectiveHeal > 0)
            SpawnPopup($"+{effectiveHeal:F0}", Color.green);

        OnStatsChanged?.Invoke();
    }

    public void AddShield(float amount)
    {
        // CORREZIONE: Era Color.cyan (Blu), ora è Color.yellow (Giallo) come le note
        SpawnPopup($"+{amount:F0} SHIELD", Color.yellow);
    }

    public void ApplySlow(float percentage, float duration) { }

    void Die()
    {
        Debug.Log("PLAYER MORTO");
        SpawnPopup("MORTO", Color.black);

        OnDeath?.Invoke();
        gameObject.SetActive(false);
    }

    // --- HELPER POPUP ---
    public void SpawnPopup(string text, Color color)
    {
        if (floatingTextPrefab)
        {
            Vector3 pos = popupSpawnPoint ? popupSpawnPoint.position : transform.position + Vector3.up * 2f;
            GameObject obj = Instantiate(floatingTextPrefab, pos, Quaternion.identity);
            var ft = obj.GetComponent<FloatingText>();
            // Usiamo size 4f come default ben visibile
            if (ft) ft.Setup(text, color, 4f);
        }
    }
}