using UnityEngine;
using UnityEngine.InputSystem; // Fondamentale
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [Header("UI Navigation")]
    public GameObject firstSelectedButton;

    [Header("UI Reference")]
    public GameObject pauseMenuCanvas; // Trascina qui il pannello del menu

    private GameInputs inputActions; // La classe generata automaticamente
    private bool isPaused = false;

    void Awake()
    {
        Instance = this;

        // Inizializza gli input
        inputActions = new GameInputs();

        // Iscriviti all'evento "performed" del tasto Pausa
        // NOTA: Assicurati che nel file inputactions l'azione si chiami "Pause" e sia sotto "Player"
        inputActions.Player.Pause.performed += context => TogglePause();

        if (pauseMenuCanvas) pauseMenuCanvas.SetActive(false);
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0f;
            if (pauseMenuCanvas) pauseMenuCanvas.SetActive(true);

            // --- AGGIUNGI QUESTE RIGHE PER IL GAMEPAD ---
            // 1. Pulisce la selezione precedente
            EventSystem.current.SetSelectedGameObject(null);
            // 2. Forza la selezione sul primo bottone
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
            // --------------------------------------------

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            if (pauseMenuCanvas) pauseMenuCanvas.SetActive(false);

            // Opzionale: Deseleziona tutto quando chiudi
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // --- FUNZIONI PER I BOTTONI DELLA UI (Unity Events) ---

    public void Resume()
    {
        // Chiamalo dal bottone "Riprendi"
        TogglePause();
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f; // Importante rimettere il tempo a 1 prima di ricaricare
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        // SceneManager.LoadScene("MainMenu"); // Se hai un menu principale
        Debug.Log("Torna al Menu");
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("QUIT GAME");
        Application.Quit();
    }
}