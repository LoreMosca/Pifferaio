using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(DummyController))]
public class RatAI : MonoBehaviour
{
    [Header("--- RIFERIMENTI TARGET ---")]
    private Transform currentTarget;
    private Transform princeTransform;
    private Transform playerTransform;

    [Header("--- CONFIGURAZIONE BASE ---")]
    [Tooltip("Danno base del nemico (Livello 1).")]
    public float baseDamage = 5f;
    [Tooltip("Velocità base del nemico (Livello 1).")]
    public float baseSpeed = 3.5f;

    [Header("--- COMBATTIMENTO ---")]
    public float attackRange = 1.5f;
    public float attackRate = 1.0f;
    public float retargetRate = 0.5f; // Ricalcola il target ogni 0.5s

    // Variabili Runtime
    private float currentDamage;
    private NavMeshAgent agent;
    private DummyController stats;

    private float nextAttackTime = 0f;
    private float nextRetargetTime = 0f;

    // USA AWAKE: Fondamentale per inizializzare l'Agent PRIMA che lo spawner chiami SetStats
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<DummyController>();
    }

    void Start()
    {
        // Trova i bersagli nella scena
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTransform = p.transform;

        GameObject pr = GameObject.FindGameObjectWithTag("Principe");
        if (pr) princeTransform = pr.transform;

        // Inizializza valori se non sono stati settati dall'esterno
        if (currentDamage == 0) currentDamage = baseDamage;
        if (agent) agent.speed = baseSpeed;
    }

    // --- METODO CHIAMATO DALLO SPAWNER ---
    public void SetStats(float damageMultiplier, float speedMultiplier)
    {
        // Calcola danno finale
        currentDamage = baseDamage * damageMultiplier;

        // Calcola velocità finale
        // Controllo di sicurezza se chiamato prima di Start/Awake (anche se Awake dovrebbe prevenire)
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = baseSpeed * speedMultiplier;
            // Aumentiamo leggermente l'accelerazione per ratti più veloci
            agent.acceleration = 8f * speedMultiplier;
        }
    }

    void Update()
    {
        // Se morto, ferma tutto
        if (stats.currentHealth <= 0)
        {
            if (agent.enabled) agent.isStopped = true;
            return;
        }

        // Gestione Freeze / Slow derivante dal DummyController
        if (stats.isFrozen)
        {
            if (agent.enabled) agent.isStopped = true;
            return;
        }
        else
        {
            if (agent.enabled) agent.isStopped = false;

            // Applica la percentuale di rallentamento alla velocità attuale
            float speedMod = 1.0f - (stats.currentSlowPercent / 100f);
            agent.speed = (baseSpeed * speedMod); // Nota: qui potremmo dover risalvare il moltiplicatore base se cambia dinamicamente
        }

        // Logic Targeting Intelligente
        if (Time.time >= nextRetargetTime)
        {
            SelectClosestTarget();
            nextRetargetTime = Time.time + retargetRate;
        }

        // Se non ho target, non faccio nulla
        if (currentTarget == null) return;

        // Movimento
        agent.SetDestination(currentTarget.position);

        // Attacco
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange && Time.time >= nextAttackTime)
        {
            Attack();
        }
    }

    void SelectClosestTarget()
    {
        float distPlayer = (playerTransform != null) ? Vector3.Distance(transform.position, playerTransform.position) : float.MaxValue;
        float distPrince = (princeTransform != null) ? Vector3.Distance(transform.position, princeTransform.position) : float.MaxValue;

        // Insegue il più vicino
        if (distPlayer < distPrince) currentTarget = playerTransform;
        else currentTarget = princeTransform;
    }

    void Attack()
    {
        nextAttackTime = Time.time + (1f / attackRate);

        // Ruota verso il bersaglio per effetto visivo
        Vector3 lookPos = currentTarget.position - transform.position;
        lookPos.y = 0;
        if (lookPos != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookPos);

        // Applica danno
        IDamageable targetHealth = currentTarget.GetComponent<IDamageable>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(currentDamage);
        }
    }
}