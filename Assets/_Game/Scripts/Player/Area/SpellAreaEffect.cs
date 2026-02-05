using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SphereCollider))]
public class SpellAreaEffect : MonoBehaviour
{
    private SpellPayload payload;
    private Color areaColor;
    private float age = 0f;
    private float tickTimer = 0f;
    private bool isInitialized = false;

    // Visuals
    private float maxRadius;
    private Renderer rend;

    // CONFIGURAZIONE VISIVA
    private float baseAlpha = 0.6f; // Molto più visibile di prima (0.3)
    private float pulseAlpha = 0.9f; // Quasi solido durante il tick
    private float emissionIntensity = 2.0f; // Quanto brilla

    public void Initialize(SpellPayload data, Color color)
    {
        payload = data;
        areaColor = color;
        maxRadius = data.sizeOrRange;

        // Setup Grafico
        transform.localScale = Vector3.one * 0.1f; // Parte piccolo

        rend = GetComponent<Renderer>();
        if (rend)
        {
            // 1. Imposta Colore Base con Alpha più alto
            Color baseC = new Color(color.r, color.g, color.b, baseAlpha);
            rend.material.color = baseC;

            // 2. ATTIVA EMISSIONE (Fondamentale per vederlo bene)
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", color * emissionIntensity);
        }

        // Setup Collider
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        isInitialized = true;
        Destroy(gameObject, data.duration);
    }

    void Update()
    {
        if (!isInitialized) return;

        age += Time.deltaTime;

        // 1. Animazione Espansione
        float progress = Mathf.Clamp01(age / 0.5f);
        float currentScale = Mathf.Lerp(0.1f, maxRadius * 2f, progress);
        transform.localScale = new Vector3(currentScale, 0.1f, currentScale);

        // 2. Logica Tick
        tickTimer += Time.deltaTime;
        float interval = (payload.tickRate > 0) ? (1f / payload.tickRate) : 99f;

        if (tickTimer >= interval)
        {
            DoAreaTick();
            tickTimer = 0f;
        }
    }

    void DoAreaTick()
    {
        float currentRadius = transform.localScale.x * 0.5f;
        Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius);

        foreach (var hit in hits)
        {
            bool isTarget = hit.CompareTag("Nemico") || hit.CompareTag("Principe");
            if (!isTarget) continue;

            IDamageable dmg = hit.GetComponent<IDamageable>();
            if (dmg != null)
            {
                switch (payload.effect)
                {
                    case SpellEffect.Damage: dmg.TakeDamage(payload.powerValue); break;
                    case SpellEffect.Heal: dmg.Heal(payload.powerValue); break;
                    case SpellEffect.Shield: dmg.AddShield(payload.powerValue); break;
                    case SpellEffect.Slow: dmg.ApplySlow(30f, 1.5f); break;
                }
            }

            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic && payload.knockback > 0)
            {
                Vector3 pushDir = (hit.transform.position - transform.position).normalized;
                pushDir.y = 0.1f;
                rb.AddForce(pushDir * payload.knockback * 10f, ForceMode.Impulse);
            }
        }

        StartCoroutine(PulseVisual());
    }

    IEnumerator PulseVisual()
    {
        if (rend)
        {
            // Pulsazione: Diventa quasi solido (0.9) e aumenta la luminosità
            Color baseC = new Color(areaColor.r, areaColor.g, areaColor.b, baseAlpha);
            Color pulseC = new Color(areaColor.r, areaColor.g, areaColor.b, pulseAlpha);

            rend.material.color = pulseC;
            rend.material.SetColor("_EmissionColor", areaColor * (emissionIntensity * 1.5f)); // Flash luminoso

            yield return new WaitForSeconds(0.15f);

            // Ritorna normale
            rend.material.color = baseC;
            rend.material.SetColor("_EmissionColor", areaColor * emissionIntensity);
        }
    }
}