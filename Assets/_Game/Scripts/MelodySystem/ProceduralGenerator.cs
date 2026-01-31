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

    void Awake()
    {
        // AUTO-FIX: Se non hai configurato i Tier nell'Inspector, metto i default del GDD
        if (tierRules == null || tierRules.Count == 0)
        {
            Debug.LogWarning("ProceduralGenerator: TierRules vuote! Carico valori di default.");
            tierRules = new List<TierRule>()
            {
                new TierRule { label="Comune", tierLevel=1, minCost=2, maxCost=8 },
                new TierRule { label="Raro", tierLevel=2, minCost=3, maxCost=12 },
                new TierRule { label="Epico", tierLevel=3, minCost=4, maxCost=14 }, // Adattato per test
                new TierRule { label="Leggendario", tierLevel=4, minCost=12, maxCost=16 }
            };
        }
    }

    public Melody GenerateLoot(int targetTier)
    {
        // Controllo di sicurezza sulle Note
        if (allNotes == null || allNotes.Count == 0)
        {
            Debug.LogError("ERRORE: Lista 'All Notes' vuota nel ProceduralGenerator! Assegna gli ScriptableObject.");
            return null;
        }

        Melody melody = new Melody();
        melody.tier = targetTier;

        // Trova la regola o usa default se non trovata
        TierRule rule = tierRules.Find(r => r.tierLevel == targetTier);
        if (rule.maxCost == 0)
        {
            Debug.LogWarning($"Regola Tier {targetTier} non trovata, uso Tier 1 come fallback.");
            rule = tierRules[0];
        }

        int currentCost = 0;

        // 1. Radice e Forma (sempre 2 note minime)
        AddRandomNote(melody, ref currentCost);
        AddRandomNote(melody, ref currentCost);

        // 2. Estensioni (riempie fino al range)
        int safety = 0;
        // Nota: Aggiunto check (currentCost < rule.minCost) per forzare l'aggiunta se siamo sotto il minimo
        while ((currentCost < rule.minCost || (currentCost < rule.maxCost && Random.value > 0.5f))
                && melody.sequence.Count < 4 && safety < 50)
        {
            NoteDefinition candidate = GetRandomNote();

            // Possiamo aggiungere la nota senza sforare il massimo?
            if (currentCost + candidate.complexityCost <= rule.maxCost)
            {
                melody.sequence.Add(candidate);
                currentCost += candidate.complexityCost;
            }
            else
            {
                // Se non entra, proviamo un'altra volta (magari esce una nota costo 1) oppure break se siamo già validi
                if (currentCost >= rule.minCost) break;
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