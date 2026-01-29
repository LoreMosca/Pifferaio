using System.Collections.Generic;
using System.Linq; // Necessario per .Last() etc
using UnityEngine;

public class SpellCasterSystem : MonoBehaviour
{
    [Header("Componenti Richiesti")]
    public ProceduralGenerator generator;

    [Header("Input Settings")]
    [Tooltip("Le 4 note assegnate ai tasti 1, 2, 3, 4.")]
    public NoteDefinition[] noteKeyMap; // Ordine: 0=Tasto1, 1=Tasto2...

    [Header("Stato Giocatore")]
    [SerializeField]
    [Tooltip("Incantesimi attualmente conosciuti/nell'inventario.")]
    private List<Melody> knownSpells = new List<Melody>();

    [SerializeField]
    [Tooltip("Buffer FIFO delle ultime note suonate.")]
    private List<NoteDefinition> noteQueue = new List<NoteDefinition>();

    // Variabile privata per tracciare se abbiamo un match pronto
    private Melody readyToCastSpell = null;

    [Header("Debug")]
    public bool showDebugGUI = true;

    // Costante per la dimensione della FIFO
    private const int MAX_QUEUE_SIZE = 4;

    void Update()
    {
        HandleInput();

        // Loot Debug (Tasto L = Tier 1, K = Tier 2)
        if (Input.GetKeyDown(KeyCode.L)) LootNewSpell(1);
        if (Input.GetKeyDown(KeyCode.K)) LootNewSpell(2);
    }

    void HandleInput()
    {
        // Input Note (1, 2, 3, 4)
        if (Input.GetKeyDown(KeyCode.Alpha1)) AddNoteToQueue(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) AddNoteToQueue(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) AddNoteToQueue(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) AddNoteToQueue(3);

        // Input Cast (Spazio)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryCastSpell();
        }
    }

    void AddNoteToQueue(int mapIndex)
    {
        if (mapIndex >= noteKeyMap.Length || noteKeyMap[mapIndex] == null) return;

        // 1. Aggiungi nota
        noteQueue.Add(noteKeyMap[mapIndex]);

        // 2. FIFO Logic: Se superiamo 4, togliamo la più vecchia (indice 0)
        if (noteQueue.Count > MAX_QUEUE_SIZE)
        {
            noteQueue.RemoveAt(0);
        }

        // 3. Check Match
        CheckForMatches();
    }

    void CheckForMatches()
    {
        readyToCastSpell = null;

        // Controlliamo ogni spell nell'inventario
        foreach (var spell in knownSpells)
        {
            // Per matchare, la queue deve finire con la sequenza della spell
            // Esempio: Queue [Verde, Rosso, Blu], Spell [Rosso, Blu] -> MATCH!

            if (IsSubsequenceMatch(spell.sequence, noteQueue))
            {
                readyToCastSpell = spell;
                // Prendiamo il match più lungo o il primo trovato? Per ora il primo.
                break;
            }
        }
    }

    // Controlla se 'spellSeq' combacia con la PARTE FINALE di 'currentQueue'
    bool IsSubsequenceMatch(List<NoteDefinition> spellSeq, List<NoteDefinition> currentQueue)
    {
        if (spellSeq.Count > currentQueue.Count) return false;

        int offset = currentQueue.Count - spellSeq.Count;
        for (int i = 0; i < spellSeq.Count; i++)
        {
            if (currentQueue[offset + i] != spellSeq[i]) return false;
        }
        return true;
    }

    void TryCastSpell()
    {
        if (readyToCastSpell != null)
        {
            Debug.Log($"<color=green>CASTING {readyToCastSpell.spellName}!</color>");

            // Qui implementerai l'effetto vero
            // ...

            // Consumiamo la queue? Di solito sì per evitare doppio cast immediato
            noteQueue.Clear();
            readyToCastSpell = null;
        }
        else
        {
            Debug.Log("FZZT! Nulla da castare (o ritmo sbagliato).");
        }
    }

    public void LootNewSpell(int tier)
    {
        Melody newSpell = generator.GenerateLoot(tier);
        if (newSpell != null)
        {
            knownSpells.Add(newSpell);
            Debug.Log($"Lootato: {newSpell.spellName}");
        }
    }

    // --- VISUAL DEBUGGER (OnGUI) ---
    // Disegna a schermo senza bisogno di Canvas setup
    void OnGUI()
    {
        if (!showDebugGUI) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        // Box Sfondo
        GUI.Box(new Rect(10, 10, 400, 500), "SYSTEM DEBUG");

        // 1. Mostra Queue Attuale
        string queueStr = "";
        foreach (var n in noteQueue) queueStr += $"[{n.noteName}] ";
        GUI.Label(new Rect(20, 40, 380, 30), $"QUEUE (FIFO 4):", style);

        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(20, 70, 380, 30), queueStr, style);

        // 2. Mostra Stato Match
        style.normal.textColor = readyToCastSpell != null ? Color.green : Color.gray;
        string status = readyToCastSpell != null ? $"READY TO CAST: {readyToCastSpell.spellName} (Press SPACE)" : "NO MATCH";
        GUI.Label(new Rect(20, 110, 380, 30), status, style);

        // 3. Mostra Inventario
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(20, 160, 380, 30), "KNOWN SPELLS (Press L to loot):", style);

        int y = 190;
        foreach (var spell in knownSpells)
        {
            string seq = "";
            foreach (var n in spell.sequence) seq += n.noteName.Substring(0, 1) + " "; // Solo iniziali

            // Se questa è la spell pronta, colorala
            style.normal.textColor = (spell == readyToCastSpell) ? Color.green : Color.white;
            GUI.Label(new Rect(20, y, 380, 25), $"- {spell.spellName} [{seq}]", style);
            y += 25;
        }
    }
}