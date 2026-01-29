using System.Collections.Generic;
using UnityEngine;

public class ProceduralGenerator : MonoBehaviour
{
    [Header("Database Note")]
    [Tooltip("Trascina qui i 4 ScriptableObject delle note (Verde, Blu, Rosso, Giallo).")]
    public List<NoteDefinition> allNotes;

    [Header("Regole Tier")]
    [Tooltip("Configura qui i range di costo per ogni Tier.")]
    public List<TierRule> tierRules;

    [System.Serializable]
    public struct TierRule
    {
        public string label;
        public int tierLevel;
        public int minCost;
        public int maxCost;
    }

    /// <summary>
    /// Crea una melodia procedurale.
    /// </summary>
    public Melody GenerateLoot(int targetTier)
    {
        Melody melody = new Melody();
        melody.tier = targetTier;

        // Trova la regola per questo tier
        TierRule rule = tierRules.Find(r => r.tierLevel == targetTier);
        if (rule.maxCost == 0) { Debug.LogError($"Nessuna regola trovata per Tier {targetTier}"); return null; }

        int currentCost = 0;

        // 1. Radice e Forma (sempre 2 note minime)
        AddRandomNote(melody, ref currentCost);
        AddRandomNote(melody, ref currentCost);

        // 2. Estensioni (riempie fino al range)
        int safety = 0;
        while (currentCost < rule.maxCost && melody.sequence.Count < 4 && safety < 50)
        {
            NoteDefinition candidate = GetRandomNote();
            if (currentCost + candidate.complexityCost <= rule.maxCost)
            {
                melody.sequence.Add(candidate);
                currentCost += candidate.complexityCost;
            }
            else if (currentCost >= rule.minCost)
            {
                break; // Siamo nel range giusto, usciamo
            }
            safety++;
        }

        melody.spellName = $"Spell T{targetTier}-{Random.Range(100, 999)}";
        return melody;
    }

    private void AddRandomNote(Melody m, ref int cost)
    {
        var note = GetRandomNote();
        m.sequence.Add(note);
        cost += note.complexityCost;
    }

    private NoteDefinition GetRandomNote() => allNotes[Random.Range(0, allNotes.Count)];
}