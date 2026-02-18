using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(DummyController))]
public class PayloadMover : MonoBehaviour
{
    // Singleton per accesso facile dal SpellCaster
    public static PayloadMover Instance;

    public enum PrinceState
    {
        MovingForward,
        WaitingForPlayer, // NUOVO: Aspetta il pifferaio
        FrozenInFear,
        PanickingRetreat,
        Completed
    }

    [Header("--- PERCORSO ---")]
    public List<Transform> waypoints;
    public float reachThreshold = 1.0f;

    [Header("--- MOVIMENTO & PROSSIMITÀ ---")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.0f;

    [Tooltip("Distanza massima entro cui il Pifferaio deve stare affinché il Principe avanzi.")]
    public float playerProximity = 8.0f;

    [Header("--- SISTEMA PAURA ---")]
    [Range(1f, 15f)] public float fearRadius = 6.0f;
    public LayerMask enemyLayer;
    public float fearBuildRate = 20f;
    public float fearRecoveryRate = 15f;

    [Header("--- SISTEMA CAPRICCI (Whims) ---")]
    [Tooltip("Se disattivato, il principe non chiederà mai colori.")]
    public bool enableWhims = true;

    [Tooltip("Ogni quanti secondi il principe chiede un nuovo colore.")]
    public float whimInterval = 15f;

    [Tooltip("Zona di tolleranza (in secondi). Se lanci una magia entro questo tempo dalla richiesta, viene ignorata (non fallisce, non da bonus).")]
    public float gracePeriod = 1.0f;

    public GameObject floatingTextPrefab;
    public Transform popupSpawnPoint;

    [Header("--- UI FEEDBACK ---")]
    public Image fearBarFill;
    public GameObject fearBarContainer;

    [Tooltip("Icona che appare quando il Principe ha un Capriccio (es. Cerchio bianco che si colora).")]
    public Image whimIcon;
    public GameObject whimContainer;

    [Header("--- DEBUG INFO ---")]
    public PrinceState currentState = PrinceState.MovingForward;
    [Range(0, 100)] public float currentFear = 0f;
    public int enemiesNearbyCount = 0;

    // Whim State
    public bool hasActiveWhim = false;
    public NoteColor currentWhimColor;

    // Interni
    private NavMeshAgent agent;
    private Animator animator;
    private int currentWaypointIndex = 0;
    private const float MAX_FEAR = 100f;
    private Transform playerTransform;

    private float whimTimer = 0f;
    private float whimStartTime = 0f;

    // Hash Animazioni
    private readonly int AnimSpeed = Animator.StringToHash("Speed");
    private readonly int AnimPanic = Animator.StringToHash("IsPanicking");

    void Awake()
    {
        Instance = this;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        // Trova il player tramite GameManager (che abbiamo creato prima)
        if (GameManager.Instance && GameManager.Instance.playerTransform)
            playerTransform = GameManager.Instance.playerTransform;
        else
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

        agent.speed = walkSpeed;
        if (waypoints.Count > 0) agent.SetDestination(waypoints[0].position);

        if (fearBarContainer) fearBarContainer.SetActive(false);
        if (whimContainer) whimContainer.SetActive(false);

        // Avvia ciclo capricci
        whimTimer = whimInterval;
    }

    void Update()
    {
        if (GameManager.Instance.isGameOver || currentState == PrinceState.Completed)
        {
            agent.isStopped = true;
            return;
        }

        // 1. Logiche di Base
        DetectEnemies();
        UpdateFearLogic();

        if (enableWhims)
        {
            UpdateWhimLogic();
        }
        else
        {
            // Se disattivi a runtime, pulisci tutto
            if (hasActiveWhim) CancelWhim();
        }

        // 2. Macchina a Stati Movimento
        HandleStateMovement();

        // 3. Visuals
        UpdateVisuals();
    }

    // --- LOGICA CAPRICCI (WHIMS) ---
    void UpdateWhimLogic()
    {
        // Se non ha un capriccio attivo, conta il tempo
        if (!hasActiveWhim)
        {
            whimTimer -= Time.deltaTime;
            if (whimTimer <= 0)
            {
                GenerateWhim();
            }
        }
    }

    void GenerateWhim()
    {
        hasActiveWhim = true;
        whimStartTime = Time.time; // Segna l'inizio per la Grace Period

        currentWhimColor = (NoteColor)Random.Range(0, 4);

        string msg = "";
        Color c = Color.white;

        switch (currentWhimColor)
        {
            case NoteColor.Green: msg = "VOGLIO VERDE!"; c = Color.green; break;
            case NoteColor.Blue: msg = "VOGLIO BLU!"; c = Color.cyan; break;
            case NoteColor.Red: msg = "VOGLIO ROSSO!"; c = Color.red; break;
            case NoteColor.Yellow: msg = "VOGLIO GIALLO!"; c = Color.yellow; break;
        }

        SpawnPopup(msg, c, 6f);
    }

    void SpawnPopup(string text, Color color, float size)
    {
        if (!floatingTextPrefab) return;

        Vector3 spawnPos = popupSpawnPoint ? popupSpawnPoint.position : transform.position + Vector3.up * 2.5f;
        GameObject obj = Instantiate(floatingTextPrefab, spawnPos, Quaternion.identity);

        // Assicurati che abbia lo script FloatingText
        FloatingText ft = obj.GetComponent<FloatingText>();
        if (ft)
        {
            ft.Setup(text, color, size);
        }

        // Opzionale: Ruota verso la camera se il prefab non lo fa da solo
        obj.transform.LookAt(Camera.main.transform);
        obj.transform.Rotate(0, 180, 0);
    }

    void CancelWhim()
    {
        hasActiveWhim = false;
        whimTimer = whimInterval; // Resetta il timer per il PROSSIMO capriccio
    }

    /// <summary>
    /// Chiamato dal SpellCasterSystem prima di lanciare una magia.
    /// Restituisce 2.0f se il colore corrisponde al capriccio, altrimenti 1.0f.
    /// </summary>
    /// <summary>
    /// Logica "One-Shot":
    /// - Se sei nella Grace Period -> Ignora (ritorna 1, non chiude).
    /// - Se Giusto -> Bonus x2, Chiude (Successo).
    /// - Se Sbagliato -> Bonus x1, Chiude (Fallimento).
    /// </summary>
    public float CheckWhimBonus(NoteColor castColor)
    {
        if (!enableWhims || !hasActiveWhim) return 1.0f;

        // 1. GESTIONE GRACE PERIOD (Zona di Tolleranza)
        // Se il giocatore lancia una magia troppo presto (es. stava già premendo), ignoriamo tutto.
        if (Time.time < whimStartTime + gracePeriod)
        {
            return 1.0f; // Nessun bonus, ma il desiderio RIMANE ATTIVO per il prossimo colpo
        }

        // 2. CONTROLLO COLORE
        if (castColor == currentWhimColor)
        {
            // --- SUCCESSO ---
            Debug.Log($"<color=green>CAPRICCIO SODDISFATTO! Bonus x2</color>");
            SpawnPopup("SIIII!", Color.white, 5f); // Feedback positivo opzionale
            CancelWhim(); // Chiude la richiesta
            return 2.0f;
        }
        else
        {
            // --- FALLIMENTO ---
            // Hai lanciato la magia sbagliata DOPO la grace period. Hai perso l'occasione.
            Debug.Log($"<color=orange>CAPRICCIO FALLITO (Voleva {currentWhimColor}, dato {castColor})</color>");
            SpawnPopup("BAH...", Color.gray, 4f); // Feedback negativo opzionale
            CancelWhim(); // Chiude la richiesta (Fallita)
            return 1.0f;
        }
    }

    Color GetColorFromEnum(NoteColor nc)
    {
        switch (nc)
        {
            case NoteColor.Green: return Color.green;
            case NoteColor.Blue: return Color.cyan; // Cyan si vede meglio del blu scuro
            case NoteColor.Red: return Color.red;
            case NoteColor.Yellow: return Color.yellow;
            default: return Color.white;
        }
    }

    // --- LOGICA MOVIMENTO & PAURA ---

    void DetectEnemies()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, fearRadius, enemyLayer);
        enemiesNearbyCount = enemies.Length;
    }

    void UpdateFearLogic()
    {
        // Se siamo in panico, dobbiamo calmarci completamente (0) prima di ripartire
        if (currentState == PrinceState.PanickingRetreat)
        {
            if (enemiesNearbyCount == 0) currentFear -= fearRecoveryRate * Time.deltaTime;

            if (currentFear <= 0)
            {
                currentFear = 0;
                // Torna a Waiting o Moving in base alla distanza player
                currentState = IsPlayerClose() ? PrinceState.MovingForward : PrinceState.WaitingForPlayer;
            }
        }
        else
        {
            // Logica accumulo paura
            if (enemiesNearbyCount > 0)
            {
                currentFear += (fearBuildRate * enemiesNearbyCount) * Time.deltaTime;
                currentState = PrinceState.FrozenInFear;

                if (currentFear >= MAX_FEAR)
                {
                    currentFear = MAX_FEAR;
                    currentState = PrinceState.PanickingRetreat;
                }
            }
            else
            {
                // Recupero paura
                currentFear -= fearRecoveryRate * Time.deltaTime;
                if (currentFear <= 0) currentFear = 0;

                // Se la paura è 0 e non ci sono nemici, decide se muoversi o aspettare
                if (currentFear == 0)
                {
                    currentState = IsPlayerClose() ? PrinceState.MovingForward : PrinceState.WaitingForPlayer;
                }
                else
                {
                    // Ha ancora un po' di paura residua -> Resta Frozen
                    currentState = PrinceState.FrozenInFear;
                }
            }
        }
    }

    bool IsPlayerClose()
    {
        if (playerTransform == null) return true; // Fallback
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        return dist <= playerProximity;
    }

    void HandleStateMovement()
    {
        switch (currentState)
        {
            case PrinceState.WaitingForPlayer:
                agent.isStopped = true;
                // Qui potresti triggerare animazione "Impaziente" o "Saluta"
                break;

            case PrinceState.MovingForward:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                MoveToNextWaypoint();
                break;

            case PrinceState.FrozenInFear:
                agent.isStopped = true;
                break;

            case PrinceState.PanickingRetreat:
                agent.isStopped = false;
                agent.speed = runSpeed;
                RunBackwards();
                break;
        }
    }

    void MoveToNextWaypoint()
    {
        if (currentWaypointIndex >= waypoints.Count)
        {
            currentState = PrinceState.Completed;
            GameManager.Instance.TriggerVictory();
            return;
        }

        agent.SetDestination(waypoints[currentWaypointIndex].position);

        if (!agent.pathPending && agent.remainingDistance < reachThreshold)
        {
            currentWaypointIndex++;
        }
    }

    void RunBackwards()
    {
        // Logica migliorata: torna all'indice precedente
        int retreatIndex = Mathf.Max(0, currentWaypointIndex - 1);

        agent.SetDestination(waypoints[retreatIndex].position);

        // Se raggiunge il punto precedente
        if (!agent.pathPending && agent.remainingDistance < reachThreshold)
        {
            // Decrementa l'indice solo se non siamo già all'inizio
            if (currentWaypointIndex > 0)
            {
                currentWaypointIndex--;
                // Forza immediatamente il target al NUOVO punto precedente per fluidità
                int nextRetreat = Mathf.Max(0, currentWaypointIndex - 1);
                agent.SetDestination(waypoints[nextRetreat].position);
            }
        }
    }

    void UpdateVisuals()
    {
        // Animator Update
        if (animator)
        {
            float speed = agent.isStopped ? 0 : agent.velocity.magnitude;
            animator.SetFloat(AnimSpeed, speed);
            animator.SetBool(AnimPanic, currentState == PrinceState.PanickingRetreat);
        }

        // Fear Bar Update
        if (fearBarFill)
        {
            float pct = currentFear / MAX_FEAR;
            fearBarFill.fillAmount = pct;
            fearBarFill.color = Color.Lerp(Color.white, Color.red, pct);
        }

        if (fearBarContainer)
        {
            fearBarContainer.SetActive(currentFear > 0);
        }

        // Whim Icon Pulse (opzionale)
        if (whimContainer && whimContainer.activeSelf)
        {
            float scale = 1f + Mathf.Sin(Time.time * 5f) * 0.1f;
            whimContainer.transform.localScale = Vector3.one * scale;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, fearRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, playerProximity);
    }
}