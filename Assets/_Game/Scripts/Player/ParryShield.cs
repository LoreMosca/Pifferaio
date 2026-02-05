using UnityEngine;

public class ParryShield : MonoBehaviour
{
    [Header("--- BILANCIAMENTO DIFESA ---")]
    public float counterDamage = 80f;      // Danno alto per premiare il timing
    public float counterKnockback = 25f;   // Spinta molto forte
    public float blockKnockback = 5f;      // Spinta leggera se blocchi e basta

    [Header("--- VISUALS BOLLA ---")]
    [Tooltip("Colore durante la finestra di Parry Perfetto (es. Bianco/Ciano Luminoso)")]
    public Color parryColor = new Color(0.8f, 1f, 1f, 0.6f);

    [Tooltip("Colore quando la finestra scade e rimane solo il blocco (es. Arancio/Giallo Scuro)")]
    public Color blockColor = new Color(1f, 0.5f, 0f, 0.4f);

    [Tooltip("VFX quando pari perfettamente.")]
    public GameObject parrySuccessVFX;
    [Tooltip("VFX quando blocchi normalmente.")]
    public GameObject blockVFX;

    private PlayerController playerRef;
    private float creationTime;
    private float parryWindowDuration;
    private Renderer shieldRenderer;

    // Setup chiamato dal PlayerController
    public void Setup(PlayerController pc, float parryWindow)
    {
        playerRef = pc;
        parryWindowDuration = parryWindow;
        creationTime = Time.time;

        // Trova il renderer della bolla
        shieldRenderer = GetComponent<Renderer>();
        if (shieldRenderer == null) shieldRenderer = GetComponentInChildren<Renderer>();

        // Imposta colore iniziale
        UpdateShieldColor(0f);
    }

    void Update()
    {
        // Gestione continua del colore in base al tempo trascorso
        float age = Time.time - creationTime;
        UpdateShieldColor(age);
    }

    void UpdateShieldColor(float age)
    {
        if (shieldRenderer == null) return;

        Color target;
        if (age <= parryWindowDuration)
        {
            // Fase Parry: Colore Brillante
            target = parryColor;
        }
        else
        {
            // Fase Blocco: Colore Spento/Arancio
            // Lerp veloce per transizione fluida
            target = blockColor;
        }

        // Applica colore (preservando l'emissione se presente)
        shieldRenderer.material.color = Color.Lerp(shieldRenderer.material.color, target, Time.deltaTime * 10f);
        shieldRenderer.material.SetColor("_EmissionColor", target * 0.5f);
    }

    void OnTriggerEnter(Collider other)
    {
        // Ignora Player e trigger non ostili
        if (other.CompareTag("Player") || (!other.CompareTag("Nemico") && !other.CompareTag("EnemyAttack")))
            return;

        float age = Time.time - creationTime;

        if (age <= parryWindowDuration)
        {
            // --- PERFECT PARRY ---
            PerformCounter(other);
        }
        else
        {
            // --- NORMAL BLOCK ---
            PerformBlock(other);
        }
    }

    void PerformCounter(Collider enemy)
    {
        Debug.Log("<color=cyan>PERFECT PARRY!</color>");

        // 1. Danno al nemico
        IDamageable target = enemy.GetComponent<IDamageable>();
        if (target != null) target.TakeDamage(counterDamage);

        // 2. Knockback Esplosivo
        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            Vector3 dir = (enemy.transform.position - transform.position).normalized + Vector3.up * 0.5f;
            rb.AddForce(dir * counterKnockback, ForceMode.Impulse);
        }

        // 3. Feedback
        if (parrySuccessVFX) Instantiate(parrySuccessVFX, transform.position, Quaternion.identity);
        if (playerRef) playerRef.OnParrySuccess();
    }

    void PerformBlock(Collider enemy)
    {
        Debug.Log("<color=orange>BLOCKED</color>");

        if (enemy.CompareTag("EnemyAttack"))
        {
            Destroy(enemy.gameObject); // Distruggi proiettili
        }
        else
        {
            // Spinta leggera ai nemici fisici per non farli entrare nello scudo
            Rigidbody rb = enemy.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Vector3 dir = (enemy.transform.position - transform.position).normalized;
                dir.y = 0;
                rb.AddForce(dir * blockKnockback, ForceMode.Impulse);
            }
        }

        if (blockVFX) Instantiate(blockVFX, transform.position, Quaternion.identity);
        if (playerRef) playerRef.OnShieldHit();
    }
}