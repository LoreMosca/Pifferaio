using UnityEngine;

public class SmartProjectile : MonoBehaviour
{
    [Header("Settings Movimento")]
    public float turnSpeed = 20f;
    public float hitThreshold = 1.5f;
    public float terminalDistance = 5f;

    private string targetTag;
    private Transform target;

    private SpellPayload payload;
    private bool isInitialized = false;
    private int currentPenetration;

    public void Initialize(SpellPayload data)
    {
        payload = data;
        currentPenetration = data.penetration;
        isInitialized = true;

        if (payload.effect == SpellEffect.Heal || payload.effect == SpellEffect.Shield)
            targetTag = "Principe";
        else
            targetTag = "Nemico";

        FindTarget();
        Destroy(gameObject, 5f);
    }

    void FindTarget()
    {
        GameObject found = GameObject.FindGameObjectWithTag(targetTag);
        if (found != null) target = found.transform;
    }

    void Update()
    {
        if (!isInitialized) return;

        float speed = payload.moveSpeed;

        if (target != null)
        {
            float dist = Vector3.Distance(transform.position, target.position);

            if (dist <= hitThreshold)
            {
                HitTarget(target.gameObject);
                return;
            }

            Vector3 direction = (target.position - transform.position).normalized;

            if (dist < terminalDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
                transform.forward = direction;
            }
            else
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);
                transform.position += transform.forward * speed * Time.deltaTime;
            }
        }
        else
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;
        if (other.CompareTag(targetTag)) HitTarget(other.gameObject);
    }

    void HitTarget(GameObject hitObj)
    {
        IDamageable target = hitObj.GetComponent<IDamageable>();
        if (target != null)
        {
            switch (payload.effect)
            {
                case SpellEffect.Damage: target.TakeDamage(payload.powerValue); break;
                case SpellEffect.Heal: target.Heal(payload.powerValue); break;
                case SpellEffect.Shield: target.AddShield(payload.powerValue); break;
                case SpellEffect.Slow: target.ApplySlow(20f, 3f); break;
            }
        }

        // --- KNOCKBACK AGGIUNTO ---
        Rigidbody rb = hitObj.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic && payload.knockback > 0)
        {
            // Spinge nella direzione del proiettile
            Vector3 pushDir = transform.forward;
            pushDir.y = 0.2f;
            rb.AddForce(pushDir.normalized * payload.knockback, ForceMode.Impulse);
        }
        // -------------------------

        ApplyEffectLog(hitObj);

        if (currentPenetration > 0) { currentPenetration--; }
        else { Destroy(gameObject); }
    }

    void ApplyEffectLog(GameObject hitObj)
    {
        string tName = hitObj.name;
        float val = payload.powerValue;
        switch (payload.effect)
        {
            case SpellEffect.Heal: Debug.Log($"<color=green>✚ HEAL</color> {tName} (+{val:F1})"); break;
            case SpellEffect.Damage: Debug.Log($"<color=red>⚔ DAMAGE</color> {tName} (-{val:F1})"); break;
            case SpellEffect.Shield: Debug.Log($"<color=yellow>🛡 SHIELD</color> {tName} (+{val:F1})"); break;
            case SpellEffect.Slow: Debug.Log($"<color=cyan>❄ SLOW</color> {tName}"); break;
        }
    }
}