using UnityEngine;
using System.Collections.Generic;

public class FloatingRuneSystem : MonoBehaviour
{
    [Header("--- RIFERIMENTI ---")]
    [Tooltip("Prefab della Runa (Sfera con materiale che supporta Emission).")]
    public GameObject runePrefab;
    [Tooltip("Oggetto vuoto sopra la testa del player che funge da centro per l'arco.")]
    public Transform anchorPoint;
    [Tooltip("Riferimento allo script SpellCasterSystem.")]
    public SpellCasterSystem spellSystem;

    [Header("--- LAYOUT ARCO ---")]
    [Tooltip("Distanza delle rune dalla testa.")]
    [Range(0.5f, 5f)] public float radius = 1.0f;
    [Tooltip("Quanto è aperto l'arco (es. 120 gradi).")]
    [Range(0f, 360f)] public float arcAngle = 120f;
    [Tooltip("Se VERO, l'arco è concavo (forma a U). Se FALSO, convesso (arcobaleno).")]
    public bool invertCurve = false;
    [Tooltip("Altezza extra verticale rispetto all'Anchor Point.")]
    public float heightOffset = 0.0f;

    [Header("--- ANIMAZIONE ---")]
    [Tooltip("Velocità con cui le rune raggiungono la loro posizione.")]
    public float moveSpeed = 10f;
    [Tooltip("Velocità dell'oscillazione verticale (Idle).")]
    public float bobSpeed = 2f;
    [Tooltip("Ampiezza dell'oscillazione verticale.")]
    public float bobAmount = 0.1f;

    [Header("--- COLORI ---")]
    [ColorUsage(true, true)]
    [Tooltip("Colore/Intensità extra quando una runa è attiva.")]
    public Color activeGlowMultiplier = new Color(2f, 2f, 2f, 1f);

    // Stato interno
    private List<GameObject> activeRunes = new List<GameObject>();
    private List<NoteDefinition> cachedNotes = new List<NoteDefinition>();
    private List<int> litIndices = new List<int>(); // Indici da illuminare

    void Update()
    {
        SyncWithSystem();
        UpdatePositions();
        UpdateVisuals();
    }

    /// <summary>
    /// Sincronizza il numero e il tipo di rune con la coda del SpellCasterSystem.
    /// </summary>
    void SyncWithSystem()
    {
        if (spellSystem == null) return;

        var sysQueue = spellSystem.GetCurrentQueue();

        // Recuperiamo sempre gli indici delle note che formano la spell (se presente)
        litIndices = spellSystem.GetMatchedIndices();

        // Controllo se dobbiamo ricostruire le rune fisiche
        bool needsRebuild = false;

        // Se il numero è diverso, ricostruisci
        if (sysQueue.Count != activeRunes.Count) needsRebuild = true;
        else
        {
            // Se il numero è uguale, controlla se il contenuto è cambiato
            for (int i = 0; i < sysQueue.Count; i++)
            {
                if (sysQueue[i] != cachedNotes[i]) { needsRebuild = true; break; }
            }
        }

        if (needsRebuild) RebuildRunes(sysQueue);
    }

    void RebuildRunes(List<NoteDefinition> newQueue)
    {
        // Pulisci le vecchie
        foreach (var r in activeRunes) Destroy(r);
        activeRunes.Clear();
        cachedNotes.Clear();

        // Crea le nuove
        foreach (var note in newQueue)
        {
            GameObject r = Instantiate(runePrefab, anchorPoint.position, Quaternion.identity);
            r.transform.SetParent(anchorPoint);

            var rend = r.GetComponent<Renderer>();
            if (rend)
            {
                // Imposta colore base e abilita emissione (spenta di base)
                rend.material.color = GetColor(note.color);
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", GetColor(note.color) * 0.1f);
            }
            activeRunes.Add(r);
            cachedNotes.Add(note);
        }
    }

