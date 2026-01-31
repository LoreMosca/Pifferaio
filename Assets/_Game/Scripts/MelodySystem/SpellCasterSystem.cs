using System.Collections.Generic;
using UnityEngine;

public class SpellCasterSystem : MonoBehaviour
{
    [Header("Dependencies")]
    public SpellBuilder builder;
    public ProceduralGenerator generator;

    [Header("Input Mapping")]
    [Tooltip("Ordine Tasti: 1, 2, 3, 4")]
    public NoteDefinition[] keyMappings;

    [Header("Visuals")]
    public SpellVisualizer visualizer;

    [Header("Runtime State")]
    [SerializeField] private List<Melody> inventory = new List<Melody>();

    [Header("FIFO Queue")]
    [SerializeField] private List<NoteDefinition> currentInputQueue = new List<NoteDefinition>();

    private const int QUEUE_SIZE = 4;
    private Melody readySpell = null;

    void Start()
    {
        if (generator == null) generator = GetComponent<ProceduralGenerator>();
    }

    void Update()
    {
        // Input Tasti 1-4
        if (Input.GetKeyDown(KeyCode.Alpha1)) PushNote(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) PushNote(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) PushNote(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) PushNote(3);

        // Cast
        if (Input.GetKeyDown(KeyCode.Space)) CastSpell();

        // Debug Loot
        if (Input.GetKeyDown(KeyCode.L)) LootRandom(1);
    }

    void PushNote(int index)
    {
        if (keyMappings == null || index >= keyMappings.Length || keyMappings[index] == null)
        {
            Debug.LogWarning($"KeyMapping mancante all'indice {index}");
            return;
        }

        currentInputQueue.Insert(0, keyMappings[index]);

        if (currentInputQueue.Count > QUEUE_SIZE)
            currentInputQueue.RemoveAt(currentInputQueue.Count - 1);

        CheckMatches();
    }

    void CheckMatches()
    {
        readySpell = null;
        if (inventory == null) return;

        foreach (var spell in inventory)
        {
            if (currentInputQueue.Count < spell.sequence.Count) continue;

            bool match = true;
            for (int i = 0; i < spell.sequence.Count; i++)
            {
                int queueIndex = (spell.sequence.Count - 1) - i;
                if (currentInputQueue[queueIndex] != spell.sequence[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                readySpell = spell;
                break;
            }
        }
    }

    void CastSpell()
    {
        if (readySpell == null) return;

        // Nota: Passiamo l'intera 'readySpell' al builder, non solo la sequenza, 
        // perché ci serve il Livello per il calcolo dei danni.
        SpellPayload payload = builder.BuildSpell(readySpell);

        Debug.Log($"<color=cyan>CASTING: {payload.constructedName.ToUpper()}</color>");

        if (visualizer != null)
        {
            visualizer.VisualizeSpell(payload, this.transform);
        }

        currentInputQueue.Clear();
        readySpell = null;
    }

    // <--- NUOVA LOGICA LOOT (LEVEL UP)
    void LootRandom(int tier)
    {
        if (generator != null)
        {
            Melody newLoot = generator.GenerateLoot(tier);
            if (newLoot != null)
            {
                // Cerchiamo se esiste già
                Melody existingSpell = inventory.Find(m => m.IsSameSequence(newLoot));

                if (existingSpell != null)
                {
                    // Duplicato -> Level Up
                    existingSpell.level++;
                    Debug.Log($"<color=yellow>LEVEL UP!</color> {existingSpell.spellName} ora è Livello {existingSpell.level}");
                }
                else
                {
                    // Nuovo -> Aggiungi
                    inventory.Add(newLoot);
                    Debug.Log($"Loot Generato: {newLoot.spellName}");
                }

                CheckMatches(); // Aggiorna stato ready immediato
            }
        }
        else
        {
            Debug.LogError("ProceduralGenerator non assegnato!");
        }
    }

    void OnGUI()
    {
        GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        st.normal.textColor = Color.white;
        GUI.Box(new Rect(10, 10, 600, 400), "SYSTEM STATUS");

        string q = "";
        List<NoteDefinition> visualList = new List<NoteDefinition>(currentInputQueue);
        visualList.Reverse();
        foreach (var n in visualList) q += $"[{n.noteName}] ";

        GUI.Label(new Rect(20, 40, 580, 30), "QUEUE: " + q, st);

        if (readySpell != null)
        {
            st.normal.textColor = Color.green;
            GUI.Label(new Rect(20, 80, 580, 30), $"READY: {readySpell.spellName}!", st);

            SpellPayload preview = builder.BuildSpell(readySpell);
            st.fontSize = 12; st.normal.textColor = Color.cyan;
            GUI.Label(new Rect(20, 100, 580, 40), $"{preview.constructedName}\n({preview.effect} - {preview.delivery})", st);
        }

        st.fontSize = 14; st.normal.textColor = Color.white;
        GUI.Label(new Rect(20, 150, 580, 30), "INVENTORY (Press L):", st);
        float y = 170;
        foreach (var s in inventory)
        {
            string seqStr = "";
            foreach (var n in s.sequence) seqStr += n.noteName.Substring(0, 1) + " ";

            // Aggiunto display livello
            GUI.Label(new Rect(20, y, 580, 20), $"- {s.spellName} (Lv.{s.level}): {seqStr}", st);
            y += 20;
        }
    }
}