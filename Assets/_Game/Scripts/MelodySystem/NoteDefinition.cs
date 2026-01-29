using UnityEngine;

public enum NoteType
{
    Rapido_Verde,   // Input 1
    Area_Blu,       // Input 2
    Pesante_Rosso,  // Input 3
    Parry_Giallo    // Input 4
}

[CreateAssetMenu(fileName = "New_Note", menuName = "Pifferaio/Note Definition", order = 1)]
public class NoteDefinition : ScriptableObject
{
    [Header("Identità Nota")]
    [Tooltip("Il tipo logico della nota (collegato all'input).")]
    public NoteType type;

    [Tooltip("Nome visualizzato nell'UI.")]
    public string noteName;

    [Header("Bilanciamento")]
    [Tooltip("Quanto 'pesa' questa nota nel calcolo del Tier?")]
    [Range(1, 5)]
    public int complexityCost = 1;

    [Header("Visuals")]
    [Tooltip("Icona da mostrare nell'UI.")]
    public Sprite icon;
    [Tooltip("Colore per il debug a schermo.")]
    public Color debugColor = Color.white;
}