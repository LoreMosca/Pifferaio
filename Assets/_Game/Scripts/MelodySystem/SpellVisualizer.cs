using UnityEngine;

public class SpellVisualizer : MonoBehaviour
{
    [Header("Primitive Prefabs")]
    public GameObject projectilePrefab; // Sfera
    public GameObject areaPrefab;       // Cilindro piatto
    public GameObject beamPrefab;       // Cubo lungo

    [Header("Settings")]
    public float projectileSpeed = 20f;

    public void VisualizeSpell(SpellPayload payload, Transform caster)
    {
        // Determiniamo il colore in base all'EFFETTO (Radice)
        Color spellColor = GetColorFromEffect(payload.effect);

        foreach (Vector3 dir in payload.fireDirections)
        {
            Vector3 worldDir = caster.TransformDirection(dir);
            Quaternion rotation = Quaternion.LookRotation(worldDir);

            switch (payload.delivery)
            {
                case SpellForm.Projectile:
                    SpawnProjectile(caster.position, rotation, spellColor, payload);
                    break;

                case SpellForm.AreaAoE:
                    Vector3 spawnPos = caster.position + (worldDir * 3f);
                    SpawnArea(spawnPos, spellColor, payload);
                    break;

                case SpellForm.LinearBeam:
                    SpawnBeam(caster, worldDir, spellColor, payload);
                    break;

                case SpellForm.SelfBuff:
                    // <--- FIX: Ora passiamo la durata corretta
                    ApplyBuffVisual(caster, spellColor, payload.duration);
                    break;
            }
        }
    }

    void SpawnProjectile(Vector3 pos, Quaternion rot, Color c, SpellPayload p)
    {
        GameObject obj = Instantiate(projectilePrefab, pos, rot);
        Colorize(obj, c);
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        // NOTA: Se usi una versione vecchia di Unity 6 (beta) o Unity 2022/2023, 
        // cambia 'linearVelocity' in 'velocity' per risolvere l'errore.
        if (rb) rb.linearVelocity = obj.transform.forward * projectileSpeed;

        Destroy(obj, 3f);
    }

    void SpawnArea(Vector3 pos, Color c, SpellPayload p)
    {
        GameObject obj = Instantiate(areaPrefab, pos, Quaternion.identity);
        Colorize(obj, c);
        obj.transform.localScale = new Vector3(p.sizeOrRange, 0.1f, p.sizeOrRange);
        Destroy(obj, p.duration);
    }

    void SpawnBeam(Transform caster, Vector3 dir, Color c, SpellPayload p)
    {
        GameObject obj = Instantiate(beamPrefab, caster.position, Quaternion.LookRotation(dir));
        obj.transform.SetParent(caster);
        Colorize(obj, c);
        obj.transform.localScale = new Vector3(0.5f, 0.5f, p.sizeOrRange);
        obj.transform.localPosition += dir * (p.sizeOrRange / 2f);
        Destroy(obj, p.duration);
    }

    // <--- FIX: Aggiunto parametro 'duration'
    void ApplyBuffVisual(Transform target, Color c, float duration)
    {
        GameObject aura = Instantiate(areaPrefab, target.position, Quaternion.identity);
        aura.transform.SetParent(target);
        aura.transform.localScale = new Vector3(2f, 0.05f, 2f);

        // Rendiamo semitrasparente
        Color alphaColor = new Color(c.r, c.g, c.b, 0.3f);
        Colorize(aura, alphaColor);

        Destroy(aura, duration);
    }

    void Colorize(GameObject obj, Color c)
    {
        var rend = obj.GetComponent<Renderer>();
        if (rend) rend.material.color = c;
    }

    Color GetColorFromEffect(SpellEffect effect)
    {
        switch (effect)
        {
            case SpellEffect.Damage: return Color.red;
            case SpellEffect.Heal: return Color.green;
            case SpellEffect.Slow: return Color.blue;
            case SpellEffect.Shield: return Color.yellow;
            default: return Color.white;
        }
    }
}