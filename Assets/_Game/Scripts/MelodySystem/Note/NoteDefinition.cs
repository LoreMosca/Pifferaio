using UnityEngine;

// Enum semplificato come richiesto
public enum NoteColor
{
    Green,  // Rapido / Cura
    Blue,   // Area / Controllo
    Red,    // Pesante / Danno
    Yellow  // Parry / Difesa
}

[CreateAssetMenu(fileName = "Note_Color", menuName = "Pifferaio/Note Definition", order = 1)]
public class NoteDefinition : ScriptableObject
{
    [Header("Identità")]
    [Tooltip("Il colore è l'unica cosa che conta per la logica.")]
    public NoteColor color;

    [Tooltip("Nome visualizzato nel Debug (es. 'Verde').")]
    public string noteName;

    [Header("Configurazione UI")]
    public Sprite icon;
    public Color debugColor = Color.white;

    [Header("Tier Calculation")]
    [Tooltip("Costo di Esecuzione (CD) per il calcolo del Tier.")]
    [Range(1, 4)]
    public int complexityCost = 1;
}