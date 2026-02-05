using UnityEngine;
using System.Collections;

public class DummyController : MonoBehaviour, IDamageable
{
    public enum DummyType { Enemy, Prince }

    [Header("--- CONFIGURAZIONE ---")]
    public DummyType type = DummyType.Enemy;

    [Header("--- STATS ---")]
    public float maxHealth = 1000f;
    public float currentHealth;
    public float currentShield = 0f;

    [Header("--- GHIACCIO & SLOW (Inspector Curato) ---")]
    [Tooltip("Percentuale di rallentamento attuale.")]
    [Range(0, 100)] public float currentSlowPercent = 0f;

    [Tooltip("Quanto velocemente cala lo slow al secondo (es. 20 = perde 20% al sec).")]
    public float slowDecayRate = 20f;

    [Tooltip("Durata dello stato di congelamento (Freeze) una volta raggiunto il 100%.")]
    public float freezeDuration = 3.0f;

    [Tooltip("Stato attuale di congelamento.")]
    public bool isFrozen = false;

    [Header("--- RIFERIMENTI VISUALI ---")]
    public GameObject floatingTextPrefab;
    public Transform popupSpawnPoint;
    public Renderer meshRenderer;
    public DummyStatusUI statusUI;
    public GameObject shieldVisualObject;

    [Header("--- DPS METER ---")]
    public float combatResetTime = 2.0f;

    // Variabili interne
    private Color baseColor;
    private Color freezeColor = new Color(0, 1, 1, 1);
    private Coroutine flashRoutine;
    private float freezeTimer = 0f;
    private Rigidbody rb;

    // Variabili per il Reset Posizione
    private Vector3 startPosition;
    private Quaternion startRotation;

    // DPS logic
    private float totalDamageDealt = 0;
    private float totalHealingDone = 0;
    private float combatStartTime = 0;
    private float lastHitTime = 0;
    private bool inCombat = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Salva posizione originale per il reset
        startPosition = transform.position;
        startRotation = transform.rotation;

        if (type == DummyType.Prince) currentHealth = maxHealth * 0.2f;
        else currentHealth = maxHealth;

        if (meshRenderer) baseColor = meshRenderer.material.color;

        if (type == DummyType.Enemy) gameObject.tag = "Nemico";
        else gameObject.tag = "Principe";

        if (shieldVisualObject) shieldVisualObject.SetActive(false);

