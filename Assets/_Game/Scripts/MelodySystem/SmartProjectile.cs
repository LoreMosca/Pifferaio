using UnityEngine;

public class SmartProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float turnSpeed = 20f;     // Aumentato da 10 a 20 per sterzate più secche
    public float hitThreshold = 1.5f; // Distanza di impatto garantito
    public float terminalDistance = 5f; // Sotto questa distanza, la guida diventa perfetta

    [Header("Debug Info")]
    [SerializeField] private string targetTag;
    [SerializeField] private Transform target;

    private SpellPayload payload;
    private bool isInitialized = false;
    private float moveSpeed;

    public void Initialize(SpellPayload data, float speed)
    {
        payload = data;
        moveSpeed = speed;
        isInitialized = true;

        // Logica Targeting
        if (payload.effect == SpellEffect.Heal || payload.effect == SpellEffect.Shield)
        {
            targetTag = "Principe";
        }
        else
        {
            targetTag = "Nemico";
        }

        FindTarget();
        Destroy(gameObject, 5f); // Safety timer
    }

    void FindTarget()
    {
        GameObject found = GameObject.FindGameObjectWithTag(targetTag);
        if (found != null) target = found.transform;
    }

    void Update()
    {
        if (!isInitialized) return;

        if (target != null)
        {
            float dist = Vector3.Distance(transform.position, target.position);

            // --- FASE 1: SPOLETTA DI PROSSIMITÀ ---
            // Se siamo vicinissimi, esplodi subito (evita il compenetramento)
            if (dist <= hitThreshold)
            {
                HitTarget(target.gameObject);
                return;
            }

            // --- FASE 2: MOVIMENTO ---
            Vector3 direction = (target.position - transform.position).normalized;

            if (dist < terminalDistance)
            {
                // GUIDA TERMINALE (Perfetta): Ignora la rotazione, vai dritto al punto
                // Questo impedisce l'orbita quando si è vicini
                transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
                transform.forward = direction; // Aggiorna rotazione visiva istantaneamente
            }
            else
            {
                // GUIDA NORMALE (Curva): Sterzata realistica
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
                transform.position += transform.forward * moveSpeed * Time.deltaTime;
            }
        }
        else
        {
            // Senza target va dritto
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;
        if (other.CompareTag(targetTag)) HitTarget(other.gameObject);
    }

    void HitTarget(GameObject hitObj)
    {
        if (!this.enabled) return;

        ApplyEffect(hitObj);
        this.enabled = false;
        Destroy(gameObject);
    }

    void ApplyEffect(GameObject hitObj)
    {
        string tName = hitObj.name;
        float val = payload.powerValue;

        switch (payload.effect)
        {
            case SpellEffect.Heal:
                Debug.Log($"<color=green>✚ HEAL</color> >> {tName} (+{val})");
                break;
            case SpellEffect.Shield:
                Debug.Log($"<color=yellow>🛡 SHIELD</color> >> {tName} (+{val})");
                break;
            case SpellEffect.Damage:
                Debug.Log($"<color=red>⚔ DAMAGE</color> >> {tName} (-{val})");
                break;
            case SpellEffect.Slow:
                Debug.Log($"<color=cyan>❄ SLOW</color> >> {tName} ({payload.duration}s)");
                break;
        }
    }
}