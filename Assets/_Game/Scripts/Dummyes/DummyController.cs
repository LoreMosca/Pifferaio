using UnityEngine;
using System.Collections;
using System;
using TMPro;

public class DummyController : MonoBehaviour, IDamageable
{
    public enum DummyType { Enemy, Prince }

    [Header("--- RIFERIMENTI VISUALI ---")]
    public Animator animator;

    // --- MODIFICA QUI: Da singolo a Array per supportare Body, Jaw, Skull ---
    [Tooltip("Assegna qui tutte le parti della mesh (Body, Skull, Jaw).")]
    public Renderer[] meshRenderers;
    // ------------------------------------------------------------------------

    public Transform popupSpawnPoint;
    public GameObject floatingTextPrefab;
    public DummyStatusUI statusUI;
    public GameObject shieldVisualObject;

    [Header("--- CONFIGURAZIONE BASE ---")]
    [Tooltip("Tipo di entità (Nemico o Principe da difendere).")]
    public DummyType type = DummyType.Enemy;

    [Tooltip("Se TRUE, respawna dopo la morte. Se FALSE viene distrutto.")]
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

    [Header("--- CONFIGURAZIONE DPS (Solo Training) ---")]
    public float combatResetTime = 2.0f;

    // Variabili interne Private
    private Color baseColor;
    private Color freezeColor = new Color(0, 1, 1, 1);
    private Coroutine flashRoutine;
    private float freezeTimer = 0f;
    private Rigidbody rb;
    private Collider col;
    private Vector3 startPosition;
    private Quaternion startRotation;

    // Variabili DPS (LOGICA PRESERVATA)
    private float totalDamageDealt = 0;
    private float totalHealingDone = 0;
    private float combatStartTime = 0;
    private float lastHitTime = 0;
    private bool inCombat = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (animator == null) animator = GetComponentInChildren<Animator>();

        // --- MODIFICA: Auto-riempimento Array ---
        // Se l'array è vuoto, cerca tutti i renderer nei figli
        if (meshRenderers == null || meshRenderers.Length == 0)
        {
            meshRenderers = GetComponentsInChildren<Renderer>();
        }

