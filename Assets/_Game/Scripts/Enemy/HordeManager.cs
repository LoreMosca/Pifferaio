using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Necessario per ordinare le liste

// --------------------------------------------------------
// CLASSI DI DATI (Serializzabili per Inspector)
// --------------------------------------------------------

[System.Serializable]
public class EnemyGroup
{
    [Tooltip("Nome descrittivo per l'inspector (es. 'Ratti Veloci').")]
    public string label = "Gruppo Nemici";

    [Tooltip("Il Prefab del nemico da spawnare.")]
    public GameObject prefab;

    [Tooltip("Numero totale di nemici in questo gruppo.")]
    public int count = 10;

    [Tooltip("Livello dei nemici (influenza HP, Danno, Drop, Colore).")]
    public int level = 1;

    [Tooltip("Tempo (in secondi) tra uno spawn e l'altro di questo gruppo.")]
    public float spawnRate = 0.5f;

    [Tooltip("Ritardo iniziale (in secondi) prima che questo gruppo inizi a spawnare.")]
    public float startDelay = 0f;
}

[System.Serializable]
public class Wave
{
    [Tooltip("Nome dell'ondata (es. 'Ondata 1 - Invasione').")]
    public string waveName = "Ondata 1";

    [Tooltip("Lista dei gruppi di nemici che compongono questa ondata (spawnano in parallelo).")]
    public List<EnemyGroup> groups = new List<EnemyGroup>();

    [Header("Fine Ondata")]
    [Tooltip("Se VERO, l'ondata non finisce finché tutti i nemici non sono morti. Se FALSO, finisce appena ha finito di spawnare.")]
    public bool waitUntilClear = false;

    [Tooltip("Tempo di pausa (in secondi) prima di passare alla prossima ondata.")]
    public float breakDuration = 5f;
}

[System.Serializable]
public class LevelingConfig
{
    [Header("Moltiplicatori Statistici")]
    [Tooltip("Aumento percentuale HP per livello (0.2 = +20%).")]
    public float hpPerLevel = 0.2f;
    [Tooltip("Aumento percentuale Danno per livello (0.1 = +10%).")]
    public float dmgPerLevel = 0.1f;
    [Tooltip("Aumento percentuale Grandezza Modello per livello (0.05 = +5%).")]
    public float scalePerLevel = 0.05f;

    [Header("Visuals")]
    [Tooltip("Gradiente colore dal Livello 1 (Sinistra) al Livello 10+ (Destra).")]
    public Gradient levelColorGradient;
}

[System.Serializable]
public class LootTierDefinition
{
    [Tooltip("Livello minimo del nemico affinché usi questa tabella.")]
    public int minEnemyLevel = 1;
    [Tooltip("La tabella di loot da usare.")]
    public LootTable table;
    [Tooltip("Probabilità di drop (0.0 - 1.0) per questa fascia di livello.")]
    [Range(0f, 1f)] public float dropChance = 0.2f;
}

// --------------------------------------------------------
// CLASSE PRINCIPALE MANAGER
// --------------------------------------------------------

public class HordeManager : MonoBehaviour
{
    [Header("--- CONTROLLO FLUSSO (NUOVO) ---")]
    public bool spawningActive = false; // Parte spento finché non viene attivato

    [Header("--- CONFIGURAZIONE ONDATE ---")]
    [Tooltip("Lista sequenziale delle ondate definite a mano.")]
    public List<Wave> waves = new List<Wave>();

    [Tooltip("Se attivato, genera ondate procedurali infinite quando finiscono quelle manuali.")]
    public bool infiniteMode = true;

    [Header("--- BILANCIAMENTO NEMICI ---")]
    public LevelingConfig levelingConfig;

    [Header("--- CONFIGURAZIONE LOOT ---")]
    [Tooltip("Il Prefab fisico del loot (Sfera/Cristallo).")]
    public GameObject lootPrefab;

    [Tooltip("Definisci quali tabelle usare in base al livello del nemico. Ordine non importante, il sistema le ordina.")]
    public List<LootTierDefinition> lootTiers = new List<LootTierDefinition>();

    [Header("--- SPAWN SETTINGS ---")]
    [Tooltip("Distanza MINIMA di sicurezza dai target (Player e Principe). I nemici non spawnano più vicini di così.")]
    public float minSafeDistance = 8f;

    [Tooltip("Distanza MASSIMA di spawn dal punto medio tra i bersagli.")]
    public float maxSpawnDistance = 20f;

