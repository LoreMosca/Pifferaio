using UnityEngine;

public class HordeTrigger : MonoBehaviour
{
    public enum TriggerType { StartHorde, EndHorde }

    public TriggerType type;
    private bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        // Scatta quando entra il PRINCIPE (Payload)
        // Usiamo il Principe perché è lui l'obiettivo. Il player potrebbe correre fuori da solo.
        if (other.CompareTag("Principe"))
        {
            hasTriggered = true;
            HordeManager horde = FindFirstObjectByType<HordeManager>();

            if (horde)
            {
                if (type == TriggerType.StartHorde)
                {
                    horde.StartHorde();
                }
                else if (type == TriggerType.EndHorde)
                {
                    horde.StopAndClearHorde();
                    // Qui puoi anche curare il player/principe
                }
            }
        }
    }
}