        // Prende il colore base dal primo renderer trovato
        if (meshRenderers.Length > 0 && meshRenderers[0] != null)
        {
            baseColor = meshRenderers[0].material.color;
        }
    }

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        if (type == DummyType.Prince)
        {

            currentHealth = maxHealth;
            gameObject.tag = "Principe";
        }
        else
        {
            if (currentHealth <= 0) currentHealth = maxHealth;
            gameObject.tag = "Nemico";
        }

        if (shieldVisualObject) shieldVisualObject.SetActive(false);

        UpdateUI();
    }

    // ----------------------------------------------------------------------
    // METODO DI INIZIALIZZAZIONE LIVELLO
    // ----------------------------------------------------------------------
    public void InitializeLevel(int newLevel, LevelingConfig config)
    {
        level = newLevel;

        float hpMult = 1.0f + ((level - 1) * config.hpPerLevel);
        maxHealth *= hpMult;
        currentHealth = maxHealth;

        float scaleMult = 1.0f + ((level - 1) * config.scalePerLevel);
        transform.localScale = Vector3.one * scaleMult;

        float t = Mathf.Clamp01((level - 1) / 10f);
        Color levelColor = config.levelColorGradient.Evaluate(t);

        baseColor = levelColor; // Aggiorna colore base

        // --- MODIFICA: Applica colore a TUTTE le parti ---
        if (meshRenderers != null)
        {
            foreach (var rend in meshRenderers)
            {
                if (rend != null) rend.material.color = baseColor;
            }
        }

        UpdateUI();
    }

    void Update()
    {
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
        if (currentHealth <= 0 && !isTrainingDummy) return;
       // if (type == DummyType.Prince) { SpawnPopup("0", Color.grey, 3f); return; }

        if (isTrainingDummy) CheckCombatStart();

        float effectiveDamage = amount;

        if (isFrozen) effectiveDamage *= 1.5f;

        if (currentShield > 0)
        {
            float shieldAbsorb = Mathf.Min(currentShield, amount);
            currentShield -= shieldAbsorb;
            effectiveDamage -= shieldAbsorb;
            SpawnPopup($"-{shieldAbsorb:F0} SHLD", Color.yellow, 3f);
        }

        if (effectiveDamage > 0)
        {
            currentHealth -= effectiveDamage;
            if (isTrainingDummy) totalDamageDealt += effectiveDamage;

            SpawnPopup($"-{effectiveDamage:F0}", Color.red, 5f);
            Flash(Color.white); // Questo ora flasha tutte le parti

            if (animator != null) animator.SetTrigger("Hit");
        }

        UpdateUI();
        if (currentHealth <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (type == DummyType.Enemy && !isTrainingDummy) return;

        if (isTrainingDummy) CheckCombatStart();

        float healAmount = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healAmount;
        if (isTrainingDummy) totalHealingDone += healAmount;

        SpawnPopup($"+{healAmount:F0} HP", Color.green, 5f);
        UpdateUI();
    }

    public void AddShield(float amount)
    {
        if (type == DummyType.Enemy && !isTrainingDummy) return;

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

    void Die()
    {
        OnDeath?.Invoke(transform.position, level);

        if (isTrainingDummy)
        {
            SpawnPopup("RESET", Color.white, 6f);
            ReportDPS();
            ResetPosition();

            currentHealth = maxHealth;
            currentShield = 0;
            inCombat = false;

            if (animator) animator.Play("Idle");
        }
        else
        {
            SpawnPopup("DISTRUTTO", Color.grey, 7f);

            if (animator != null) animator.SetTrigger("Die");

            if (col) col.enabled = false;
            if (rb) rb.isKinematic = true;

            var ai = GetComponent<RatAI>();
            if (ai) ai.enabled = false;

            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav) nav.enabled = false;

            if (statusUI) statusUI.gameObject.SetActive(false);

            if (autoRespawn)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                Destroy(gameObject, 3.0f);
            }
        }
        UpdateUI();
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        Respawn();
    }

    void Respawn()
    {
        gameObject.SetActive(true);
        ResetPosition();

        if (col) col.enabled = true;
        if (rb) rb.isKinematic = false;
        var ai = GetComponent<RatAI>(); if (ai) ai.enabled = true;
        var nav = GetComponent<UnityEngine.AI.NavMeshAgent>(); if (nav) nav.enabled = true;
        if (statusUI) statusUI.gameObject.SetActive(true);
        if (animator) animator.Play("Idle");

        if (type == DummyType.Prince) currentHealth = maxHealth * 0.2f;
        else currentHealth = maxHealth;

        currentShield = 0;
        currentSlowPercent = 0;
        isFrozen = false;
        UpdateUI();
    }

    void StartFreeze()
    {
        isFrozen = true;
        freezeTimer = freezeDuration;
        SpawnPopup("FROZEN! ❄", Color.cyan, 5f);
        if (rb) rb.linearVelocity = Vector3.zero;
        if (animator) animator.speed = 0;
    }

    void BreakFreeze()
    {
        isFrozen = false;
        currentSlowPercent = 0f;
        SpawnPopup("THAWED", Color.white, 3f);
        if (animator) animator.speed = 1;
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
        // --- MODIFICA: Controllo Array ---
        if (meshRenderers == null || meshRenderers.Length == 0 || flashRoutine != null) return;

        Color targetColor = baseColor;
        if (isFrozen) targetColor = freezeColor;
        else if (currentSlowPercent > 0) targetColor = Color.Lerp(baseColor, freezeColor, currentSlowPercent / 100f);

        // --- MODIFICA: Loop su tutti ---
        foreach (var rend in meshRenderers)
        {
            if (rend != null) rend.material.color = targetColor;
        }
    }

    void Flash(Color c)
    {
        if (meshRenderers == null || meshRenderers.Length == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(c));
    }

    IEnumerator FlashRoutine(Color c)
    {
        // --- MODIFICA: Loop su tutti ---
        foreach (var rend in meshRenderers)
        {
            if (rend != null) rend.material.color = c;
        }

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

        //obj.transform.LookAt(Camera.main.transform);
        //obj.transform.Rotate(0, 180, 0);

        if (ft) ft.Setup(text, color, size);
    }

    // ----------------------------------------------------------------------
    // LOGICA DPS / TRAINING (PRESERVATA INTEGRALMENTE)
    // ----------------------------------------------------------------------
    void CheckCombatStart()
    {
        lastHitTime = Time.time;
        if (!inCombat)
        {
            inCombat = true;
            combatStartTime = Time.time;
            totalDamageDealt = 0;
            totalHealingDone = 0;
        }
    }

    void HandleDPSLogic()
    {
        if (inCombat && Time.time > lastHitTime + combatResetTime)
        {
            inCombat = false;
            ReportDPS();
            ResetPosition();
            currentHealth = maxHealth;
            UpdateUI();
        }
    }

    void ReportDPS()
    {
        float d = lastHitTime - combatStartTime;
        if (d < 0.1f) d = 1f;
        float dps = totalDamageDealt / d;
        if (dps > 0) SpawnPopup($"DPS: {dps:F1}", Color.white, 5f);
    }

    void ResetPosition()
    {
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.position = startPosition;
        transform.rotation = startRotation;
    }
}