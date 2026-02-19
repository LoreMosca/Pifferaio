using UnityEngine;
using System.Collections.Generic;

public class DebugSystem : MonoBehaviour
{
    [Header("Settings")]
    public bool showDebugMenu = false;
    public KeyCode toggleKey = KeyCode.F1; // Premi F1 per aprire/chiudere

    // Riferimenti
    private PlayerStats playerStats;
    private DummyController princeStats;
    private HordeManager hordeManager;

    // Stato Cheats
    private bool godMode = false;

    void Start()
    {
        // Trova riferimenti (pigro, ma ok per debug)
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p) playerStats = p.GetComponent<PlayerStats>();

        GameObject pr = GameObject.FindGameObjectWithTag("Principe");
        if (pr) princeStats = pr.GetComponent<DummyController>();

        hordeManager = FindFirstObjectByType<HordeManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) showDebugMenu = !showDebugMenu;

        // God Mode Logic (Tiene la vita al massimo ogni frame)
        if (godMode)
        {
            if (playerStats) playerStats.currentHealth = playerStats.maxHealth;
            if (princeStats) princeStats.currentHealth = princeStats.maxHealth;
        }
    }

    void OnGUI()
    {
        if (!showDebugMenu) return;

        // Box Sfondo in alto a sinistra
        GUI.Box(new Rect(10, 10, 200, 350), "--- DEBUG MENU (F1) ---");

        // 1. GOD MODE
        string godText = godMode ? "GOD MODE: [ON]" : "GOD MODE: [OFF]";
        GUI.color = godMode ? Color.green : Color.white;
        if (GUI.Button(new Rect(20, 40, 180, 30), godText))
        {
            godMode = !godMode;
        }
        GUI.color = Color.white;

        // 2. CURE
        if (GUI.Button(new Rect(20, 80, 180, 30), "Full Heal Both"))
        {
            if (playerStats) playerStats.Heal(9999);
            if (princeStats) princeStats.Heal(9999);
        }

        // 3. KILL ALL
        GUI.color = Color.red;
        if (GUI.Button(new Rect(20, 120, 180, 30), "KILL ALL ENEMIES"))
        {
            KillAllEnemies();
        }
        GUI.color = Color.white;

        // 4. SPEED UP GAME
        if (GUI.Button(new Rect(20, 160, 85, 30), "Speed x1")) Time.timeScale = 1f;
        if (GUI.Button(new Rect(115, 160, 85, 30), "Speed x5")) Time.timeScale = 5f;

        // 5. ORDA CONTROLS
        if (hordeManager)
        {
            if (GUI.Button(new Rect(20, 200, 180, 30), "Skip Wave"))
            {
                // Trucco sporco: forziamo il timer dell'orda
                // Nota: Richiede di rendere 'waveTimer' pubblico in HordeManager o aggiungere un metodo Skip
                Debug.Log("Skip Wave non implementato (Richiede modifica HordeManager)");
            }
        }

        // 6. AGGIUNGI NOTE (Loot)
        if (GUI.Button(new Rect(20, 240, 180, 30), "+ Random Melody"))
        {
            var caster = FindFirstObjectByType<SpellCasterSystem>();
            if (caster) caster.LootFromTable("Common");
        }

        // 7. BREAK DOOR
        if (GUI.Button(new Rect(20, 280, 180, 30), "Break Door"))
        {
            var door = FindFirstObjectByType<BreakableDoor>();
            if (door) door.TakeDamage(9999);
        }
    }

    void KillAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Nemico");
        foreach (var e in enemies)
        {
            // Ignora la porta se ha il tag nemico
            if (e.GetComponent<BreakableDoor>()) continue;

            var dmg = e.GetComponent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(99999);
            else Destroy(e);
        }
    }
}