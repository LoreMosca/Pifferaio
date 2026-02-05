using UnityEngine;

public class SpellBuffEffect : MonoBehaviour
{
    private SpellPayload payload;
    private PlayerStats targetStats;
    private float tickTimer = 0f;
    private bool isInitialized = false;

    // Visuals
    private float rotateSpeed = 50f;

    public void Initialize(Transform target, SpellPayload data, Color color)
    {
        payload = data;

        targetStats = target.GetComponent<PlayerStats>();
        if (targetStats == null) targetStats = target.GetComponentInParent<PlayerStats>();

        // Si attacca al target
        transform.SetParent(target);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Visual
        var rend = GetComponent<Renderer>();
        if (rend)
        {
            rend.material.color = new Color(color.r, color.g, color.b, 0.4f);
        }

        Debug.Log($"<color=yellow>BUFF ACTIVATED:</color> {data.effect}");

        isInitialized = true;
        Destroy(gameObject, data.duration);
    }

    void Update()
    {
        if (!isInitialized) return;

        // Ruota l'aura
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);

        // Tick
        tickTimer += Time.deltaTime;
        float interval = (payload.tickRate > 0) ? (1f / payload.tickRate) : 99f;

        if (tickTimer >= interval)
        {
            ApplyBuffTick();
            tickTimer = 0f;
        }
    }

    void ApplyBuffTick()
    {
        if (targetStats != null)
        {
            switch (payload.effect)
            {
                case SpellEffect.Heal: targetStats.Heal(payload.powerValue); break;
                // Qui puoi aggiungere logica per stat boost se PlayerStats supporta
                case SpellEffect.Shield: targetStats.AddShield(payload.powerValue); break;
            }
        }
    }
}