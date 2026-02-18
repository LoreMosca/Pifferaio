using UnityEngine;
using UnityEngine.InputSystem;

public class ControlsHelpUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Collega qui l'azione UI/ToggleHelp.")]
    public InputActionReference toggleAction;

    [Header("UI References")]
    [Tooltip("L'oggetto piccolo che dice 'Select: Controlli'.")]
    public GameObject promptObject;

    [Tooltip("Il pannello grande con tutta la lista.")]
    public GameObject fullListPanel;

    void Start()
    {
        // Stato iniziale: Prompt visibile, Lista nascosta
        if (promptObject != null) promptObject.SetActive(true);
        if (fullListPanel != null) fullListPanel.SetActive(false);

        // Attiva Input
        if (toggleAction != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnToggle;
        }
    }

    void OnDestroy()
    {
        if (toggleAction != null)
        {
            toggleAction.action.performed -= OnToggle;
            toggleAction.action.Disable();
        }
    }

    private void OnToggle(InputAction.CallbackContext context)
    {
        // Scambia lo stato
        bool isListOpen = fullListPanel.activeSelf;

        if (isListOpen)
        {
            // CHIUDI: Nascondi lista, mostra prompt
            fullListPanel.SetActive(false);
            promptObject.SetActive(true);
        }
        else
        {
            // APRI: Mostra lista, nascondi prompt
            fullListPanel.SetActive(true);
            promptObject.SetActive(false);
        }
    }
}