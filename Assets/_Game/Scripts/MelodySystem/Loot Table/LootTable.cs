using UnityEngine;

[CreateAssetMenu(fileName = "New_LootTable", menuName = "Pifferaio/Loot Table", order = 2)]
public class LootTable : ScriptableObject
{
    [Header("Probabilità Drop (Pesi)")]
    [Tooltip("Più alto è il numero, più è probabile.")]
    public int commonWeight = 100; // Tier 1
    public int rareWeight = 30;    // Tier 2
    public int epicWeight = 10;    // Tier 3
    public int legendaryWeight = 2;// Tier 4

    /// <summary>
    /// Estrae un Tier casuale (1-4) basandosi sui pesi.
    /// </summary>
    public int PickRandomTier()
    {
        int totalWeight = commonWeight + rareWeight + epicWeight + legendaryWeight;
        int randomValue = Random.Range(0, totalWeight);

        if (randomValue < commonWeight) return 1;
        randomValue -= commonWeight;

        if (randomValue < rareWeight) return 2;
        randomValue -= rareWeight;

        if (randomValue < epicWeight) return 3;

        return 4; // Legendary
    }
}