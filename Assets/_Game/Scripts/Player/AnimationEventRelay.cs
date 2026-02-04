using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [Tooltip("Trascina qui l'oggetto Padre che ha il PlayerController")]
    public PlayerController mainController;

    private void Awake()
    {
        // Prova a trovarlo da solo se ti dimentichi di assegnarlo
        if (mainController == null)
            mainController = GetComponentInParent<PlayerController>();
    }

    // --- METODI CHIAMATI DALL'ANIMATOR ---

    // 1. Magia: Fuoco
    public void OnSpellFireFrame()
    {
        if (mainController) mainController.OnSpellFireFrame();
    }

    // 2. Magia: Fine
    public void OnCastEndFrame()
    {
        if (mainController) mainController.OnCastEndFrame();
    }
 
}