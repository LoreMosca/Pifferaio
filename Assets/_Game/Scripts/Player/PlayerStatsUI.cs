using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class PlayerStatusUI : MonoBehaviour
{
    [Header("Riferimenti")]
    public PlayerStats stats;
    public Image healthBar;
    public Image staminaBar;

    [Header("Settings Visivi")]
    public float fadeSpeed = 5f;
    [Tooltip("Velocità del lampeggiamento quando sei esausto.")]
    public float blinkSpeed = 10f;

    // Colori Standard
    public Color staminaNormalColor = Color.green;
    public Color staminaExhaustedColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Grigio Scuro
    public Color blinkColor = Color.red; // Colore alternativo per il lampeggio (es. Rosso allarme)

    private CanvasGroup canvasGroup;
    private float targetAlpha = 0f;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (stats != null) stats.OnStatsChanged += UpdateUI;

        if (staminaBar) staminaBar.color = staminaNormalColor;
        UpdateUI();
    }

    void OnDestroy()
    {
        if (stats != null) stats.OnStatsChanged -= UpdateUI;
    }

    void Update()
    {
        if (stats == null) return;

        // 1. LOGICA COMPARSA (Fade In/Out)
        // Mostra se ferito o se manca stamina
        bool hurt = stats.currentHealth < stats.maxHealth - 1f;
        bool tired = stats.currentStamina < stats.maxStamina - 1f;

        if (hurt || tired)
        {
            targetAlpha = 1f;
        }
        else
        {
            targetAlpha = 0f;
        }

        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        // 2. LOGICA COLORE & LAMPEGGIO STAMINA
        if (staminaBar)
        {
            if (stats.isExhausted)
            {
                // EFFETTO LAMPEGGIO (PingPong)
                // Oscilla tra Colore Esausto (Grigio) e Colore Blink (Rosso/Bianco)
                float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);
                staminaBar.color = Color.Lerp(staminaExhaustedColor, blinkColor, blink);
            }
            else
            {
                // Ritorno fluido al verde
                staminaBar.color = Color.Lerp(staminaBar.color, staminaNormalColor, Time.deltaTime * 10f);
            }
        }
    }

    void UpdateUI()
    {
        if (stats == null) return;

        if (healthBar) healthBar.fillAmount = stats.currentHealth / stats.maxHealth;
        if (staminaBar) staminaBar.fillAmount = stats.currentStamina / stats.maxStamina;
    }
}