        UpdateUI();
    }

    void Update()
    {
        HandleDPSLogic();
        HandleStatusRecovery();
        UpdateVisualColor();
        UpdateShieldVisual();
    }

    // --- IDAMAGEABLE & KNOCKBACK ---

    public void TakeDamage(float amount)
    {
        if (type == DummyType.Prince) { SpawnPopup("0", Color.grey, 3f); return; }

        CheckCombatStart();
        float effectiveDamage = amount;

        // Bonus danno se congelato?
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
            totalDamageDealt += effectiveDamage;
            SpawnPopup($"-{effectiveDamage:F0}", Color.red, 5f);
            Flash(Color.red);
        }

        UpdateUI();
        if (currentHealth <= 0) Die();
    }

    public void ApplyKnockback(Vector3 force)
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    public void ApplySlow(float percentage, float duration)
    {
        if (type == DummyType.Prince) { SpawnPopup("0", Color.grey, 3f); return; }
        if (isFrozen) return;

        currentSlowPercent += percentage;

        if (currentSlowPercent >= 100f)
        {
            currentSlowPercent = 100f;
            StartFreeze();
        }
        else
        {
            SpawnPopup($"-{percentage}% SLOW", Color.cyan, 3f);
        }
        UpdateUI();
    }

    // --- STATUS LOGIC ---

    void StartFreeze()
    {
        isFrozen = true;
        freezeTimer = freezeDuration;
        SpawnPopup("FROZEN! ❄", Color.cyan, 5f);

        // Blocca movimento fisico quando congelato
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

    // --- DPS LOGIC & RESET ---

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
        // Se è passato abbastanza tempo dall'ultimo colpo, chiudi il combattimento
        if (inCombat && Time.time > lastHitTime + combatResetTime)
        {
            inCombat = false;

            // Calcolo DPS
            float duration = lastHitTime - combatStartTime;
            if (duration < 0.1f) duration = 1f;

            float dps = totalDamageDealt / duration;
            float hps = totalHealingDone / duration;

            string report = "";
            if (dps > 0) report += $"DPS: {dps:F1}\n";
            if (hps > 0) report += $"HPS: {hps:F1}";

            if (report != "")
            {
                StartCoroutine(ShowReport(report));
            }

            // RESET POSIZIONE (Funzionalità richiesta)
            ResetPosition();
        }
    }

    void ResetPosition()
    {
        // Ferma qualsiasi movimento residuo
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Teletrasporta al punto di origine
        transform.position = startPosition;
        transform.rotation = startRotation;
    }

    // --- UTILS ---
    public void Heal(float amount)
    {
        if (type == DummyType.Enemy) { SpawnPopup("0", Color.grey, 3f); return; }
        CheckCombatStart();
        float h = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += h; totalHealingDone += h;
        SpawnPopup($"+{h:F0} HP", Color.green, 5f); Flash(Color.green); UpdateUI();
    }
    public void AddShield(float amount)
    {
        if (type == DummyType.Enemy) { SpawnPopup("0", Color.grey, 3f); return; }
        CheckCombatStart(); currentShield += amount; SpawnPopup($"+{amount:F0} SHLD", Color.yellow, 4f); UpdateUI();
    }

    void UpdateUI() { if (statusUI) { statusUI.UpdateHealth(currentHealth, maxHealth); statusUI.UpdateShield(currentShield, maxHealth); statusUI.UpdateSlow(currentSlowPercent); } }
    void UpdateShieldVisual() { if (shieldVisualObject) shieldVisualObject.SetActive(currentShield > 1f); }
    void UpdateVisualColor() { if (!meshRenderer || flashRoutine != null) return; if (isFrozen) meshRenderer.material.color = freezeColor; else if (currentSlowPercent > 0) meshRenderer.material.color = Color.Lerp(baseColor, freezeColor, currentSlowPercent / 100f); else meshRenderer.material.color = baseColor; }

    IEnumerator ShowReport(string t) { yield return new WaitForSeconds(0.2f); SpawnPopup("--- REPORT ---", Color.white, 4f); yield return new WaitForSeconds(0.3f); SpawnPopup(t, Color.white, 6f); }
    void SpawnPopup(string t, Color c, float s) { if (!floatingTextPrefab) return; Vector3 p = popupSpawnPoint ? popupSpawnPoint.position : transform.position + Vector3.up * 2f; GameObject o = Instantiate(floatingTextPrefab, p, Quaternion.identity); FloatingText ft = o.GetComponent<FloatingText>(); if (ft) ft.Setup(t, c, s); }
    void Flash(Color c) { if (!meshRenderer) return; if (flashRoutine != null) StopCoroutine(flashRoutine); flashRoutine = StartCoroutine(FlashRoutine(c)); }
    IEnumerator FlashRoutine(Color c) { meshRenderer.material.color = c; yield return new WaitForSeconds(0.1f); flashRoutine = null; UpdateVisualColor(); }
    void Die() { SpawnPopup("DISTRUTTO", Color.grey, 7f); gameObject.SetActive(false); Invoke(nameof(Respawn), 2f); }
    void Respawn()
    {
        gameObject.SetActive(true);
        ResetPosition(); // Reset anche al respawn
        if (type == DummyType.Prince) currentHealth = maxHealth * 0.2f; else currentHealth = maxHealth;
        currentShield = 0; currentSlowPercent = 0; isFrozen = false; meshRenderer.material.color = baseColor; UpdateUI();
    }
}