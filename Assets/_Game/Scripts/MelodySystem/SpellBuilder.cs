using UnityEngine;
using System.Collections.Generic;

// --- CLASSI DI CONFIGURAZIONE PER INSPECTOR ---

[System.Serializable]
public class ProjectileSettings
{
    [Header("Bilanciamento Proiettile")]
    [Tooltip("Danno o Cura applicato all'impatto.")]
    public float baseDamage = 15f;
    [Tooltip("Velocità di volo del proiettile.")]
    public float speed = 20f;
    [Tooltip("Dimensione visiva della sfera.")]
    public float size = 0.5f;
}

[System.Serializable]
public class AreaSettings
{
    [Header("Bilanciamento Area (AoE)")]
    [Tooltip("Quanto Danno/Cura applica per ogni tick.")]
    public float valuePerTick = 5f;
    [Tooltip("Raggio dell'area in metri.")]
    public float radius = 4f;
    [Tooltip("Durata totale della zona a terra.")]
    public float duration = 5f;
    [Tooltip("Ogni quanti secondi applica l'effetto (Basso = Più frequente).")]
    public float tickInterval = 1.0f;
}

[System.Serializable]
public class BeamSettings
{
    [Header("Bilanciamento Laser (Beam)")]
    [Tooltip("Danno/Cura al secondo iniziale.")]
    public float startDps = 8f;
    [Tooltip("Lunghezza massima del raggio.")]
    public float maxLength = 10f;
    [Tooltip("Durata dell'emissione.")]
    public float duration = 3.5f;
    [Tooltip("Moltiplicatore di crescita del danno nel tempo.")]
    public float rampUpSpeed = 1.5f;
}

[System.Serializable]
public class BuffSettings
{
    [Header("Bilanciamento Passive")]
    [Tooltip("Valore della statistica potenziata.")]
    public float statValue = 20f;
    [Tooltip("Durata del buff sul Pifferaio.")]
    public float duration = 10f;
    [Tooltip("Percentuale extra di drop note (0.2 = 20%).")]
    public float lootChance = 0.2f;
}

// --- PAYLOAD (DATI DI GIOCO) ---

[System.Serializable]
public struct SpellPayload
{
    public string constructedName;
    public string logicDescription;

    public SpellEffect effect;
    public SpellForm delivery;

    // Stats
    public float powerValue;
    public float sizeOrRange;
    public float duration;

    // Mechanics Specifiche
    public float moveSpeed;         // Velocità movimento
    public int burstCount;          // Numero colpi (Verde)
    public int penetration;         // Numero nemici passanti (Rosso)
    public float tickRate;          // Frequenza tick (Blu/Verde)

    public float lootLuckChance;
    public List<Vector3> fireDirections;
}

public enum SpellEffect { Damage, Heal, Slow, Shield }
public enum SpellForm { Projectile, AreaAoE, LinearBeam, SelfBuff }

public class SpellBuilder : MonoBehaviour
{
    [Header("--- CONFIGURAZIONE ARCHETIPI ---")]
    public ProjectileSettings projectile;
    public AreaSettings area;
    public BeamSettings beam;
    public BuffSettings buff;

    [Header("--- PROGRESSIONE ---")]
    [Tooltip("Moltiplicatore potenza per livello (0.25 = +25%).")]
    public float powerPerLevel = 0.25f;

