using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float fadeDuration = 1f;
    public Vector3 randomOffset = new Vector3(0.5f, 0, 0);

    private TextMeshPro textMesh;
    private float alpha = 1f;
    private float timer = 0f;

    void Awake()
    {
        textMesh = GetComponentInChildren<TextMeshPro>();
        // Offset casuale
        transform.position += new Vector3(Random.Range(-randomOffset.x, randomOffset.x),
                                          Random.Range(-randomOffset.y, randomOffset.y), 0);
    }

    public void Setup(string text, Color color, float size = 4f)
    {
        if (textMesh)
        {
            textMesh.text = text;
            textMesh.color = color;
            textMesh.fontSize = size;
        }
    }

    void LateUpdate()
    {
        // 1. BILLBOARD: Guarda sempre la camera
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        // 2. MOVIMENTO: Sempre verso l'alto del MONDO (Y), non locale
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 3. FADE
        timer += Time.deltaTime;
        if (timer > 0.5f)
        {
            alpha -= Time.deltaTime / fadeDuration;
            if (textMesh) textMesh.color = new Color(textMesh.color.r, textMesh.color.g, textMesh.color.b, alpha);
        }

        if (alpha <= 0) Destroy(gameObject);
    }
}