using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)] // Si inizializza prima di tutto il resto
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("--- RIFERIMENTI GLOBALI ---")]
    [Tooltip("Trascina qui il Player.")]
    public Transform playerTransform;
    [Tooltip("Trascina qui il Principe.")]
    public Transform princeTransform;

    [Header("--- STATO GIOCO ---")]
    [Tooltip("Sola lettura. Indica se il gioco è finito.")]
    public bool isGameOver = false;
    public bool isVictory = false;

    // Riferimenti agli script di stat per iscriversi agli eventi morte
    private DummyController princeStats;
    private PlayerStats playerStats;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        AutoFindReferences();
        SubscribeToEvents();
    }

    void AutoFindReferences()
    {
        if (!playerTransform)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTransform = p.transform;
        }

        if (!princeTransform)
        {
            GameObject pr = GameObject.FindGameObjectWithTag("Principe");
            if (pr) princeTransform = pr.transform;
        }

        if (playerTransform) playerStats = playerTransform.GetComponent<PlayerStats>();
        if (princeTransform) princeStats = princeTransform.GetComponent<DummyController>();
    }

    void SubscribeToEvents()
    {
        // Iscrizione Morte Principe
        if (princeStats)
        {
            princeStats.OnDeath += (pos, lvl) => GameOver("Il Principe è caduto!");
        }

        // Iscrizione Morte Player
        if (playerStats)
        {
            playerStats.OnDeath += () => GameOver("Il Pifferaio è morto!");
        }
    }

    public void TriggerVictory()
    {
        if (isGameOver) return;
        isVictory = true;
        isGameOver = true;
        Debug.Log("<color=green>VITTORIA! Il Principe è salvo.</color>");
        // Qui attiverai il pannello UI vittoria
    }

    void GameOver(string reason)
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log($"<color=red>GAME OVER: {reason}</color>");
        // Qui attiverai il pannello UI sconfitta
        // Time.timeScale = 0; // Opzionale: Ferma il gioco
    }

    /// <summary>
    /// Usato dai nemici per sapere chi attaccare.
    /// Restituisce il target più vicino tra Player e Principe.
    /// </summary>
    public Transform GetClosestTarget(Vector3 enemyPos)
    {
        if (isGameOver) return null;
        if (playerTransform == null && princeTransform == null) return null;
        if (playerTransform == null) return princeTransform;
        if (princeTransform == null) return playerTransform;

        float distPlayer = Vector3.SqrMagnitude(enemyPos - playerTransform.position);
        float distPrince = Vector3.SqrMagnitude(enemyPos - princeTransform.position);

        // Logica semplice: attacca il più vicino
        return (distPlayer < distPrince) ? playerTransform : princeTransform;
    }
}