    public SpellPayload BuildSpell(Melody melody)
    {
        SpellPayload payload = new SpellPayload();
        List<NoteDefinition> notes = melody.sequence;

        // Defaults
        payload.fireDirections = new List<Vector3> { Vector3.forward };
        payload.burstCount = 1;
        payload.penetration = 0;

        if (notes == null || notes.Count < 2)
        {
            payload.constructedName = "Melodia Rotta";
            return payload;
        }

        // 1. CONFIGURAZIONE BASE (Dal Designer)
        NoteColor formColor = notes[1].color;
        switch (formColor)
        {
            case NoteColor.Green:  // PROIETTILE
                payload.delivery = SpellForm.Projectile;
                payload.powerValue = projectile.baseDamage;
                payload.sizeOrRange = projectile.size;
                payload.duration = 3.0f; // Safety destroy timer
                payload.moveSpeed = projectile.speed;
                break;

            case NoteColor.Blue:   // AREA
                payload.delivery = SpellForm.AreaAoE;
                payload.powerValue = area.valuePerTick;
                payload.sizeOrRange = area.radius;
                payload.duration = area.duration;
                payload.tickRate = area.tickInterval;
                break;

            case NoteColor.Red:    // BEAM
                payload.delivery = SpellForm.LinearBeam;
                payload.powerValue = beam.startDps;
                payload.sizeOrRange = beam.maxLength;
                payload.duration = beam.duration;
                payload.tickRate = beam.rampUpSpeed;
                break;

            case NoteColor.Yellow: // BUFF
                payload.delivery = SpellForm.SelfBuff;
                payload.powerValue = buff.statValue;
                payload.duration = buff.duration;
                payload.lootLuckChance = buff.lootChance;
                break;
        }

        // 2. SCALING LEVEL
        float levelMultiplier = 1.0f + ((melody.level - 1) * powerPerLevel);
        payload.powerValue *= levelMultiplier;

        // 3. EFFETTO
        switch (notes[0].color)
        {
            case NoteColor.Green: payload.effect = SpellEffect.Heal; break;
            case NoteColor.Blue: payload.effect = SpellEffect.Slow; break;
            case NoteColor.Red: payload.effect = SpellEffect.Damage; break;
            case NoteColor.Yellow: payload.effect = SpellEffect.Shield; break;
        }

        // 4. ESTENSIONI
        int yellowCount = 0;
        for (int i = 2; i < notes.Count; i++)
        {
            if (notes[i].color == NoteColor.Yellow)
            {
                yellowCount++;
                ApplyYellowLogic(yellowCount, ref payload);
            }
            else
            {
                ApplyExtension(notes[i].color, ref payload);
            }
        }

        payload.constructedName = $"{payload.effect} {payload.delivery} Lv.{melody.level}";
        payload.logicDescription = GenerateDesc(payload);

        return payload;
    }

    void ApplyExtension(NoteColor c, ref SpellPayload p)
    {
        switch (c)
        {
            case NoteColor.Red:
                // Se proiettile -> Perforazione
                if (p.delivery == SpellForm.Projectile) p.penetration++;
                // Sempre -> Danno
                p.powerValue *= 1.5f;
                break;

            case NoteColor.Blue:
                // Sempre -> Area e Durata
                p.sizeOrRange *= 1.4f;
                p.duration += 2.0f;
                break;

            case NoteColor.Green:
                // Se proiettile -> Raffica (Burst)
                if (p.delivery == SpellForm.Projectile)
                    p.burstCount++;
                else if (p.delivery == SpellForm.AreaAoE)
                    p.tickRate *= 0.7f; // Tick più veloce
                else
                    p.moveSpeed *= 1.2f; // Fallback generico
                break;
        }
    }

    void ApplyYellowLogic(int count, ref SpellPayload p)
    {
        if (p.delivery == SpellForm.SelfBuff) p.lootLuckChance += 0.2f;
        else
        {
            if (count == 1) p.fireDirections.Add(Vector3.back);
            else if (count >= 2)
            {
                if (!p.fireDirections.Contains(Vector3.right))
                {
                    p.fireDirections.Add(Vector3.right);
                    p.fireDirections.Add(Vector3.left);
                }
            }
        }
    }

    string GenerateDesc(SpellPayload p)
    {
        string extra = "";
        if (p.burstCount > 1) extra += $" | Burst x{p.burstCount}";
        if (p.penetration > 0) extra += $" | Pierce {p.penetration}";
        return $"Pwr: {p.powerValue:F1} {extra}";
    }
}