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
    [Range(0, 100)] public float currentSlowPercent = 0f;

    [Header("--- RIFERIMENTI VISUALI ---")]
    public GameObject floatingTextPrefab;
    public Transform popupSpawnPoint;
    public Renderer meshRenderer;
    public DummyStatusUI statusUI;

    [Tooltip("Oggetto figlio (es. Sfera semitrasparente) che appare quando c'è lo scudo.")]
    public GameObject shieldVisualObject; // <--- NUOVO

    [Header("--- DPS METER ---")]
    public float combatResetTime = 2.0f;

    // Colori
    private Color baseColor;
    private Color freezeColor = new Color(0, 1, 1, 1);
    private Coroutine flashRoutine;

    // DPS Interni
    private float totalDamageDealt = 0;
    private float totalHealingDone = 0;
    private float combatStartTime = 0;
    private float lastHitTime = 0;
    private bool inCombat = false;

    void Start()
    {
        // SETUP VITA INIZIALE
        if (type == DummyType.Prince)
        {
            // Il principe parte ferito per testare le cure
            currentHealth = maxHealth * 0.2f;
        }
        else
        {
            currentHealth = maxHealth;
        }

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
        UpdateShieldVisual(); // Controlla se mostrare la sfera scudo
    }

    // --- INTERFACCIA IDAMAGEABLE CON LOGICA DI TIPO ---

    public void TakeDamage(float amount)
    {
        // I NEMICI prendono danno, IL PRINCIPE NO
        if (type == DummyType.Prince)
        {
            SpawnPopup("0", Color.grey, 3f);
            return;
        }

        CheckCombatStart();
        float effectiveDamage = amount;

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

    public void Heal(float amount)
    {
        // IL PRINCIPE si cura, I NEMICI NO
        if (type == DummyType.Enemy)
        {
            SpawnPopup("0", Color.grey, 3f);
            return;
        }

        CheckCombatStart();
        float healAmount = amount;
        if (currentHealth + amount > maxHealth) healAmount = maxHealth - currentHealth;

        currentHealth += healAmount;
        totalHealingDone += healAmount;

        SpawnPopup($"+{healAmount:F0} HP", Color.green, 5f);
        Flash(Color.green);
        UpdateUI();
    }

    public void AddShield(float amount)
    {
        // SOLO IL PRINCIPE riceve scudi
        if (type == DummyType.Enemy)
        {
            SpawnPopup("0", Color.grey, 3f);
            return;
        }

        CheckCombatStart();
        currentShield += amount;
        SpawnPopup($"+{amount:F0} SHLD", Color.yellow, 4f);
        UpdateUI();
    }

    public void ApplySlow(float percentage, float duration)
    {
        // SOLO I NEMICI rallentano
        if (type == DummyType.Prince)
        {
            SpawnPopup("0", Color.grey, 3f);
            return;
        }

        currentSlowPercent = Mathf.Clamp(currentSlowPercent + percentage, 0, 100);

        if (currentSlowPercent >= 100)
            SpawnPopup("FREEZE!", Color.cyan, 5f);
        else
            SpawnPopup($"-{percentage}% SLOW", Color.cyan, 3f);

        UpdateUI();
    }

    // --- LOGICA VISIVA ---

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
        // Attiva la sfera scudo se abbiamo scudo
        if (shieldVisualObject)
        {
            bool shouldBeActive = currentShield > 1f;
            if (shieldVisualObject.activeSelf != shouldBeActive)
            {
                shieldVisualObject.SetActive(shouldBeActive);
            }
        }
    }

    void UpdateVisualColor()
    {
        if (!meshRenderer || flashRoutine != null) return;

        if (currentSlowPercent > 0)
        {
            float t = currentSlowPercent / 100f;
            meshRenderer.material.color = Color.Lerp(baseColor, freezeColor, t);
        }
        else
        {
            meshRenderer.material.color = baseColor;
        }
    }

    void HandleStatusRecovery()
    {
        if (currentSlowPercent > 0)
        {
            currentSlowPercent -= 20f * Time.deltaTime;
            if (currentSlowPercent < 0) currentSlowPercent = 0;
            UpdateUI();
        }
    }

    // --- DPS & UTILS (Invariati) ---
    void CheckCombatStart() { lastHitTime = Time.time; if (!inCombat) { inCombat = true; combatStartTime = Time.time; totalDamageDealt = 0; totalHealingDone = 0; } }
    void HandleDPSLogic() { if (inCombat && Time.time > lastHitTime + combatResetTime) { inCombat = false; float d = lastHitTime - combatStartTime; if (d < 0.1f) d = 1f; float dps = totalDamageDealt / d; float hps = totalHealingDone / d; string r = ""; if (dps > 0) r += $"DPS: {dps:F1}\n"; if (hps > 0) r += $"HPS: {hps:F1}"; if (r != "") StartCoroutine(ShowReport(r)); } }
    IEnumerator ShowReport(string t) { yield return new WaitForSeconds(0.2f); SpawnPopup("--- REPORT ---", Color.white, 4f); yield return new WaitForSeconds(0.3f); SpawnPopup(t, Color.white, 6f); }
    void SpawnPopup(string t, Color c, float s) { if (!floatingTextPrefab) return; Vector3 p = popupSpawnPoint ? popupSpawnPoint.position : transform.position + Vector3.up * 2f; GameObject o = Instantiate(floatingTextPrefab, p, Quaternion.identity); FloatingText ft = o.GetComponent<FloatingText>(); if (ft) ft.Setup(t, c, s); }
    void Flash(Color c) { if (!meshRenderer) return; if (flashRoutine != null) StopCoroutine(flashRoutine); flashRoutine = StartCoroutine(FlashRoutine(c)); }
    IEnumerator FlashRoutine(Color c) { meshRenderer.material.color = c; yield return new WaitForSeconds(0.1f); flashRoutine = null; UpdateVisualColor(); }
    void Die() { SpawnPopup("DISTRUTTO", Color.grey, 7f); gameObject.SetActive(false); Invoke(nameof(Respawn), 2f); }
    void Respawn() { gameObject.SetActive(true); if (type == DummyType.Prince) currentHealth = maxHealth * 0.2f; else currentHealth = maxHealth; currentShield = 0; currentSlowPercent = 0; meshRenderer.material.color = baseColor; UpdateUI(); }
}