    [Tooltip("Trascina qui i Collider (Trigger) delle Safe Zones (Start e End).")]
    public List<Collider> safeZones = new List<Collider>();

    // Riferimenti Runtime
    private Transform playerTransform;
    private Transform princeTransform;
    private ProceduralGenerator generator;

    // Stato Interno
    private int currentWaveIndex = 0;
    private int activeEnemies = 0;

    void Start()
    {
        generator = FindFirstObjectByType<ProceduralGenerator>();

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTransform = p.transform;

        GameObject pr = GameObject.FindGameObjectWithTag("Principe");
        if (pr) princeTransform = pr.transform;

        // Fallback: Se non c'è gradiente, creane uno bianco di default
        if (levelingConfig.levelColorGradient == null)
            levelingConfig.levelColorGradient = new Gradient();

        // Ordina i Loot Tiers per livello decrescente (dal più alto al più basso) per facilitare la ricerca
        lootTiers.Sort((a, b) => b.minEnemyLevel.CompareTo(a.minEnemyLevel));

        StartCoroutine(GameLoop());
    }

    // --- NUOVI METODI PUBBLICI PER IL FLUSSO ---
    public void StartHorde()
    {
        spawningActive = true;
        Debug.Log("<color=green>ORDA INIZIATA!</color>");
    }

    public void StopAndClearHorde()
    {
        spawningActive = false;
        StopAllCoroutines(); // Ferma spawn futuri

        // Uccidi tutti i nemici vivi per pulire la scena
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Nemico");
        foreach (var enemy in enemies)
        {
            // Prova a ucciderli "bene" (animazione morte)
            IDamageable dmg = enemy.GetComponent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(9999f);
            else Destroy(enemy); // O distruggi brutalmente se non hanno stats
        }

        Debug.Log("<color=green>ORDA FERMATA E NEMICI ELIMINATI.</color>");
    }
    // ------------------------------------------

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(2f); // Breve attesa all'avvio scena

        // --- MODIFICA: Attendi che qualcuno attivi l'orda ---
        while (!spawningActive)
        {
            yield return null;
        }
        // ----------------------------------------------------

