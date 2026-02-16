using UnityEngine;
using System.Collections;
using System; // Necessario per Action

public class DummyController : MonoBehaviour, IDamageable
{
    public enum DummyType { Enemy, Prince }

    [Header("--- CONFIGURAZIONE BASE ---")]
    [Tooltip("Tipo di entità (Nemico o Principe da difendere).")]
    public DummyType type = DummyType.Enemy;

    [Tooltip("Se TRUE, respawna dopo la morte (utile per test). Se FALSE viene distrutto.")]
    public bool autoRespawn = false;

    [Tooltip("Se VERO: Abilita logiche DPS/HPS e Reset posizione. Se FALSO: Nemico standard.")]
    public bool isTrainingDummy = false;

    [Header("--- PROGRESSIONE (Livelli) ---")]
    [SerializeField] private int level = 1;

    [Header("--- STATISTICHE ---")]
    public float maxHealth = 1000f;
    public float currentHealth;
    public float currentShield = 0f;

    // EVENTO MORTE: Passa (Posizione, Livello)
    public Action<Vector3, int> OnDeath;

    [Header("--- STATI ALTERATI (Ghiaccio & Slow) ---")]
    [Range(0, 100)] public float currentSlowPercent = 0f;
    public float slowDecayRate = 20f;
    public float freezeDuration = 3.0f;
    public bool isFrozen = false;

    [Header("--- RIFERIMENTI VISUALI ---")]
    public GameObject floatingTextPrefab;
    public Transform popupSpawnPoint;
    public Renderer meshRenderer;
    public DummyStatusUI statusUI;
    public GameObject shieldVisualObject;

    [Header("--- CONFIGURAZIONE DPS (Solo Training) ---")]
    public float combatResetTime = 2.0f;

    // Variabili interne Private
    private Color baseColor;
    private Color freezeColor = new Color(0, 1, 1, 1);
    private Coroutine flashRoutine;
    private float freezeTimer = 0f;
    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;

