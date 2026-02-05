using UnityEngine;
using System.Collections.Generic; // Necessario per Dictionary

[RequireComponent(typeof(BoxCollider))]
public class JuicyBeam : MonoBehaviour
{
    [Header("Visual Juice")]
    public float scrollSpeedX = -2.0f;
    public float pulseSpeed = 10f;
    public float pulseAmount = 0.1f;
    public float fadeDuration = 0.1f;
    public float coreIntensity = 3.0f;

    [Header("Logic")]
    public LayerMask hitLayers;
    public float visualPenetration = 0.5f;

    [Header("Ramp Up (Crescita Esponenziale)")]
    [Tooltip("Velocità di crescita del danno se si mantiene il raggio sullo STESSO bersaglio.")]
    public float rampUpSpeed = 0.5f;

    private Renderer rend;
    private Color targetColor;
    private float lifetime;
    private float age;
    private PlayerController ownerPlayer;
    private SpellPayload payload;
    private float tickTimer = 0f;
    private bool isInitialized = false;

    private float currentLength;

    // TRACKING BERSAGLI: ID Oggetto -> Numero di Hit Consecutivi
    private Dictionary<int, int> targetConsecutiveHits = new Dictionary<int, int>();

    void Awake()
    {
        rend = GetComponent<Renderer>();
        GetComponent<BoxCollider>().isTrigger = true;
    }

    public void Initialize(PlayerController owner, SpellPayload data, Color color)
    {
        ownerPlayer = owner;
        payload = data;
        lifetime = data.duration;
        targetColor = color;
        currentLength = data.sizeOrRange;
        age = 0f;
        isInitialized = true;

        targetConsecutiveHits.Clear(); // Pulisce la memoria

        if (rend)
        {
            Color c = color * coreIntensity; c.a = 0f;
            rend.material.SetColor("_Color", c);
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", c);
        }

        if (ownerPlayer != null) ownerPlayer.SetChanneling(true);
        Destroy(gameObject, lifetime);
    }

    void OnDestroy() { if (ownerPlayer != null) ownerPlayer.SetChanneling(false); }

    void Update()
    {
        if (!isInitialized) return;
        age += Time.deltaTime;

        CalculateCollision();
        UpdateTransform();
        HandleVisuals();
        HandleTickLogic();
    }

    void HandleTickLogic()
    {
        tickTimer += Time.deltaTime;
        float interval = (payload.tickRate > 0) ? (1f / payload.tickRate) : 99f;

        if (tickTimer >= interval)
        {
            ApplyEffectArea(interval);
            tickTimer = 0f;
        }
    }

    void ApplyEffectArea(float tickInterval)
    {
        // 1. Rileva collisioni attuali
        Collider[] hits = Physics.OverlapBox(transform.position, transform.localScale / 2, transform.rotation, hitLayers);

        // Ordina per distanza (per la logica del Damage Decay sui nemici in fila)
        Vector3 origin = (ownerPlayer != null) ? ownerPlayer.transform.position : transform.position;
        System.Array.Sort(hits, (a, b) => Vector3.Distance(origin, a.transform.position).CompareTo(Vector3.Distance(origin, b.transform.position)));

        // 2. Set per tracciare chi abbiamo colpito in QUESTO tick
        HashSet<int> currentHitIds = new HashSet<int>();

        int hitCount = 0; // Contatore per il Damage Decay (Estensione Rossa)

        foreach (var hit in hits)
        {
            bool isEnemy = hit.CompareTag("Nemico");
            bool isPrince = hit.CompareTag("Principe");

            if (isEnemy || isPrince)
            {
                // A. Identificazione e Tracking
                GameObject targetObj = hit.gameObject;
                int targetID = targetObj.GetInstanceID();
                currentHitIds.Add(targetID);

                // Incrementa contatore hit consecutivi per questo specifico nemico
                if (!targetConsecutiveHits.ContainsKey(targetID)) targetConsecutiveHits[targetID] = 0;
                targetConsecutiveHits[targetID]++;

                // B. Calcolo Moltiplicatore Esponenziale (Ramp Up)
                // Tempo = numero tick * durata tick
                float timeOnTarget = targetConsecutiveHits[targetID] * tickInterval;
                float timeMult = 1.0f + (timeOnTarget * timeOnTarget * rampUpSpeed);

                // C. Calcolo Decadimento Distanza (Damage Decay)
                hitCount++;
                float distDecay = Mathf.Clamp01(1.0f - ((hitCount - 1) * payload.damageDecay));

                // D. Potenza Finale
                float finalPower = payload.powerValue * distDecay * timeMult;

                // E. Applica Effetto
                IDamageable target = hit.GetComponent<IDamageable>();
                if (target != null)
                {
                    switch (payload.effect)
                    {
                        case SpellEffect.Damage: target.TakeDamage(finalPower); break;
                        case SpellEffect.Heal: target.Heal(finalPower); break;
                        case SpellEffect.Shield: target.AddShield(finalPower); break;
                        case SpellEffect.Slow: target.ApplySlow(10f, 0.2f); break;
                    }
                }
            }
        }

        // 3. LOGICA DI RESET (Cruciale!)
        // Se un nemico era nella lista ma NON è stato colpito in questo tick, resettalo.
        List<int> idsToRemove = new List<int>();
        foreach (var id in targetConsecutiveHits.Keys)
        {
            if (!currentHitIds.Contains(id))
            {
                idsToRemove.Add(id);
            }
        }

        foreach (var id in idsToRemove)
        {
            targetConsecutiveHits.Remove(id);
            // Debug.Log($"Reset combo su Target ID: {id}");
        }
    }

    // --- VISUAL & COLLISION (Invariati) ---
    void CalculateCollision()
    {
        currentLength = payload.sizeOrRange;
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, payload.sizeOrRange, hitLayers);
        float nearestWall = payload.sizeOrRange;
        bool foundWall = false;
        foreach (var hit in hits)
        {
            if (!hit.collider.isTrigger && !hit.collider.CompareTag("Nemico") && !hit.collider.CompareTag("Player") && !hit.collider.CompareTag("Principe"))
            {
                if (hit.distance < nearestWall) { nearestWall = hit.distance; foundWall = true; }
            }
        }
        if (foundWall) currentLength = nearestWall + visualPenetration;
    }
    void UpdateTransform()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = new Vector3(0.5f * pulse, 0.5f * pulse, currentLength);
        transform.localPosition = new Vector3(0, 0, currentLength / 2f);
    }
    void HandleVisuals()
    {
        if (rend) rend.material.mainTextureOffset = new Vector2(Time.time * scrollSpeedX, 0);
        float alpha = 1f;
        if (age < fadeDuration) alpha = age / fadeDuration;
        else if (age > lifetime - fadeDuration) alpha = Mathf.Clamp01((lifetime - age) / fadeDuration);
        if (rend) { Color c = targetColor * coreIntensity; c.a = alpha; rend.material.SetColor("_Color", c); rend.material.SetColor("_EmissionColor", c * alpha); }
    }
}