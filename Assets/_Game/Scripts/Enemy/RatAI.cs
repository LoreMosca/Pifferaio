using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(DummyController))]
public class RatAI : MonoBehaviour
{
    [Header("--- ANIMAZIONI ---")]
    public Animator animator; // ASSEGNA QUESTO NELL'INSPECTOR

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
    public float retargetRate = 0.5f;

    // Variabili Runtime
    private float currentDamage;
    private NavMeshAgent agent;
    private DummyController stats;

    private float nextAttackTime = 0f;
    private float nextRetargetTime = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<DummyController>();
        // Se l'animator è sul figlio (il modello 3D), prova a trovarlo se non assegnato
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerTransform = p.transform;

        GameObject pr = GameObject.FindGameObjectWithTag("Principe");
        if (pr) princeTransform = pr.transform;

        if (currentDamage == 0) currentDamage = baseDamage;
        if (agent) agent.speed = baseSpeed;
    }

    public void SetStats(float damageMultiplier, float speedMultiplier)
    {
        currentDamage = baseDamage * damageMultiplier;
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = baseSpeed * speedMultiplier;
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

        // Gestione Animazione Movimento
        if (animator != null && agent.enabled)
        {
            // Passiamo la velocità corrente all'animator per passare da Idle a Run
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }

        // Gestione Freeze / Slow
        if (stats.isFrozen)
        {
            if (agent.enabled) agent.isStopped = true;
            if (animator) animator.speed = 0; // Ferma l'animazione se congelato
            return;
        }
        else
        {
            if (agent.enabled) agent.isStopped = false;
            if (animator) animator.speed = 1; // Riprendi animazione

            float speedMod = 1.0f - (stats.currentSlowPercent / 100f);
            agent.speed = (baseSpeed * speedMod);
        }

        // Logic Targeting
        if (Time.time >= nextRetargetTime)
        {
            SelectClosestTarget();
            nextRetargetTime = Time.time + retargetRate;
        }

        if (currentTarget == null) return;

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

        if (distPlayer < distPrince) currentTarget = playerTransform;
        else currentTarget = princeTransform;
    }

    void Attack()
    {
        nextAttackTime = Time.time + (1f / attackRate);

        Vector3 lookPos = currentTarget.position - transform.position;
        lookPos.y = 0;
        if (lookPos != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookPos);

        // Trigger Animazione
        if (animator != null) animator.SetTrigger("Attack");

        // Danno (Sincronizzato grossolanamente, per perfezionarlo servirebbero Animation Events)
        IDamageable targetHealth = currentTarget.GetComponent<IDamageable>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(currentDamage);
        }
    }
}