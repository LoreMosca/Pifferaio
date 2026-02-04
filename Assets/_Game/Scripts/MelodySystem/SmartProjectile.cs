using UnityEngine;

public class SmartProjectile : MonoBehaviour
{
    [Header("Settings Movimento")]
    [Tooltip("Velocità di rotazione per l'inseguimento.")]
    public float turnSpeed = 20f;
    [Tooltip("Distanza minima per esplosione garantita.")]
    public float hitThreshold = 1.5f;
    [Tooltip("Sotto questa distanza va dritto ignorando la curva.")]
    public float terminalDistance = 5f;

    private string targetTag;
    private Transform target;

    private SpellPayload payload;
    private bool isInitialized = false;
    private int currentPenetration;

    public void Initialize(SpellPayload data)
    {
        payload = data;
        currentPenetration = data.penetration; // Carica dal payload
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

        float speed = payload.moveSpeed; // Velocità corretta

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
        ApplyEffect(hitObj);

        // LOGICA PENETRAZIONE
        if (currentPenetration > 0)
        {
            currentPenetration--;
            Debug.Log($"<color=orange>TRAFITTO!</color> Nemici rimasti da colpire: {currentPenetration}");
            // Non distruggiamo
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void ApplyEffect(GameObject hitObj)
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