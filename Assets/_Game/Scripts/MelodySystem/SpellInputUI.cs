using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SpellInputUI : MonoBehaviour
{
    [Header("Riferimenti")]
    public SpellCasterSystem spellSystem;

    [Header("UI Elementi")]
    [Tooltip("Le 4 immagini UI dove appariranno le note.")]
    public Image[] noteSlots;

    [Tooltip("Il testo del nome della spell.")]
    public TextMeshProUGUI spellNameText;

    [Tooltip("Glow opzionale per spell pronta.")]
    public GameObject readyGlow;

    [Header("Visual Settings")]
    public Color readyTextColor = Color.yellow;
    public Color notReadyTextColor = Color.gray;

    void Start()
    {
        if (spellSystem != null)
        {
            spellSystem.OnInputChanged += UpdateUI;
            UpdateUI();
        }
    }

    void OnDestroy()
    {
        if (spellSystem != null) spellSystem.OnInputChanged -= UpdateUI;
    }

    void UpdateUI()
    {
        if (spellSystem == null) return;

        List<NoteDefinition> queue = spellSystem.GetCurrentQueue();
        Melody readySpell = spellSystem.GetReadySpell();

        // 1. AGGIORNA GLI SLOT (SOLO ICONE)
        if (noteSlots != null)
        {
            for (int i = 0; i < noteSlots.Length; i++)
            {
                if (noteSlots[i] == null) continue;

                if (i < queue.Count)
                {
                    // --- SLOT PIENO ---
                    NoteDefinition note = queue[i];

                    if (note != null && note.icon != null)
                    {
                        // Assegna lo sprite
                        noteSlots[i].sprite = note.icon;
                        // Mantiene le proporzioni (evita icone stirate)
                        noteSlots[i].preserveAspect = true;
                        // IMPORTANTE: Colore Bianco = Vedi lo sprite originale senza tinte
                        noteSlots[i].color = Color.white;
                    }
                    else
                    {
                        // Se manca lo sprite nel NoteDefinition, metti un colore di fallback per debug
                        noteSlots[i].sprite = null;
                        noteSlots[i].color = Color.magenta; // Magenta = Errore (manca sprite)
                    }

                    noteSlots[i].enabled = true;
                }
                else
                {
                    // --- SLOT VUOTO ---
                    noteSlots[i].sprite = null;
                    noteSlots[i].color = Color.clear; // Completamente trasparente
                    noteSlots[i].enabled = false;
                }
            }
        }

        // 2. AGGIORNA TESTO
        if (spellNameText != null)
        {
            if (readySpell != null)
            {
                spellNameText.text = readySpell.spellName;
                spellNameText.color = GetTierColor(readySpell.tier);
                if (readyGlow) readyGlow.SetActive(true);
            }
            else
            {
                spellNameText.text = "...";
                spellNameText.color = notReadyTextColor;
                if (readyGlow) readyGlow.SetActive(false);
            }
        }
    }

    Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 1: return Color.white;
            case 2: return Color.cyan;
            case 3: return Color.yellow;
            case 4: return new Color(1f, 0.6f, 0f); // Arancio
            default: return Color.white;
        }
    }
}