using UnityEngine;
using System.Collections;

public class SpellVisualizer : MonoBehaviour
{
    public GameObject projectilePrefab;
    public GameObject areaPrefab; // Assicurati che abbia Renderer e SphereCollider
    public GameObject beamPrefab;

    [Header("--- CAST POINTS ---")]
    public Transform castPointForward;
    public Transform castPointBack;
    public Transform castPointLeft;
    public Transform castPointRight;

    public void VisualizeSpell(SpellPayload payload, Transform originPoint)
    {
        if (originPoint == null) originPoint = transform;
        Color spellColor = GetColorFromEffect(payload.effect);

        // --- GESTIONE BUFF ---
        if (payload.delivery == SpellForm.SelfBuff)
        {
            ApplyBuffLogic(originPoint.root, spellColor, payload);
            return;
        }

        foreach (Vector3 dir in payload.fireDirections)
        {
            Transform spawnPoint = GetDirectionalCastPoint(dir, originPoint);

            switch (payload.delivery)
            {
                case SpellForm.Projectile:
                    StartCoroutine(SpawnProjectileBurst(spawnPoint.position, spawnPoint.rotation, spellColor, payload));
                    break;

                case SpellForm.AreaAoE:
                    Vector3 areaPos = spawnPoint.position + (spawnPoint.forward * 4f);
                    areaPos.y = 0.1f;
                    SpawnAreaLogic(areaPos, spellColor, payload);
                    break;

                case SpellForm.LinearBeam:
                    SpawnBeam(spawnPoint, spellColor, payload, originPoint);
                    break;
            }
        }
    }

    // --- METODI DI SPAWN LOGICA ---

    void SpawnAreaLogic(Vector3 pos, Color c, SpellPayload p)
    {
        GameObject obj = Instantiate(areaPrefab, pos, Quaternion.identity);

        // Collega Script Logica Area
        SpellAreaEffect areaScript = obj.GetComponent<SpellAreaEffect>();
        if (areaScript == null) areaScript = obj.AddComponent<SpellAreaEffect>();

        areaScript.Initialize(p, c);
    }

    void ApplyBuffLogic(Transform target, Color c, SpellPayload p)
    {
        // Usa areaPrefab come aura temporanea
        GameObject obj = Instantiate(areaPrefab, target.position, Quaternion.identity);

        // Collega Script Logica Buff
        SpellBuffEffect buffScript = obj.GetComponent<SpellBuffEffect>();
        if (buffScript == null) buffScript = obj.AddComponent<SpellBuffEffect>();

        buffScript.Initialize(target, p, c);
    }

    void SpawnBeam(Transform parentPoint, Color c, SpellPayload p, Transform owner)
    {
        GameObject obj = Instantiate(beamPrefab, parentPoint.position, parentPoint.rotation);
        obj.transform.SetParent(parentPoint);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        JuicyBeam juice = obj.GetComponent<JuicyBeam>();
        if (juice == null) juice = obj.AddComponent<JuicyBeam>();

        // Trova il PlayerController per il channeling
        PlayerController pc = owner.GetComponentInParent<PlayerController>();
        if (pc == null) pc = owner.GetComponent<PlayerController>();

        juice.Initialize(pc, p, c);
    }

    Transform GetDirectionalCastPoint(Vector3 dir, Transform defaultOrigin)
    {
        if (Vector3.Dot(dir, Vector3.forward) > 0.9f) return castPointForward ? castPointForward : defaultOrigin;
        if (Vector3.Dot(dir, Vector3.back) > 0.9f) return castPointBack ? castPointBack : defaultOrigin;
        if (Vector3.Dot(dir, Vector3.left) > 0.9f) return castPointLeft ? castPointLeft : defaultOrigin;
        if (Vector3.Dot(dir, Vector3.right) > 0.9f) return castPointRight ? castPointRight : defaultOrigin;
        return defaultOrigin;
    }

    IEnumerator SpawnProjectileBurst(Vector3 pos, Quaternion rot, Color c, SpellPayload p) { for (int i = 0; i < p.burstCount; i++) { GameObject obj = Instantiate(projectilePrefab, pos, rot); Colorize(obj, c); SmartProjectile brain = obj.GetComponent<SmartProjectile>(); if (brain == null) brain = obj.AddComponent<SmartProjectile>(); brain.Initialize(p); yield return new WaitForSeconds(0.15f); } }

    void Colorize(GameObject obj, Color c) { var rend = obj.GetComponent<Renderer>(); if (rend) rend.material.color = c; }
    Color GetColorFromEffect(SpellEffect effect) { switch (effect) { case SpellEffect.Damage: return Color.red; case SpellEffect.Heal: return Color.green; case SpellEffect.Slow: return Color.cyan; case SpellEffect.Shield: return Color.yellow; default: return Color.white; } }
}