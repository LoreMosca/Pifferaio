using UnityEngine;
using UnityEngine.UI;

public class BuffIcon : MonoBehaviour
{
    public Image iconImage;
    [Tooltip("Immagine 'Filled' scura sovrapposta per mostrare il tempo rimanente.")]
    public Image cooldownFill;

    private float maxDuration;
    private float currentTimer;

    public void Setup(Sprite sprite, float duration, Color color)
    {
        if (iconImage)
        {
            iconImage.sprite = sprite;
            iconImage.color = color;
        }
        maxDuration = duration;
        currentTimer = duration;
    }

    void Update()
    {
        currentTimer -= Time.deltaTime;

        if (cooldownFill)
            cooldownFill.fillAmount = currentTimer / maxDuration;

        if (currentTimer <= 0)
            Destroy(gameObject);
    }
}