using UnityEngine;
using System.Collections.Generic;

// --- CONFIGURAZIONI DESIGNER SPECIFICHE ---
// Queste classi servono solo a tenere l'Inspector ordinato e chiaro.

[System.Serializable]
public class ProjectileSettings
{
    [Header("Bilanciamento Proiettile")]
    [Tooltip("Danno base all'impatto.")]
    public float baseDamage = 15f;
    [Tooltip("Velocità di movimento (metri/sec).")]
    public float speed = 20f;
    [Tooltip("Grandezza fisica della sfera.")]
    public float size = 0.5f;
}

[System.Serializable]
public class AreaSettings
{
    [Header("Bilanciamento Area (AoE)")]
    [Tooltip("Danno o Cura per ogni Tick.")]
    public float valuePerTick = 5f;
    [Tooltip("Raggio dell'area in metri.")]
    public float radius = 4f;
    [Tooltip("Quanto dura la zona a terra (secondi).")]
    public float duration = 5f;
    [Tooltip("Ogni quanto applica l'effetto (secondi). Basso = Più veloce.")]
    public float tickInterval = 1.0f;
}

[System.Serializable]
public class BeamSettings
{
    [Header("Bilanciamento Laser (Beam)")]
    [Tooltip("Danno iniziale al secondo.")]
    public float startDps = 8f;
    [Tooltip("Lunghezza massima del raggio.")]
    public float maxLength = 10f;
    [Tooltip("Durata del raggio (secondi).")]
    public float duration = 3.5f;
    [Tooltip("Quanto aumenta il danno/cura al secondo (Moltiplicatore).")]
    public float rampUpSpeed = 1.5f;
}

[System.Serializable]
public class BuffSettings
{
    [Header("Bilanciamento Passive")]
    [Tooltip("Valore del buff (es. % Scudo o Stat).")]
    public float statValue = 20f;
    [Tooltip("Durata del buff sul Pifferaio.")]
    public float duration = 10f;
    [Tooltip("Chance extra di lootare note rare (0.1 = 10%).")]
    public float lootChance = 0.2f;
}

// --- DATI DI GIOCO (PAYLOAD) ---
// Questa è la "scatola" che passiamo al sistema di gioco.
[System.Serializable]
public struct SpellPayload
{
    public string constructedName;
    public string logicDescription;

    // Tipi
    public SpellEffect effect;
    public SpellForm delivery;

    // Valori finali (Calcolati)
    public float powerValue;
    public float sizeOrRange;
    public float duration;
    public float tickRate;
    public float lootLuckChance;

    // Geometria
    public List<Vector3> fireDirections;
}

public enum SpellEffect { Damage, Heal, Slow, Shield }
public enum SpellForm { Projectile, AreaAoE, LinearBeam, SelfBuff }

public class SpellBuilder : MonoBehaviour
{
    [Header("--- CONFIGURAZIONE ARCHETIPI ---")]
    [Space(10)]
    public ProjectileSettings projectile;
    [Space(5)]
    public AreaSettings area;
    [Space(5)]
    public BeamSettings beam;
    [Space(5)]
    public BuffSettings buff;

    [Header("--- PROGRESSIONE ---")]
    [Space(10)]
    [Tooltip("Moltiplicatore potenza per livello (0.2 = +20% a livello).")]
    public float powerPerLevel = 0.25f;

