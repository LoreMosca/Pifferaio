using UnityEngine;
using System.Collections.Generic;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance;

    [Header("Configurazione")]
    public GameObject buffIconPrefab;
    public Transform container;

    [Header("Icone Specifiche (4 Tipi)")]
    public Sprite healIcon;
    public Sprite shieldIcon;
    public Sprite damageIcon;
    public Sprite speedIcon;

    void Awake() { Instance = this; }

    public void AddBuff(SpellEffect type, float duration)
    {
        Sprite iconToUse = null;
        Color colorToUse = Color.white;

        switch (type)
        {
            case SpellEffect.Heal: iconToUse = healIcon; colorToUse = Color.green; break;
            case SpellEffect.Shield: iconToUse = shieldIcon; colorToUse = Color.yellow; break;
            case SpellEffect.DamageUp: iconToUse = damageIcon; colorToUse = Color.red; break;
            case SpellEffect.SpeedUp: iconToUse = speedIcon; colorToUse = Color.cyan; break;
        }

        if (iconToUse != null && buffIconPrefab != null)
        {
            GameObject obj = Instantiate(buffIconPrefab, container);
            obj.GetComponent<BuffIcon>().Setup(iconToUse, duration, colorToUse);
        }
    }
}