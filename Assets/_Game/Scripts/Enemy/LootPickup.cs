using UnityEngine;

public class LootPickup : MonoBehaviour
{
    [Header("Dati")]
    public Melody melodyContent; // La spell contenuta

    [Header("Visuals")]
    public float rotateSpeed = 90f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.5f;
    [Tooltip("Particle System per l'aura di rarità.")]
    public ParticleSystem rarityVFX;

    private Vector3 startPos;
    private bool isCollected = false;

    void Start()
    {
        startPos = transform.position;
        UpdateVisuals();
    }

    public void Initialize(Melody melody)
    {
        melodyContent = melody;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (melodyContent == null) return;

        // Determina colore in base al Tier
        Color c = Color.white;
        switch (melodyContent.tier)
        {
            case 1: c = Color.white; break;       // Comune
            case 2: c = Color.cyan; break;        // Raro
            case 3: c = Color.yellow; break;      // Epico
            case 4: c = new Color(1f, 0.5f, 0f); break; // Leggendario
        }

        // Colora la Mesh
        var rend = GetComponent<Renderer>();
        if (rend)
        {
            rend.material.color = c;
            rend.material.SetColor("_EmissionColor", c * 0.5f);
            rend.material.EnableKeyword("_EMISSION");
        }

        // Colora il VFX (se presente)
        if (rarityVFX != null)
        {
            var main = rarityVFX.main;
            main.startColor = c;
            rarityVFX.Play();
        }
    }

    void Update()
    {
        // Animazione Galleggiamento
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        if (other.CompareTag("Player"))
        {
            isCollected = true;

            // Consegna il loot
            var caster = other.GetComponent<SpellCasterSystem>();
            if (caster != null)
            {
                // Usa il metodo pubblico che abbiamo aggiunto prima (o aggiungeremo ora)
                caster.LootFromPickup(melodyContent);
            }

            // Feedback sonoro qui (opzionale)
            Destroy(gameObject);
        }
    }
}