    public SpellPayload BuildSpell(Melody melody)
    {
        SpellPayload payload = new SpellPayload();
        List<NoteDefinition> notes = melody.sequence;

        // Default setup
        payload.fireDirections = new List<Vector3> { Vector3.forward };

        if (notes == null || notes.Count < 2)
        {
            payload.constructedName = "Melodia Spezzata";
            return payload;
        }

        // --- 1. APPLICAZIONE CONFIGURAZIONE BASE ---
        // Qui leggiamo i dati dalle classi specifiche dell'Inspector

        NoteColor formColor = notes[1].color;
        switch (formColor)
        {
            case NoteColor.Green:  // PROIETTILE
                payload.delivery = SpellForm.Projectile;
                payload.powerValue = projectile.baseDamage;
                payload.sizeOrRange = projectile.size;
                payload.duration = 3.0f; // Default safety destruction
                payload.tickRate = projectile.speed; // Usiamo tickRate per passare la velocità
                break;

            case NoteColor.Blue:   // AREA (AoE)
                payload.delivery = SpellForm.AreaAoE;
                payload.powerValue = area.valuePerTick;
                payload.sizeOrRange = area.radius;
                payload.duration = area.duration;
                payload.tickRate = area.tickInterval;
                break;

            case NoteColor.Red:    // BEAM (Laser)
                payload.delivery = SpellForm.LinearBeam;
                payload.powerValue = beam.startDps;
                payload.sizeOrRange = beam.maxLength;
                payload.duration = beam.duration;
                payload.tickRate = beam.rampUpSpeed; // Usiamo tickRate per il ramp-up
                break;

            case NoteColor.Yellow: // BUFF (Passiva)
                payload.delivery = SpellForm.SelfBuff;
                payload.powerValue = buff.statValue;
                payload.duration = buff.duration;
                payload.lootLuckChance = buff.lootChance;
                break;
        }

        // --- 2. SCALING LIVELLO ---
        float levelMultiplier = 1.0f + ((melody.level - 1) * powerPerLevel);
        payload.powerValue *= levelMultiplier;

        // --- 3. DEFINIZIONE EFFETTO (RADICE) ---
        switch (notes[0].color)
        {
            case NoteColor.Green: payload.effect = SpellEffect.Heal; break;
            case NoteColor.Blue: payload.effect = SpellEffect.Slow; break;
            case NoteColor.Red: payload.effect = SpellEffect.Damage; break;
            case NoteColor.Yellow: payload.effect = SpellEffect.Shield; break;
        }

        // --- 4. ESTENSIONI ---
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

        // --- 5. COSTRUZIONE NOME E DESCRIZIONE ---
        payload.constructedName = GenerateName(payload);
        payload.logicDescription = GenerateDesc(payload);

        return payload;
    }

    void ApplyExtension(NoteColor c, ref SpellPayload p)
    {
        // Logica semplice e universale
        switch (c)
        {
            case NoteColor.Red:   // + POTENZA
                p.powerValue *= 1.5f; // +50% Danno/Cura
                break;

            case NoteColor.Blue:  // + SPAZIO / TEMPO
                p.sizeOrRange *= 1.4f; // +40% Grandezza
                p.duration += 2.0f;    // +2 Secondi
                break;

            case NoteColor.Green: // + VELOCITA'
                // Se è un proiettile aumenta velocità, se è area riduce tick
                if (p.delivery == SpellForm.Projectile) p.tickRate *= 1.5f;
                else if (p.delivery == SpellForm.AreaAoE) p.tickRate *= 0.7f;
                else if (p.delivery == SpellForm.LinearBeam) p.tickRate += 0.5f; // Ramp up più veloce
                break;
        }
    }

    void ApplyYellowLogic(int count, ref SpellPayload p)
    {
        if (p.delivery == SpellForm.SelfBuff)
        {
            p.lootLuckChance += 0.2f; // Più fortuna
        }
        else
        {
            // Geometria
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

    string GenerateName(SpellPayload p)
    {
        string geo = p.fireDirections.Count > 1 ? "Multi " : "";
        if (p.delivery == SpellForm.SelfBuff) return $"Buff {p.effect}";
        return $"{geo}{p.effect} {p.delivery}";
    }

    string GenerateDesc(SpellPayload p)
    {
        return $"Pwr: {p.powerValue:F1} | Dur: {p.duration:F1}s | Size: {p.sizeOrRange:F1}";
    }
}