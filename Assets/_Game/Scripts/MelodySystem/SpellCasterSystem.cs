using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SpellCasterSystem : MonoBehaviour
{
    [Header("--- DIPENDENZE ---")]
    [Tooltip("Builder che calcola le statistiche finali della spell.")]
    public SpellBuilder builder;
    [Tooltip("Generatore procedurale per creare nuove melodie.")]
    public ProceduralGenerator generator;
    [Tooltip("Gestisce la visualizzazione grafica degli attacchi.")]
    public SpellVisualizer visualizer;

    [Header("--- CONFIGURAZIONE INPUT ---")]
    [Tooltip("Mappatura tasti -> Note.")]
    public NoteDefinition[] keyMappings;

    [Header("--- DEBUG LOOT TABLES (3 Fonti) ---")]
    [Tooltip("Tabella per casse comuni (Tier bassi frequenti).")]
    public LootTable debugCommonTable;
    [Tooltip("Tabella per nemici elite.")]
    public LootTable debugEliteTable;
    [Tooltip("Tabella per Boss (Tier alti frequenti).")]
    public LootTable debugBossTable;

    [Header("--- STATO RUNTIME ---")]
    [SerializeField] private List<Melody> inventory = new List<Melody>();
    [SerializeField] private List<NoteDefinition> currentInputQueue = new List<NoteDefinition>();

    // Variabili interne per la logica di combo
    private List<int> matchedIndices = new List<int>();
    private const int QUEUE_SIZE = 4;
    private Melody readySpell = null;

    private struct Candidate { public Melody spell; public List<int> indices; public int len; }

    void Start()
    {
        if (generator == null) generator = GetComponent<ProceduralGenerator>();
    }

    // --- GESTIONE INPUT ---

    public void PushNote(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= keyMappings.Length) return;
        currentInputQueue.Add(keyMappings[noteIndex]);

        // Mantiene la coda a dimensione fissa (4)
        while (currentInputQueue.Count > QUEUE_SIZE)
            currentInputQueue.RemoveAt(0);

        CheckForCombinations();
    }

    void CheckForCombinations()
    {
        readySpell = null;
        matchedIndices.Clear();
        List<Candidate> candidates = new List<Candidate>();

        // Cerca pattern nell'inventario
        foreach (var spell in inventory)
        {
            int seqLen = spell.sequence.Count;
            int queueLen = currentInputQueue.Count;
            if (queueLen < seqLen) continue;

            for (int startIdx = 0; startIdx <= queueLen - seqLen; startIdx++)
            {
                bool match = true;
                for (int k = 0; k < seqLen; k++)
                {
                    if (currentInputQueue[startIdx + k].color != spell.sequence[k].color)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    Candidate c = new Candidate { spell = spell, len = seqLen, indices = new List<int>() };
                    for (int k = 0; k < seqLen; k++) c.indices.Add(startIdx + k);
                    candidates.Add(c);
                }
            }
        }

        // Priorità alla combo più lunga
        if (candidates.Count > 0)
        {
            candidates.Sort((a, b) => b.len.CompareTo(a.len));
            readySpell = candidates[0].spell;
            matchedIndices = candidates[0].indices;
        }
    }

    // --- METODI PUBBLICI (API per altri script) ---

    public bool HasSpellReady() => readySpell != null;

    public List<NoteDefinition> GetCurrentQueue() => currentInputQueue;

    // REINTEGRATO: Serve a FloatingRuneSystem per illuminare le rune
    public List<int> GetMatchedIndices() => matchedIndices;

    public void FireCurrentSpell(Transform defaultOrigin)
    {
        if (readySpell == null) return;

        // Costruisci il payload (include calcolo potenza basato su Livello e Tier)
        SpellPayload payload = builder.BuildSpell(readySpell);

        if (visualizer != null) visualizer.VisualizeSpell(payload, defaultOrigin);

        Debug.Log($"<color=cyan>CAST: {payload.constructedName}</color>");

        // Rimuovi SOLO le note usate dalla combo
        matchedIndices.Sort((a, b) => b.CompareTo(a)); // Ordine inverso per rimozione sicura
        foreach (int index in matchedIndices)
        {
            if (index < currentInputQueue.Count) currentInputQueue.RemoveAt(index);
        }

        matchedIndices.Clear();
        readySpell = null;

        // Ricontrolla subito se le note rimaste formano un'altra combo
        CheckForCombinations();
    }

    // --- GESTIONE LOOT & LIVELLAMENTO ---

    /// <summary>
    /// Chiamato dai pulsanti di debug o dai nemici.
    /// </summary>
    public void LootFromTable(string tableType)
    {
        if (generator == null) return;

        LootTable selectedTable = null;
        if (tableType == "Common") selectedTable = debugCommonTable;
        else if (tableType == "Elite") selectedTable = debugEliteTable;
        else if (tableType == "Boss") selectedTable = debugBossTable;

        if (selectedTable != null)
        {
            Melody newMelody = generator.GenerateLoot(selectedTable);
            if (newMelody != null)
            {
                AddOrLevelUp(newMelody);
            }
        }
    }

    /// <summary>
    /// Gestisce la logica dei duplicati: se esiste, sale di livello.
    /// </summary>
    private void AddOrLevelUp(Melody incomingMelody)
    {
        // Cerca se esiste già una melodia con la stessa sequenza esatta
        Melody existing = inventory.Find(m => m.IsSameSequence(incomingMelody));

        if (existing != null)
        {
            // ESISTE: Level Up!
            existing.level++;
            Debug.Log($"<color=yellow>LEVEL UP!</color> {existing.spellName} è ora al Livello {existing.level}");
        }
        else
        {
            // NUOVA: Aggiungi all'inventario
            incomingMelody.level = 1;
            inventory.Add(incomingMelody);
            Debug.Log($"<color=green>NEW SPELL!</color> {incomingMelody.spellName} aggiunto al grimorio.");
        }
    }

    // --- GUI DEBUG ---

    void OnGUI()
    {
        GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        st.normal.textColor = Color.white;

        // BOX INPUT
        GUI.Box(new Rect(10, 10, 300, 200), "SYSTEM STATUS");
        string q = "";
        foreach (var n in currentInputQueue) q += $"[{n.noteName}] ";
        GUI.Label(new Rect(20, 40, 280, 30), "INPUT: " + q, st);

        if (readySpell != null)
        {
            st.normal.textColor = Color.green;
            GUI.Label(new Rect(20, 70, 280, 30), $"READY: {readySpell.spellName} (Lv.{readySpell.level})", st);
        }
        else
        {
            st.normal.textColor = Color.gray;
            GUI.Label(new Rect(20, 70, 280, 30), "Waiting...", st);
        }

        // PULSANTI LOOT (Usa le tabelle assegnate in inspector)
        if (GUI.Button(new Rect(20, 140, 90, 30), "Loot Common")) LootFromTable("Common");
        if (GUI.Button(new Rect(115, 140, 90, 30), "Loot Elite")) LootFromTable("Elite");
        if (GUI.Button(new Rect(210, 140, 90, 30), "Loot Boss")) LootFromTable("Boss");

        // GRIMORIO
        GUI.Box(new Rect(Screen.width - 260, 10, 250, 600), "GRIMORIO");
        float y = 40;

        foreach (var s in inventory)
        {
            // Colore basato sul Tier (Rarità)
            if (s.tier == 1) st.normal.textColor = Color.white;       // Comune
            else if (s.tier == 2) st.normal.textColor = Color.cyan;   // Raro
            else if (s.tier == 3) st.normal.textColor = Color.yellow; // Epico
            else st.normal.textColor = new Color(1f, 0.5f, 0f);       // Leggendario (Arancio/Oro)

            // Riga 1: Nome, Tier e Livello
            GUI.Label(new Rect(Screen.width - 240, y, 230, 20), $"{s.spellName} [T{s.tier}] Lv.{s.level}", st);

            // Riga 2: Sequenza Note (Grigio)
            string seqString = "";
            foreach (var n in s.sequence) seqString += n.noteName + " ";

            GUIStyle smallSt = new GUIStyle(st);
            smallSt.fontSize = 11;
            smallSt.normal.textColor = Color.gray;
            GUI.Label(new Rect(Screen.width - 240, y + 18, 230, 20), seqString, smallSt);

            y += 45;
        }
    }
}