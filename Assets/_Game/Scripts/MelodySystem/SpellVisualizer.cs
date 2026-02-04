using UnityEngine;
using System.Collections;

public class SpellVisualizer : MonoBehaviour
{
    [Header("--- PREFAB GEOMETRICI (Feedback) ---")]
    [Tooltip("Prefab Sfera (con script SmartProjectile). Usato per magie dirette.")]
    public GameObject projectilePrefab;
    [Tooltip("Prefab Cilindro piatto. Usato per magie ad Area (AoE).")]
    public GameObject areaPrefab;
    [Tooltip("Prefab Cubo allungato. Usato per magie Laser (Beam).")]
    public GameObject beamPrefab;

    /// <summary>
    /// Metodo principale chiamato dal System per visualizzare la magia.
    /// </summary>
    /// <param name="payload">Dati della magia.</param>
    /// <param name="originPoint">Punto di origine (es. SpellOrigin).</param>
    public void VisualizeSpell(SpellPayload payload, Transform originPoint)
    {
        // Fallback di sicurezza
        if (originPoint == null) originPoint = transform;

        Color spellColor = GetColorFromEffect(payload.effect);

        foreach (Vector3 dir in payload.fireDirections)
        {
            // Calcola rotazione mondo basata sull'origine
            Vector3 worldDir = originPoint.TransformDirection(dir);
            Quaternion rotation = Quaternion.LookRotation(worldDir);

            switch (payload.delivery)
            {
                case SpellForm.Projectile:
                    // Usa Coroutine per gestire raffiche (es. 3 colpi veloci)
                    StartCoroutine(SpawnProjectileBurst(originPoint.position, rotation, spellColor, payload));
                    break;

                case SpellForm.AreaAoE:
                    // L'area spawna 3 metri avanti a terra
                    Vector3 spawnPos = originPoint.position + (worldDir * 3f);
                    spawnPos.y = 0.1f;
                    SpawnArea(spawnPos, spellColor, payload);
                    break;

                case SpellForm.LinearBeam:
                    SpawnBeam(originPoint, worldDir, spellColor, payload);
                    break;

                case SpellForm.SelfBuff:
                    ApplyBuffVisual(originPoint.root, spellColor, payload.duration);
                    break;
            }
        }
    }

    // --- LOGICA DI SPAWN ---

    IEnumerator SpawnProjectileBurst(Vector3 pos, Quaternion rot, Color c, SpellPayload p)
    {
        for (int i = 0; i < p.burstCount; i++)
        {
            GameObject obj = Instantiate(projectilePrefab, pos, rot);
            Colorize(obj, c);

            // Inizializza il cervello del proiettile
            SmartProjectile brain = obj.GetComponent<SmartProjectile>();
            if (brain == null) brain = obj.AddComponent<SmartProjectile>();
            brain.Initialize(p);

            yield return new WaitForSeconds(0.15f); // Ritardo tra i colpi
        }
    }

    void SpawnArea(Vector3 pos, Color c, SpellPayload p)
    {
        GameObject obj = Instantiate(areaPrefab, pos, Quaternion.identity);
        Colorize(obj, c);
        // Scala l'area in base al range
        obj.transform.localScale = new Vector3(p.sizeOrRange, 0.1f, p.sizeOrRange);
        Destroy(obj, p.duration);
    }

    void SpawnBeam(Transform origin, Vector3 dir, Color c, SpellPayload p)
    {
        // Il beam diventa figlio dell'origine per muoversi col player
        GameObject obj = Instantiate(beamPrefab, origin.position, Quaternion.LookRotation(dir));
        obj.transform.SetParent(origin);
        Colorize(obj, c);

        // Scala e posiziona per farlo partire davanti
        obj.transform.localScale = new Vector3(0.3f, 0.3f, p.sizeOrRange);
        obj.transform.localPosition += Vector3.forward * (p.sizeOrRange / 2f);

        Destroy(obj, p.duration);
    }

    void ApplyBuffVisual(Transform targetRoot, Color c, float duration)
    {
        // Semplice aura ai piedi
        Vector3 pos = targetRoot.position;
        pos.y = 0.1f;
        GameObject aura = Instantiate(areaPrefab, pos, Quaternion.identity);
        aura.transform.SetParent(targetRoot);
        Colorize(aura, new Color(c.r, c.g, c.b, 0.3f)); // Trasparente
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
            case SpellEffect.Slow: return Color.cyan;
            case SpellEffect.Shield: return Color.yellow;
            default: return Color.white;
        }
    }
}