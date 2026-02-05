using UnityEngine;
using System.Collections;

public class SpellVisualizer : MonoBehaviour
{
    public GameObject projectilePrefab;
    public GameObject areaPrefab;
    public GameObject beamPrefab;

    [Header("--- CAST POINTS ---")]
    [Tooltip("IMPORTANTE: Ruota questi punti nell'editor! (Es. Back Y=180)")]
    public Transform castPointForward;
    public Transform castPointBack;
    public Transform castPointLeft;
    public Transform castPointRight;

    public void VisualizeSpell(SpellPayload payload, Transform originPoint)
    {
        if (originPoint == null) originPoint = transform;
        Color spellColor = GetColorFromEffect(payload.effect);

        if (payload.delivery == SpellForm.SelfBuff)
        {
            ApplyBuffVisual(originPoint.root, spellColor, payload.duration);
            return;
        }

        foreach (Vector3 dir in payload.fireDirections)
        {
            // 1. Prendi il CastPoint GIÀ RUOTATO
            Transform spawnPoint = GetDirectionalCastPoint(dir, originPoint);

            // 2. Spawn
            switch (payload.delivery)
            {
                case SpellForm.Projectile:
                    // Il proiettile usa la rotazione del punto di spawn
                    StartCoroutine(SpawnProjectileBurst(spawnPoint.position, spawnPoint.rotation, spellColor, payload));
                    break;

                case SpellForm.AreaAoE:
                    Vector3 areaPos = spawnPoint.position + (spawnPoint.forward * 3f);
                    areaPos.y = 0.1f;
                    SpawnArea(areaPos, spellColor, payload);
                    break;

                case SpellForm.LinearBeam:
                    // Il beam diventa figlio e resetta la rotazione
                    SpawnBeam(spawnPoint, spellColor, payload, originPoint);
                    break;
            }
        }
    }

    void SpawnBeam(Transform parentPoint, Color c, SpellPayload p, Transform owner)
    {
        // 1. Istanzia
        GameObject obj = Instantiate(beamPrefab, parentPoint.position, parentPoint.rotation);

        // 2. Rendi Figlio
        obj.transform.SetParent(parentPoint);

        // 3. RESETTA LOCALI (Fondamentale!)
        // Dicendogli "LocalRotation = 0", lui si gira esattamente come il padre.
        // Se il padre è ruotato indietro, lui guarda indietro.
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        JuicyBeam juice = obj.GetComponent<JuicyBeam>();
        if (juice == null) juice = obj.AddComponent<JuicyBeam>();

        PlayerController pc = owner.GetComponentInParent<PlayerController>();
        if (pc == null) pc = owner.GetComponent<PlayerController>();

        juice.Initialize(pc, p, c);
    }

    Transform GetDirectionalCastPoint(Vector3 dir, Transform defaultOrigin)
    {
        // Usa il Dot Product per capire quale cast point approssima meglio la direzione richiesta
        if (Vector3.Dot(dir, Vector3.forward) > 0.9f) return castPointForward ? castPointForward : defaultOrigin;
        if (Vector3.Dot(dir, Vector3.back) > 0.9f) return castPointBack ? castPointBack : defaultOrigin;
        if (Vector3.Dot(dir, Vector3.left) > 0.9f) return castPointLeft ? castPointLeft : defaultOrigin;
        if (Vector3.Dot(dir, Vector3.right) > 0.9f) return castPointRight ? castPointRight : defaultOrigin;
        return defaultOrigin;
    }

    // --- Helper (Invariati) ---
    IEnumerator SpawnProjectileBurst(Vector3 pos, Quaternion rot, Color c, SpellPayload p) { for (int i = 0; i < p.burstCount; i++) { GameObject obj = Instantiate(projectilePrefab, pos, rot); Colorize(obj, c); SmartProjectile brain = obj.GetComponent<SmartProjectile>(); if (brain == null) brain = obj.AddComponent<SmartProjectile>(); brain.Initialize(p); yield return new WaitForSeconds(0.15f); } }
    void SpawnArea(Vector3 pos, Color c, SpellPayload p) { GameObject obj = Instantiate(areaPrefab, pos, Quaternion.identity); Colorize(obj, c); obj.transform.localScale = new Vector3(p.sizeOrRange, 0.1f, p.sizeOrRange); Destroy(obj, p.duration); }
    void ApplyBuffVisual(Transform targetRoot, Color c, float duration) { Vector3 pos = targetRoot.position; pos.y = 0.1f; GameObject aura = Instantiate(areaPrefab, pos, Quaternion.identity); aura.transform.SetParent(targetRoot); Colorize(aura, new Color(c.r, c.g, c.b, 0.3f)); Destroy(aura, duration); }
    void Colorize(GameObject obj, Color c) { var rend = obj.GetComponent<Renderer>(); if (rend) rend.material.color = c; }
    Color GetColorFromEffect(SpellEffect effect) { switch (effect) { case SpellEffect.Damage: return Color.red; case SpellEffect.Heal: return Color.green; case SpellEffect.Slow: return Color.cyan; case SpellEffect.Shield: return Color.yellow; default: return Color.white; } }
}