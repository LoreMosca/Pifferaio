using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SpellCasterSystem : MonoBehaviour
{
    [Header("--- DIPENDENZE ---")]
    [Tooltip("Il componente che costruisce i dati della magia (Danno, Area, ecc).")]
    public SpellBuilder builder;
    [Tooltip("Il componente che genera magie casuali.")]
    public ProceduralGenerator generator;
    [Tooltip("Il componente che gestisce gli effetti visivi (VFX).")]
    public SpellVisualizer visualizer;

    [Header("--- CONFIGURAZIONE NOTE ---")]
    [Tooltip("Assegna qui i 4 ScriptableObject delle note (Verde, Blu, Rosso, Giallo).")]
    public NoteDefinition[] keyMappings;

    [Header("--- DEBUG LOOT TABLES ---")]
    [Tooltip("Tabella di loot per casse Comuni (Tier 1).")]
    public LootTable debugCommonTable;
    [Tooltip("Tabella di loot per nemici Elite (Tier 2).")]
    public LootTable debugEliteTable;
    [Tooltip("Tabella di loot per Boss (Tier 3).")]
    public LootTable debugBossTable;

    [Header("--- STATO RUNTIME (Solo Lettura) ---")]
    [Tooltip("Le magie attualmente imparate dal giocatore.")]
    [SerializeField] private List<Melody> inventory = new List<Melody>();

    [Tooltip("Le note premute finora in attesa di completare una sequenza.")]
    [SerializeField] private List<NoteDefinition> currentInputQueue = new List<NoteDefinition>();

    // Variabili interne
    private const int QUEUE_SIZE = 4;
    private Melody readySpell = null;

    void Start()
    {
        // Auto-collegamento se manca
        if (generator == null) generator = GetComponent<ProceduralGenerator>();
    }

    // --- LOGICA INPUT ---

    /// <summary>
    /// Chiamato dal PlayerController quando premi 1, 2, 3 o 4.
    /// </summary>
    public void PushNote(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= keyMappings.Length) return;

        NoteDefinition note = keyMappings[noteIndex];
        currentInputQueue.Add(note);

        // Mantieni la coda corta (ultime 4 note)
        if (currentInputQueue.Count > QUEUE_SIZE)
            currentInputQueue.RemoveAt(0);

        CheckForCombinations();
    }

    /// <summary>
    /// Controlla se le ultime note corrispondono a una magia nell'inventario.
    /// </summary>
    void CheckForCombinations()
    {
        readySpell = null; // Reset

        foreach (var spell in inventory)
        {
            // Se abbiamo abbastanza note per questa spell...
            if (currentInputQueue.Count >= spell.sequence.Count)
            {
                // Prendi le ultime N note
                var lastNotes = currentInputQueue.GetRange(currentInputQueue.Count - spell.sequence.Count, spell.sequence.Count);

                // Confronta i colori
                bool match = true;
                for (int i = 0; i < spell.sequence.Count; i++)
                {
                    if (lastNotes[i].color != spell.sequence[i].color)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    readySpell = spell;
                    return; // Trovata!
                }
            }
        }
    }

    // --- API PER IL PLAYER CONTROLLER ---

    public bool HasSpellReady()
    {
        return readySpell != null;
    }

    public void FireCurrentSpell(Transform originPoint)
    {
        if (readySpell == null) return;

        // 1. Costruisci il payload (Dati di gioco)
        SpellPayload payload = builder.BuildSpell(readySpell);

        // 2. Visualizza (Effetti grafici)
        if (visualizer != null)
        {
            visualizer.VisualizeSpell(payload, originPoint);
        }

        Debug.Log($"<color=cyan>CASTING: {payload.constructedName} (Tier {readySpell.tier})</color>");

        // 3. Resetta dopo il lancio
        currentInputQueue.Clear();
        readySpell = null;
    }

    // --- METODI DEBUG (LOOT) ---

    public void LootRandom(int tier)
    {
        LootTable table = debugCommonTable;
        if (tier == 2) table = debugEliteTable;
        if (tier == 3) table = debugBossTable;

        if (table != null)
        {
            Melody newSpell = generator.GenerateLoot(table);
            if (newSpell != null)
            {
                inventory.Add(newSpell);
                // Debug.Log($"LOOTED: {newSpell.spellName} (Tier {tier})");
            }
        }
        else
        {
            Debug.LogWarning("Nessuna LootTable assegnata per questo Tier!");
        }
    }

    // --- INTERFACCIA DI DEBUG (ON GUI) ---
    void OnGUI()
    {
        // Stile Testo
        GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        st.normal.textColor = Color.white;

        // --- BOX SINISTRO: STATO SISTEMA ---
        GUI.Box(new Rect(10, 10, 300, 200), "SYSTEM STATUS");

        // 1. Coda Note
        string q = "";
        // Copia per non modificare l'originale durante il foreach
        var visualQueue = new List<NoteDefinition>(currentInputQueue);
        foreach (var n in visualQueue) q += $"[{n.noteName}] ";
        GUI.Label(new Rect(20, 40, 280, 30), "INPUT: " + q, st);

        // 2. Spell Pronta
        if (readySpell != null)
        {
            st.normal.textColor = Color.green;
            GUI.Label(new Rect(20, 70, 280, 30), $"READY: {readySpell.spellName}!", st);

            SpellPayload preview = builder.BuildSpell(readySpell);
            st.fontSize = 12; st.normal.textColor = Color.cyan;
            GUI.Label(new Rect(20, 95, 280, 40), $"{preview.effect} | {preview.delivery}", st);
        }
        else
        {
            st.fontSize = 12; st.normal.textColor = Color.gray;
            GUI.Label(new Rect(20, 70, 280, 30), "(Nessuna combinazione...)", st);
        }

        // 3. Pulsanti Loot Rapido
        if (GUI.Button(new Rect(20, 140, 80, 25), "+ Common")) LootRandom(1);
        if (GUI.Button(new Rect(110, 140, 80, 25), "+ Elite")) LootRandom(2);
        if (GUI.Button(new Rect(200, 140, 80, 25), "+ Boss")) LootRandom(3);


        // --- BOX DESTRO: INVENTARIO SPELL ---
        GUI.Box(new Rect(Screen.width - 260, 10, 250, 400), "GRIMORIO (Inventory)");

        float yPos = 40;
        st.fontSize = 13;

        if (inventory.Count == 0)
        {
            st.normal.textColor = Color.gray;
            GUI.Label(new Rect(Screen.width - 240, yPos, 230, 20), "Inventario vuoto.", st);
        }

        foreach (var spell in inventory)
        {
            // Colore in base al Tier
            if (spell.tier == 1) st.normal.textColor = Color.white;
            if (spell.tier == 2) st.normal.textColor = new Color(0.4f, 0.6f, 1f); // Azzurro
            if (spell.tier >= 3) st.normal.textColor = new Color(1f, 0.6f, 0.2f); // Arancio

            // Componi la stringa della sequenza (es: V-R-V)
            string seq = "";
            foreach (var n in spell.sequence) seq += n.noteName.Substring(0, 1) + " ";

            GUI.Label(new Rect(Screen.width - 240, yPos, 230, 20),
                $"{spell.spellName} (Lv.{spell.level})", st);

            st.normal.textColor = Color.gray;
            GUI.Label(new Rect(Screen.width - 240, yPos + 15, 230, 20),
                $"[{seq}] T:{spell.tier}", st);

            yPos += 40; // Spazio per la prossima
        }
    }
}