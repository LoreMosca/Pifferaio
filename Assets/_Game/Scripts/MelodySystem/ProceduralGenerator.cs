using System.Collections.Generic;
using UnityEngine;

public class ProceduralGenerator : MonoBehaviour
{
    [Header("Database Note")]
    [Tooltip("Trascina qui i 4 ScriptableObject delle note.")]
    public List<NoteDefinition> allNotes;

    [Header("Regole Tier")]
    [Tooltip("Configura numero note e costi per tier.")]
    public List<TierRule> tierRules;

    [System.Serializable]
    public struct TierRule
    {
        public string label;
        public int tierLevel;
        public int minCost;
        public int maxCost;
        [Tooltip("Numero esatto di note per questo Tier (es. T1=2, T2=3).")]
        public int fixedNoteCount;
    }

    void Awake()
    {
        // Auto-fix regole default se mancano
        if (tierRules == null || tierRules.Count == 0)
        {
            tierRules = new List<TierRule>()
            {
                new TierRule { label="Comune", tierLevel=1, minCost=2, maxCost=8, fixedNoteCount=2 },
                new TierRule { label="Raro", tierLevel=2, minCost=3, maxCost=12, fixedNoteCount=3 },
                new TierRule { label="Epico", tierLevel=3, minCost=4, maxCost=14, fixedNoteCount=4 },
                new TierRule { label="Leggendario", tierLevel=4, minCost=12, maxCost=16, fixedNoteCount=4 }
            };
        }
    }

    /// <summary>
    /// NUOVO METODO: Genera loot basandosi su una tabella di probabilità.
    /// </summary>
    public Melody GenerateLoot(LootTable table)
    {
        if (table == null)
        {
            Debug.LogError("LootTable mancante!");
            return null;
        }

        int selectedTier = table.PickRandomTier();
        return GenerateLoot(selectedTier);
    }

    // Metodo base (Tier specifico)
    public Melody GenerateLoot(int targetTier)
    {
        if (allNotes == null || allNotes.Count == 0) return null;

        Melody melody = new Melody();
        melody.tier = targetTier;

        TierRule rule = tierRules.Find(r => r.tierLevel == targetTier);
        if (rule.fixedNoteCount == 0) rule = tierRules[0];

        int currentCost = 0;
        int attempts = 0;

        while (attempts < 100)
        {
            melody.sequence.Clear();
            currentCost = 0;

            for (int i = 0; i < rule.fixedNoteCount; i++)
            {
                var note = GetRandomNote();
                melody.sequence.Add(note);
                currentCost += note.complexityCost;
            }

            if (currentCost >= rule.minCost && currentCost <= rule.maxCost)
            {
                melody.spellName = $"Spell T{targetTier}-{Random.Range(100, 999)}";
                return melody;
            }
            attempts++;
        }
        return melody;
    }

    private NoteDefinition GetRandomNote() => allNotes[Random.Range(0, allNotes.Count)];
}