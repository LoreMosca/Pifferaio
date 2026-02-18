using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GrimoireEntry : MonoBehaviour
{
    [Header("UI Elements")]
    public Image trebleClefIcon;
    public Image[] noteSlots;
    public TextMeshProUGUI infoText;
    public Image staffBackground;

    [Header("Feedback Attivo")]
    [Tooltip("L'immagine di sfondo dorata/luminosa (deve essere un oggetto figlio).")]
    public GameObject highlightVisual;

    [Header("Animazione (Respiro)")]
    public float pulseSpeed = 2.0f; // Molto più lento (era 5)
    public float pulseAmount = 0.03f; // Molto più sottile (era 0.05)

    // Stato interno
    private Melody myMelody;
    private bool isHighlighted = false;
    private Vector3 highlightBaseScale;

    void Awake()
    {
        // Salviamo la scala iniziale dell'highlight, non di tutto l'oggetto
        if (highlightVisual != null)
            highlightBaseScale = highlightVisual.transform.localScale;
    }

    public void Setup(Melody melody)
    {
        myMelody = melody;

        // 1. Configura Chiave (Rarità)
        if (trebleClefIcon != null) trebleClefIcon.color = GetTierColor(melody.tier);

        // 2. Configura Note
        for (int i = 0; i < noteSlots.Length; i++)
        {
            if (i < melody.sequence.Count)
            {
                noteSlots[i].sprite = melody.sequence[i].icon;
                noteSlots[i].enabled = true;
                noteSlots[i].color = Color.white;
            }
            else
            {
                noteSlots[i].enabled = false;
            }
        }

        // 3. GENERAZIONE TESTO
        if (infoText != null)
        {
            string description = GetSpellDescription(melody);
            string colorHex = ColorUtility.ToHtmlStringRGB(GetTierColor(melody.tier));

            // Usiamo il grassetto (<b>) e una dimensione fissa per chiarezza
            infoText.text = $"{description} <color=#{colorHex}><b>Lv.{melody.level}</b></color>";
        }

        SetHighlight(false);
    }

    void Update()
    {
        // 4. ANIMAZIONE (Solo sull'Highlight, il testo resta fermo!)
        if (isHighlighted && highlightVisual != null)
        {
            // Movimento Sinusoidale dolce (Respiro)
            // Usiamo Sin invece di PingPong per una curva più morbida
            float breath = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // Valore tra 0 e 1 morbido
            float scaleMod = breath * pulseAmount;

            highlightVisual.transform.localScale = highlightBaseScale * (1f + scaleMod);

            // Opzionale: Modifica anche l'Alpha per renderlo più etereo
            Image img = highlightVisual.GetComponent<Image>();
            if (img != null)
            {
                // Oscilla l'alpha tra 0.6 e 0.8 (regolabile)
                float alpha = Mathf.Lerp(0.6f, 0.85f, breath);
                img.color = new Color(img.color.r, img.color.g, img.color.b, alpha);
            }
        }
    }

    public void SetHighlight(bool isActive)
    {
        isHighlighted = isActive;

        if (highlightVisual != null)
        {
            highlightVisual.SetActive(isActive);
            if (!isActive) highlightVisual.transform.localScale = highlightBaseScale; // Reset
        }
    }

    public Melody GetMelody() => myMelody;

    // --- LOGICA TESTO ---
    string GetSpellDescription(Melody melody)
    {
        if (melody.sequence.Count < 2) return "Melodia Rotta";

        // Nota 1 = Effetto
        string effect = "";
        switch (melody.sequence[0].color)
        {
            case NoteColor.Green: effect = "Cura"; break;
            case NoteColor.Red: effect = "Danno"; break;
            case NoteColor.Blue: effect = "Tattica"; break;
            case NoteColor.Yellow: effect = "Scudo"; break;
        }

        // Nota 2 = Forma
        string form = "";
        switch (melody.sequence[1].color)
        {
            case NoteColor.Green: form = "Proiettile"; break;
            case NoteColor.Red: form = "Raggio"; break;
            case NoteColor.Blue: form = "Area"; break;
            case NoteColor.Yellow: form = "Buff"; break;
        }

        // --- NOMI SPECIALI (CORRETTO) ---

        // Giallo + Giallo = Buff Scudo (Era "Buff Fortuna")
        if (melody.sequence[0].color == NoteColor.Yellow && melody.sequence[1].color == NoteColor.Yellow)
            return "Buff Scudo";

        if (melody.sequence[0].color == NoteColor.Red && melody.sequence[1].color == NoteColor.Yellow)
            return "Buff Rage"; // Danno

        if (melody.sequence[0].color == NoteColor.Blue && melody.sequence[1].color == NoteColor.Yellow)
            return "Buff Haste"; // Velocità

        // Fallback standard (es. "Proiettile Cura", "Raggio Danno")
        return $"{form} {effect}";
    }

    Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return new Color(1f, 0.8f, 0f);
            case 4: return new Color(1f, 0.5f, 0f);
            default: return Color.gray;
        }
    }
}