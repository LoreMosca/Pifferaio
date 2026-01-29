using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Melody
{
    [Tooltip("Nome generato proceduralmente o assegnato.")]
    public string spellName;

    [Tooltip("Livello di potenza (1-4).")]
    public int tier;

    [Tooltip("La sequenza di note richiesta per castare.")]
    public List<NoteDefinition> sequence = new List<NoteDefinition>();

    // Ritorna vero se la sequenza passata è IDENTICA a questa melodia
    public bool IsMatch(List<NoteDefinition> inputNotes)
    {
        if (inputNotes.Count != sequence.Count) return false;

        for (int i = 0; i < sequence.Count; i++)
        {
            if (inputNotes[i] != sequence[i]) return false;
        }
        return true;
    }
}