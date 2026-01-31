using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Melody
{
    [Header("Info Generata")]
    public string spellName; // Es: "Fireball"
    public int tier;         // 1, 2, 3, 4
    public int level = 1;    // <--- NUOVO: Livello della spell

    [Header("Sequenza")]
    public List<NoteDefinition> sequence = new List<NoteDefinition>();

    // Helper per calcolare il costo totale
    public int GetTotalCost()
    {
        int total = 0;
        foreach (var n in sequence) total += n.complexityCost;
        return total;
    }

    // <--- NUOVO: Controlla se la sequenza è identica (per evitare duplicati)
    public bool IsSameSequence(Melody other)
    {
        if (other == null || sequence.Count != other.sequence.Count) return false;
        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i].color != other.sequence[i].color) return false;
        }
        return true;
    }
}