    // Variabili DPS
    private float totalDamageDealt = 0;
    private float totalHealingDone = 0;
    private float combatStartTime = 0;
    private float lastHitTime = 0;
    private bool inCombat = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (meshRenderer) baseColor = meshRenderer.material.color;
    }

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        // Imposta Tag e Vita Iniziale
        if (type == DummyType.Prince)
        {
            currentHealth = maxHealth * 0.2f; // Principe parte ferito
            gameObject.tag = "Principe";
        }
        else
        {
            // Se currentHealth è 0, inizializza al massimo. Altrimenti tieni valore impostato (es. da SetLevel)
            if (currentHealth <= 0) currentHealth = maxHealth;
            gameObject.tag = "Nemico";
        }

        if (shieldVisualObject) shieldVisualObject.SetActive(false);

        UpdateUI();
    }

    // ----------------------------------------------------------------------
    // METODO DI INIZIALIZZAZIONE LIVELLO (Chiamato dall'HordeManager)
    // ----------------------------------------------------------------------
    public void InitializeLevel(int newLevel, LevelingConfig config)
    {
        level = newLevel;

        // 1. Applica Scaling HP
        float hpMult = 1.0f + ((level - 1) * config.hpPerLevel);
        maxHealth *= hpMult;
        currentHealth = maxHealth;

        // 2. Applica Scaling Grandezza (Visuale)
        float scaleMult = 1.0f + ((level - 1) * config.scalePerLevel);
        transform.localScale = Vector3.one * scaleMult;

        // 3. Applica Colore da Gradiente (Visuale)
        // Normalizziamo il livello su una scala 0-10 (Livello 1=0, Livello 11+=1)
        float t = Mathf.Clamp01((level - 1) / 10f);
        Color levelColor = config.levelColorGradient.Evaluate(t);

        if (meshRenderer)
        {
            baseColor = levelColor;
            meshRenderer.material.color = baseColor;
        }

        UpdateUI();
    }

    void Update()
    {
        // Logica DPS solo se è un Training Dummy
        if (isTrainingDummy) HandleDPSLogic();

        HandleStatusRecovery();
        UpdateVisualColor();
        UpdateShieldVisual();
    }

    // ----------------------------------------------------------------------
    // INTERFACCIA IDAMAGEABLE
    // ----------------------------------------------------------------------
    public void TakeDamage(float amount)
    {
        if (type == DummyType.Prince) { SpawnPopup("0", Color.grey, 3f); return; }

        if (isTrainingDummy) CheckCombatStart();

        float effectiveDamage = amount;

        // Bonus danno su nemici congelati
        if (isFrozen) effectiveDamage *= 1.5f;

        // Assorbimento Scudo
        if (currentShield > 0)
        {
            float shieldAbsorb = Mathf.Min(currentShield, amount);
            currentShield -= shieldAbsorb;
            effectiveDamage -= shieldAbsorb;
            SpawnPopup($"-{shieldAbsorb:F0} SHLD", Color.yellow, 3f);
        }

        // Danno effettivo
        if (effectiveDamage > 0)
        {
            currentHealth -= effectiveDamage;
            if (isTrainingDummy) totalDamageDealt += effectiveDamage;

            SpawnPopup($"-{effectiveDamage:F0}", Color.red, 5f);
            Flash(Color.white); // Flash bianco classico
        }

        UpdateUI();
        if (currentHealth <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (type == DummyType.Enemy) return; // I nemici non si curano di solito

        if (isTrainingDummy) CheckCombatStart();

        float healAmount = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healAmount;
        if (isTrainingDummy) totalHealingDone += healAmount;

        SpawnPopup($"+{healAmount:F0} HP", Color.green, 5f);
        UpdateUI();
    }

    public void AddShield(float amount)
    {
        if (type == DummyType.Enemy) return;

        currentShield += amount;
        SpawnPopup($"+{amount:F0} SHLD", Color.yellow, 4f);
        UpdateUI();
    }

    public void ApplySlow(float percentage, float duration)
    {
        if (type == DummyType.Prince) return;
        if (isFrozen) return;

        currentSlowPercent += percentage;

        if (currentSlowPercent >= 100f)
        {
            currentSlowPercent = 100f;
            StartFreeze();
        }
        else
        {
            SpawnPopup($"-{percentage}% SPD", Color.cyan, 3f);
        }
        UpdateUI();
    }

    public void ApplyKnockback(Vector3 force)
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    // ----------------------------------------------------------------------
    // STATI E MORTE
    // ----------------------------------------------------------------------
    void StartFreeze()
    {
        isFrozen = true;
        freezeTimer = freezeDuration;
        SpawnPopup("FROZEN! ❄", Color.cyan, 5f);
        if (rb) rb.linearVelocity = Vector3.zero;
    }

    void BreakFreeze()
    {
        isFrozen = false;
        currentSlowPercent = 0f;
        SpawnPopup("THAWED", Color.white, 3f);
    }

    void HandleStatusRecovery()
    {
        if (isFrozen)
        {
            freezeTimer -= Time.deltaTime;
            if (freezeTimer <= 0) BreakFreeze();
        }
        else if (currentSlowPercent > 0)
        {
            currentSlowPercent -= slowDecayRate * Time.deltaTime;
            if (currentSlowPercent < 0) currentSlowPercent = 0;
        }

        if (currentSlowPercent > 0 || isFrozen) UpdateUI();
    }

    void Die()
    {
        // Notifica l'HordeManager passando Posizione e Livello
        OnDeath?.Invoke(transform.position, level);

        SpawnPopup("DISTRUTTO", Color.grey, 7f);
        gameObject.SetActive(false);

        if (autoRespawn)
        {
            Invoke(nameof(Respawn), 2f);
        }
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }

    // ----------------------------------------------------------------------
    // UTILITIES & VISUALS
    // ----------------------------------------------------------------------
    void UpdateUI()
    {
        if (statusUI)
        {
            statusUI.UpdateHealth(currentHealth, maxHealth);
            statusUI.UpdateShield(currentShield, maxHealth);
            statusUI.UpdateSlow(currentSlowPercent);
        }
    }

    void UpdateShieldVisual()
    {
        if (shieldVisualObject) shieldVisualObject.SetActive(currentShield > 1f);
    }

    void UpdateVisualColor()
    {
        if (!meshRenderer || flashRoutine != null) return;

        if (isFrozen)
            meshRenderer.material.color = freezeColor;
        else if (currentSlowPercent > 0)
            meshRenderer.material.color = Color.Lerp(baseColor, freezeColor, currentSlowPercent / 100f);
        else
            meshRenderer.material.color = baseColor;
    }

    void Flash(Color c)
    {
        if (!meshRenderer) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(c));
    }

    IEnumerator FlashRoutine(Color c)
    {
        meshRenderer.material.color = c;
        yield return new WaitForSeconds(0.1f);
        flashRoutine = null;
        UpdateVisualColor();
    }

    void SpawnPopup(string text, Color color, float size = 4f)
    {
        if (!floatingTextPrefab) return;

        Vector3 spawnPos = popupSpawnPoint ? popupSpawnPoint.position : transform.position + Vector3.up * 2f;
        GameObject obj = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);
        FloatingText ft = obj.GetComponent<FloatingText>();
        if (ft) ft.Setup(text, color, size);
    }

    // Logica Reset Manichino
    void CheckCombatStart() { lastHitTime = Time.time; if (!inCombat) { inCombat = true; combatStartTime = Time.time; totalDamageDealt = 0; totalHealingDone = 0; } }
    void HandleDPSLogic() { if (inCombat && Time.time > lastHitTime + combatResetTime) { inCombat = false; ReportDPS(); ResetPosition(); } }
    void ReportDPS() { float d = lastHitTime - combatStartTime; if (d < 0.1f) d = 1f; float dps = totalDamageDealt / d; if (dps > 0) SpawnPopup($"DPS: {dps:F1}", Color.white, 5f); }
    void ResetPosition() { if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } transform.position = startPosition; transform.rotation = startRotation; }
    void Respawn() { gameObject.SetActive(true); ResetPosition(); if (type == DummyType.Prince) currentHealth = maxHealth * 0.2f; else currentHealth = maxHealth; currentShield = 0; currentSlowPercent = 0; isFrozen = false; UpdateUI(); }
}