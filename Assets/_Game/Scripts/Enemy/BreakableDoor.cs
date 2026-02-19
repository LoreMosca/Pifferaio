using UnityEngine;

public class BreakableDoor : MonoBehaviour, IDamageable
{
    [Header("Configurazione")]
    public float health = 50f;
    public GameObject doorModel; // L'oggetto visivo
    public GameObject brokenParticles; // Effetto distruzione

    [Header("Azione all'apertura")]
    public PayloadMover princeMover; // Trascina qui il Principe

    public void TakeDamage(float amount)
    {
        health -= amount;
        // Feedback visivo opzionale (es. shake)

        if (health <= 0)
        {
            BreakOpen();
        }
    }

    void BreakOpen()
    {
        // 1. Attiva particelle
        if (brokenParticles) Instantiate(brokenParticles, transform.position, Quaternion.identity);

        // 2. Nascondi/Distruggi porta
        if (doorModel) doorModel.SetActive(false); // O Destroy
        GetComponent<Collider>().enabled = false; // Disabilita collisione fisica

        // 3. FAI PARTIRE IL PRINCIPE
        if (princeMover)
        {
            Debug.Log("La porta è rotta! Il Principe avanza.");
            // Assicurati che lo script fosse attivo ma in stato 'Waiting' o simile
            // Se lo script PayloadMover era disabilitato:
            princeMover.enabled = true;
        }

        // Disabilita questo script per non romperla due volte
        this.enabled = false;
    }

    // Metodi IDamageable non usati ma necessari per interfaccia
    public void Heal(float amount) { }
    public void AddShield(float amount) { }
    public void ApplySlow(float percentage, float duration) { }
}