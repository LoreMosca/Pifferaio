using UnityEngine;
using UnityEngine.UI;

public class DummyStatusUI : MonoBehaviour
{
    [Header("--- RIFERIMENTI HIERARCHY ---")]
    [Tooltip("Trascina qui: HealthBar (l'immagine ROSSA filled)")]
    public Image healthFill;

    [Tooltip("Trascina qui: ShieldBar (l'immagine GIALLA filled)")]
    public Image shieldFill;

    [Tooltip("Trascina qui: SlowBar (l'immagine BLU filled)")]
    public Image slowFill;

    [Tooltip("Trascina qui: SlowBar_Background (l'oggetto padre della barra blu). Serve per nasconderlo se non c'è slow.")]
    public GameObject slowBarContainer;

    [Header("Settings")]
    public float smoothSpeed = 10f;

    // Valori target per l'interpolazione
    private float targetHealth = 1f;
    private float targetShield = 0f;
    private float targetSlow = 0f;

    public void UpdateHealth(float current, float max)
    {
        targetHealth = current / max;
    }

    public void UpdateShield(float current, float maxHealthRef)
    {
        // Lo scudo è visualizzato in proporzione alla vita massima
        targetShield = current / maxHealthRef;
    }

    public void UpdateSlow(float percent)
    {
        targetSlow = percent / 100f;
    }

    void Update()
    {
        // 1. GESTIONE VITA (Rossa)
        if (healthFill)
        {
            healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, targetHealth, Time.deltaTime * smoothSpeed);
        }

        // 2. GESTIONE SCUDO (Gialla)
        if (shieldFill)
        {
            // Lerp fluido
            float nextShield = Mathf.Lerp(shieldFill.fillAmount, targetShield, Time.deltaTime * smoothSpeed);
            shieldFill.fillAmount = nextShield;

            // Se lo scudo è quasi zero, disattiva l'immagine gialla per pulizia (ma non il background rosso)
            shieldFill.enabled = nextShield > 0.01f;
        }

        // 3. GESTIONE SLOW (Blu + Background)
        if (slowBarContainer && slowFill)
        {
            // Lerp fluido
            float nextSlow = Mathf.Lerp(slowFill.fillAmount, targetSlow, Time.deltaTime * smoothSpeed);
            slowFill.fillAmount = nextSlow;

            // Se non c'è slow (o è quasi zero), NASCONDIAMO L'INTERO BLOCCO (Background compreso)
            // Il Vertical Layout Group farà collassare lo spazio vuoto automaticamente.
            bool hasSlow = nextSlow > 0.01f;
            if (slowBarContainer.activeSelf != hasSlow)
            {
                slowBarContainer.SetActive(hasSlow);
            }
        }
    }
}