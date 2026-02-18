using UnityEngine;

public class SpellBuffEffect : MonoBehaviour
{
    private SpellPayload payload;
    private PlayerStats targetStats;
    private float tickTimer = 0f;
    private bool isInitialized = false;

    // Visuals
    private float age = 0f;
    private float duration;
    private Vector3 initialScale;
    private float rotateSpeed = 50f;

    public void Initialize(Transform target, SpellPayload data, Color color)
    {
        payload = data;
        duration = data.duration;
        initialScale = transform.localScale;

        targetStats = target.GetComponent<PlayerStats>();
        if (targetStats == null) targetStats = target.GetComponentInParent<PlayerStats>();

        transform.SetParent(target);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        SetVFXColor(color);

        // 1. APPLICA STATS FISSE
        if (targetStats)
        {
            if (payload.effect == SpellEffect.SpeedUp)
                targetStats.speedMultiplier += (payload.powerValue / 100f);

            if (payload.effect == SpellEffect.DamageUp)
                targetStats.damageMultiplier += (payload.powerValue / 100f);
        }

        // 2. FEEDBACK FORTUNA (LOGICA TIERED)
        if (targetStats && payload.lootLuckChance > 0f)
        {
            if (payload.lootLuckChance >= 0.25f)
            {
                // Tier Alto: Oro e Testo Epico
                targetStats.SpawnPopup("MAX LUCK!", new Color(1f, 0.84f, 0f));
            }
            else
            {
                // Tier Basso: Giallo e Testo Normale
                targetStats.SpawnPopup("LUCK UP!", Color.yellow);
            }
        }

        // 3. MOSTRA ICONA UI
        if (BuffManager.Instance)
        {
            BuffManager.Instance.AddBuff(payload.effect, duration);
        }

        isInitialized = true;
    }

    void OnDestroy()
    {
        if (targetStats && isInitialized)
        {
            if (payload.effect == SpellEffect.SpeedUp) targetStats.speedMultiplier -= (payload.powerValue / 100f);
            if (payload.effect == SpellEffect.DamageUp) targetStats.damageMultiplier -= (payload.powerValue / 100f);
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        age += Time.deltaTime;
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);

        // Panic Mode VFX (ultimi 3s)
        if (duration - age <= 3.0f)
        {
            float pulse = Mathf.PingPong(Time.time * 15f, 0.3f);
            transform.localScale = initialScale * (1f + pulse);
        }

        // TICK LOGIC
        tickTimer += Time.deltaTime;
        float interval = (payload.tickRate > 0) ? (1f / payload.tickRate) : 99f;

        if (tickTimer >= interval)
        {
            ApplyBuffTick();
            tickTimer = 0f;
        }

        if (age >= duration) Destroy(gameObject);
    }

    void ApplyBuffTick()
    {
        if (targetStats != null)
        {
            switch (payload.effect)
            {
                case SpellEffect.Heal: targetStats.Heal(payload.powerValue); break;
                case SpellEffect.Shield: targetStats.AddShield(payload.powerValue); break;
            }
        }
    }

    void SetVFXColor(Color color)
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = new Color(color.r, color.g, color.b, 1f);
            foreach (ParticleSystem childPs in GetComponentsInChildren<ParticleSystem>())
                if (childPs != ps) { var m = childPs.main; m.startColor = new Color(color.r, color.g, color.b, 1f); }
        }
        else
        {
            var rend = GetComponent<Renderer>();
            if (rend && !(rend is ParticleSystemRenderer))
                rend.material.color = new Color(color.r, color.g, color.b, 0.4f);
        }
    }
}