    void UpdatePositions()
    {
        int count = activeRunes.Count;
        if (count == 0) return;

        // Calcola angolo iniziale per centrare l'arco
        float startAngle = -arcAngle / 2f;
        // Se c'è più di una runa, calcola lo step angolare tra una e l'altra
        float step = (count > 1) ? arcAngle / (count - 1) : 0;

        for (int i = 0; i < count; i++)
        {
            float angleDeg = startAngle + (step * i);

            // Calcolo posizione base sull'arco
            Vector3 targetLocalPos = CalculateRunePos(angleDeg);

            // Aggiungo animazione oscillante (Bobbing)
            // Sfasiamo l'onda usando l'indice 'i' per non farle muovere tutte insieme
            targetLocalPos.y += Mathf.Sin((Time.time * bobSpeed) + i) * bobAmount;

            // Movimento fluido verso la posizione target
            activeRunes[i].transform.localPosition = Vector3.Lerp(
                activeRunes[i].transform.localPosition,
                targetLocalPos,
                Time.deltaTime * moveSpeed
            );
        }
    }

    /// <summary>
    /// Gestisce l'illuminazione (Emission) delle rune.
    /// Accende SOLO quelle indicate da litIndices.
    /// </summary>
    void UpdateVisuals()
    {
        for (int i = 0; i < activeRunes.Count; i++)
        {
            var rend = activeRunes[i].GetComponent<Renderer>();
            if (!rend) continue;

            Color baseColor = GetColor(cachedNotes[i].color);

            // Se l'indice 'i' è nella lista degli indici matchati, ILLUMINA
            if (litIndices.Contains(i))
            {
                // Effetto Pulsante Veloce per indicare "Pronto al lancio"
                float pulse = Mathf.PingPong(Time.time * 6f, 1f) + 1.0f; // Valore tra 1 e 2
                Color finalGlow = baseColor * pulse * activeGlowMultiplier;
                rend.material.SetColor("_EmissionColor", finalGlow);
            }
            else
            {
                // Spento / Fioco
                rend.material.SetColor("_EmissionColor", baseColor * 0.1f);
            }
        }
    }

    // Helper per matematica condivisa (Runtime + Gizmos)
    Vector3 CalculateRunePos(float angleDeg)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;

        // X e Z per cerchio orizzontale
        float x = Mathf.Sin(angleRad) * radius;
        float z = Mathf.Cos(angleRad) * radius;

        // Se invertCurve è TRUE, invertiamo la Z per fare la forma a U
        if (invertCurve) z = -z;

        return new Vector3(x, heightOffset, z);
    }

    Color GetColor(NoteColor nc)
    {
        switch (nc)
        {
            case NoteColor.Green: return Color.green;
            case NoteColor.Blue: return Color.cyan;
            case NoteColor.Red: return Color.red;
            case NoteColor.Yellow: return Color.yellow;
            default: return Color.white;
        }
    }

    // --- GIZMOS EDITOR ---
    void OnDrawGizmosSelected()
    {
        if (anchorPoint == null) return;

        // Disegna usando la trasformazione locale dell'anchor
        Gizmos.matrix = anchorPoint.localToWorldMatrix;

        // 1. ARCO GIALLO
        Gizmos.color = Color.yellow;
        int segments = 30;
        Vector3 lastPoint = CalculateRunePos(-arcAngle / 2f);

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentAngle = Mathf.Lerp(-arcAngle / 2f, arcAngle / 2f, t);
            Vector3 nextPoint = CalculateRunePos(currentAngle);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }

        // 2. ANTEPRIMA RUNE (Sfere Ciano)
        Gizmos.color = Color.cyan;
        int previewCount = 4; // Simulazione visiva
        float step = (previewCount > 1) ? arcAngle / (previewCount - 1) : 0;

        for (int i = 0; i < previewCount; i++)
        {
            float angle = (-arcAngle / 2f) + (step * i);
            Vector3 pos = CalculateRunePos(angle);
            Gizmos.DrawWireSphere(pos, 0.15f);
        }
    }
}