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
        if (other.CompareTag("Player") || other.isTrigger) return;

        // Evita di colpire due volte lo stesso nemico nello stesso swing
        if (hitTargets.Contains(other.gameObject)) return;

        // 1. APPLICA DANNO
        var target = other.GetComponent<IDamageable>();
        if (target == null) target = other.GetComponentInParent<IDamageable>();

        if (target != null)
        {
            target.TakeDamage(damageAmount);
            hitTargets.Add(other.gameObject);
        }

        // 2. APPLICA SPINTA (FISICA)
        // Cerchiamo il Rigidbody direttamente sul collider o sul padre
        Rigidbody rb = other.attachedRigidbody;

        // Applica forza solo se c'è un RB e non è cinematico
        if (rb != null && !rb.isKinematic && knockbackAmount > 0)
        {
            // Calcola direzione: Spinge nella direzione in cui sta andando l'attacco (transform.forward)
            // Appiattiamo la Y per evitare che i nemici volino in cielo come palloncini
            Vector3 pushDir = transform.forward;
            pushDir.y = 0;
            pushDir.Normalize();

            // Aggiunge un leggero vettore verso l'alto per "scollare" il nemico da terra (attrito)
            Vector3 finalForce = (pushDir + Vector3.up * 0.2f).normalized * knockbackAmount;

            rb.AddForce(finalForce, ForceMode.Impulse);

            Debug.Log($"Pushing {other.name} with force {knockbackAmount}");
        }
    }
}