        while (spawningActive) // Aggiunto check spawningActive al loop
        {
            Wave currentWave = GetWave(currentWaveIndex);
            Debug.Log($"<color=cyan>--- INIZIO {currentWave.waveName} ---</color>");

            // Avvia tutti i gruppi dell'ondata come Coroutines parallele
            List<Coroutine> runningGroups = new List<Coroutine>();
            foreach (var group in currentWave.groups)
            {
                runningGroups.Add(StartCoroutine(SpawnGroupRoutine(group)));
            }

            // Aspetta che tutti i gruppi abbiano finito di eseguire la loro logica di SPAWN
            foreach (var c in runningGroups) yield return c;

            // Logica "Wait Until Clear": Aspetta che i nemici muoiano tutti?
            if (currentWave.waitUntilClear)
            {
                // Controllo ogni secondo se ci sono nemici vivi
                while (activeEnemies > 0)
                {
                    yield return new WaitForSeconds(1f);
                }
            }

            Debug.Log($"Ondata completata. Pausa di {currentWave.breakDuration}s");
            yield return new WaitForSeconds(currentWave.breakDuration);

            currentWaveIndex++;
        }
    }

    IEnumerator SpawnGroupRoutine(EnemyGroup group)
    {
        // Ritardo iniziale del gruppo
        if (group.startDelay > 0) yield return new WaitForSeconds(group.startDelay);

        for (int i = 0; i < group.count; i++)
        {
            // Se l'orda è stata fermata mentre spawnava, interrompi
            if (!spawningActive) break;

            SpawnEnemy(group.prefab, group.level);

            // Ritmo di spawn
            yield return new WaitForSeconds(group.spawnRate);
        }
    }

    void SpawnEnemy(GameObject prefab, int level)
    {
        if (prefab == null) return;

        Vector3 spawnPos = GetSafeSmartSpawnPosition();
        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
        activeEnemies++; // Incrementa contatore nemici vivi

        // 1. Configura DummyController (Stats, Visuals, Eventi)
        var stats = enemy.GetComponent<DummyController>();
        if (stats)
        {
            stats.autoRespawn = false;
            stats.isTrainingDummy = false;

            // Applica la configurazione di livello globale
            stats.InitializeLevel(level, levelingConfig);

            // Sottoscrivi all'evento morte
            stats.OnDeath += (pos, lvl) => {
                activeEnemies--; // Decrementa nemici vivi
                TryDropLoot(pos, lvl); // Calcola drop
            };
        }

        // 2. Configura AI (Danno & Velocità movimento)
        var ai = enemy.GetComponent<RatAI>();
        if (ai)
        {
            // Calcola moltiplicatori basati sul livello
            float dmgMult = 1.0f + ((level - 1) * levelingConfig.dmgPerLevel);
            // La velocità scala la metà rispetto alla grandezza per non avere ratti supersonici
            float speedMult = 1.0f + ((level - 1) * (levelingConfig.scalePerLevel * 0.5f));

            ai.SetStats(dmgMult, speedMult);
        }
    }

    // --- LOGICA DI SPAWN AVANZATA (SAFE ZONES) ---
    Vector3 GetSafeSmartSpawnPosition()
    {
        Vector3 center = transform.position;
        if (playerTransform && princeTransform)
            center = (playerTransform.position + princeTransform.position) * 0.5f;
        else if (playerTransform) center = playerTransform.position;
        else if (princeTransform) center = princeTransform.position;

        for (int i = 0; i < 20; i++) // Aumentato a 20 tentativi
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minSafeDistance * 1.5f, maxSpawnDistance);
            Vector3 candidatePos = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            // 1. Check Distanza Player/Prince
            if (playerTransform && Vector3.Distance(candidatePos, playerTransform.position) < minSafeDistance) continue;
            if (princeTransform && Vector3.Distance(candidatePos, princeTransform.position) < minSafeDistance) continue;

            // 2. CHECK SAFE ZONES (NUOVO)
            bool insideSafeZone = false;
            foreach (var zone in safeZones)
            {
                if (zone != null && zone.bounds.Contains(candidatePos))
                {
                    insideSafeZone = true;
                    break;
                }
            }
            if (insideSafeZone) continue; // Punto scartato, è in una safe zone

            // 3. Check NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidatePos, out hit, 4.0f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return center + (Vector3.forward * maxSpawnDistance);
    }

    // --- LOGICA ONDATE INFINITE ---
    Wave GetWave(int index)
    {
        // Se l'ondata è definita a mano, usala
        if (index < waves.Count) return waves[index];

        // Altrimenti, genera procedurale
        if (!infiniteMode && waves.Count > 0) return waves[waves.Count - 1]; // Ripeti ultima se no infinite

        int extra = index - waves.Count + 1;
        Wave infiniteWave = new Wave();
        infiniteWave.waveName = $"Ondata Infinita {extra}";
        infiniteWave.breakDuration = 5f;
        infiniteWave.waitUntilClear = false;

        // Gruppo 1: Carne da macello (Tanti, livello medio-basso)
        EnemyGroup fodder = new EnemyGroup();
        fodder.label = "Orda Base";
        fodder.prefab = waves.Count > 0 && waves[0].groups.Count > 0 ? waves[0].groups[0].prefab : null;
        fodder.count = 15 + (extra * 2);
        fodder.level = 1 + (extra / 2);
        fodder.spawnRate = Mathf.Max(0.2f, 1.0f - (extra * 0.05f));

        // Gruppo 2: Elite (Pochi, livello alto)
        EnemyGroup elites = new EnemyGroup();
        elites.label = "Elites";
        elites.prefab = fodder.prefab;
        elites.count = 2 + (extra / 3);
        elites.level = 5 + extra; // Livello molto più alto
        elites.spawnRate = 3.0f;
        elites.startDelay = 5.0f;

        infiniteWave.groups.Add(fodder);
        infiniteWave.groups.Add(elites);

        return infiniteWave;
    }

    // --- LOGICA LOOT ---
    void TryDropLoot(Vector3 pos, int level)
    {
        if (lootPrefab == null || generator == null) return;

        // 1. Trova la definizione di Loot corretta per questo livello
        // La lista è ordinata decrescente, quindi il primo che matcha (level >= min) è quello giusto.
        LootTierDefinition tierDef = lootTiers.FirstOrDefault(t => level >= t.minEnemyLevel);

        // Se non trova nulla, usa un default implicito (nessun drop o base)
        if (tierDef == null || tierDef.table == null) return;

        // 2. Calcola Probabilità
        if (Random.value > tierDef.dropChance) return;

        // 3. Genera e Spawna
        Melody loot = generator.GenerateLoot(tierDef.table);
        GameObject obj = Instantiate(lootPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
        var pickup = obj.GetComponent<LootPickup>();
        if (pickup) pickup.Initialize(loot);
    }
}