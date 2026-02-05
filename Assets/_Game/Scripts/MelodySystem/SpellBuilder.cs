using UnityEngine;
using System.Collections.Generic;

// --- SETTINGS ---
[System.Serializable] public class ProjectileSettings { public float baseDamage = 15f; public float speed = 20f; public float size = 0.5f; public int basePenetration = 0; }
[System.Serializable] public class AreaSettings { public float valuePerTick = 5f; public float radius = 4f; public float duration = 5f; public float tickInterval = 1.0f; }
[System.Serializable] public class BeamSettings { public float startDps = 8f; public float maxLength = 10f; public float duration = 3.5f; public float baseTickRate = 4f; public float damageFalloff = 0.3f; }
[System.Serializable] public class BuffSettings { public float statValue = 20f; public float duration = 10f; public float lootChance = 0.2f; public float baseTickRate = 1.0f; }

// --- PAYLOAD ---
[System.Serializable]
public struct SpellPayload
{
    public string constructedName;
    public SpellForm delivery;
    public SpellEffect effect;

    // Valori Finali
    public float powerValue;
    public float duration;
    public float sizeOrRange;
    public float moveSpeed;
    public int penetration;
    public int burstCount;
    public float tickRate;
    public float damageDecay;
    public float lootLuckChance;

    public List<Vector3> fireDirections;
}

public enum SpellEffect { Damage, Heal, Slow, Shield }
public enum SpellForm { Projectile, AreaAoE, LinearBeam, SelfBuff }

public class SpellBuilder : MonoBehaviour
{
    [Header("--- BILANCIAMENTO BASE ---")]
    public ProjectileSettings projectile;
    public AreaSettings area;
    public BeamSettings beam;
    public BuffSettings buff;

    [Header("--- PROGRESSIONE (LIVELLO) ---")]
    [Tooltip("Percentuale di aumento potenza per ogni livello (es. 0.2 = +20% per livello).")]
    public float powerPerLevel = 0.2f;

    public SpellPayload BuildSpell(Melody melody)
    {
        SpellPayload p = new SpellPayload();
        // Default
        p.fireDirections = new List<Vector3> { Vector3.forward };
        p.burstCount = 1;
        p.penetration = 0;
        p.damageDecay = 0;

        if (melody.sequence == null || melody.sequence.Count < 2)
        {
            p.constructedName = "Melodia Rotta";
            return p;
        }

        List<NoteDefinition> notes = melody.sequence;

        // 1. FORMA (Seconda Nota)
        switch (notes[1].color)
        {
            case NoteColor.Green: // Proiettile
                p.delivery = SpellForm.Projectile;
                p.powerValue = projectile.baseDamage;
                p.sizeOrRange = projectile.size;
                p.duration = 5.0f; // Lifetime proiettile
                p.moveSpeed = projectile.speed;
                p.penetration = projectile.basePenetration;
                break;
            case NoteColor.Blue: // Area
                p.delivery = SpellForm.AreaAoE;
                p.powerValue = area.valuePerTick;
                p.sizeOrRange = area.radius;
                p.duration = area.duration;
                p.tickRate = area.tickInterval;
                break;
            case NoteColor.Red: // Beam
                p.delivery = SpellForm.LinearBeam;
                p.powerValue = beam.startDps;
                p.sizeOrRange = beam.maxLength;
                p.duration = beam.duration;
                p.tickRate = beam.baseTickRate;
                p.damageDecay = beam.damageFalloff;
                p.penetration = 99; // Infinito (decadimento gestito dallo script)
                break;
            case NoteColor.Yellow: // Buff
                p.delivery = SpellForm.SelfBuff;
                p.powerValue = buff.statValue;
                p.duration = buff.duration;
                p.tickRate = buff.baseTickRate;
                p.lootLuckChance = buff.lootChance;
                break;
        }

        // 2. EFFETTO (Prima Nota)
        switch (notes[0].color)
        {
            case NoteColor.Green: p.effect = SpellEffect.Heal; break;
            case NoteColor.Blue: p.effect = SpellEffect.Slow; break;
            case NoteColor.Red: p.effect = SpellEffect.Damage; break;
            case NoteColor.Yellow: p.effect = SpellEffect.Shield; break;
        }

        // 3. CALCOLO POTENZA (Basato sul LIVELLO, non sul Tier)
        // Livello 1 = 100% potenza. Livello 2 = 120%. Livello 3 = 140%.
        float levelMultiplier = 1.0f + ((melody.level - 1) * powerPerLevel);
        p.powerValue *= levelMultiplier;

        // 4. ESTENSIONI (Dalla 3a nota in poi)
        int yellowCount = 0;
        for (int i = 2; i < notes.Count; i++)
        {
            if (notes[i].color == NoteColor.Yellow)
            {
                yellowCount++;
                ApplyYellowLogic(yellowCount, ref p);
            }
            else
            {
                ApplyExtension(notes[i].color, ref p);
            }
        }

        // Nome Finale (Include Tier per info rarità e Livello per info potenza)
        p.constructedName = $"{p.effect} {p.delivery} [T{melody.tier}] Lv.{melody.level}";
        return p;
    }

    void ApplyExtension(NoteColor c, ref SpellPayload p)
    {
        switch (c)
        {
            case NoteColor.Red:
                // Potenza extra (moltiplicatore aggiuntivo)
                p.powerValue *= 1.3f;
                if (p.delivery == SpellForm.Projectile) p.penetration++;
                if (p.delivery == SpellForm.LinearBeam) p.damageDecay *= 0.5f; // Smorza meno
                break;
            case NoteColor.Blue:
                p.sizeOrRange *= 1.4f;
                p.duration += 2.0f;
                break;
            case NoteColor.Green:
                if (p.delivery == SpellForm.Projectile) p.burstCount++;
                else if (p.delivery == SpellForm.LinearBeam) p.tickRate *= 1.5f;
                else if (p.delivery == SpellForm.SelfBuff) p.tickRate *= 1.5f;
                else if (p.delivery == SpellForm.AreaAoE) p.tickRate *= 0.7f;
                break;
        }
    }

    void ApplyYellowLogic(int count, ref SpellPayload p)
    {
        if (p.delivery == SpellForm.SelfBuff) p.lootLuckChance += 0.2f;
        else
        {
            if (count == 1 && !p.fireDirections.Contains(Vector3.back))
                p.fireDirections.Add(Vector3.back);
            else if (count >= 2)
            {
                if (!p.fireDirections.Contains(Vector3.right)) p.fireDirections.Add(Vector3.right);
                if (!p.fireDirections.Contains(Vector3.left)) p.fireDirections.Add(Vector3.left);
            }
        }
    }
}