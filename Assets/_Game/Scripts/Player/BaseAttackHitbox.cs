using UnityEngine;
using System.Collections.Generic;

public class BasicAttackHitbox : MonoBehaviour
{
    private float damageAmount;
    private float knockbackAmount; // Forza della spinta
    private List<GameObject> hitTargets = new List<GameObject>();

    // Setup aggiornato: ora accetta anche il knockback
    public void Setup(float damage, float knockback)
    {
        this.damageAmount = damage;
        this.knockbackAmount = knockback;
        hitTargets.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        // 1. PROTEZIONE FUOCO AMICO: Ignora sempre Player e Principe
        if (other.CompareTag("Player") || other.CompareTag("Principe")) return;

        // 2. CERCA COMPONENTE VITA (IDamageable)
        // Lo cerchiamo SUBITO, prima di decidere se ignorare l'oggetto
        var target = other.GetComponent<IDamageable>();
        if (target == null) target = other.GetComponentInParent<IDamageable>();

        // 3. FILTRO TRIGGER:
        // Se l'oggetto NON ha vita (target == null) ED è un trigger (other.isTrigger),
        // allora è probabilmente un'area di vista dei nemici -> Ignoralo.
        // MA: Se 'target' esiste (es. la Porta), entriamo nell'if successivo anche se è un trigger.
        if (target == null && other.isTrigger) return;

        // 4. EVITA DOPPI COLPI
        if (hitTargets.Contains(other.gameObject)) return;

        // 5. APPLICA DANNO
        if (target != null)
        {
            target.TakeDamage(damageAmount);
            hitTargets.Add(other.gameObject);

            // Debug per capire se colpisce la porta
            Debug.Log($"Colpito: {other.name} per {damageAmount} danni.");
        }

        // 6. APPLICA SPINTA (FISICA)
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && !rb.isKinematic && knockbackAmount > 0)
        {
            Vector3 pushDir = transform.forward;
            pushDir.y = 0;
            pushDir.Normalize();
            rb.AddForce((pushDir + Vector3.up * 0.2f) * knockbackAmount, ForceMode.Impulse);
        }
    }
}