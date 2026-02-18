using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem; // <--- NECESSARIO PER IL NUOVO SYSTEM

public class GrimoireUI : MonoBehaviour
{
    [Header("Riferimenti")]
    public SpellCasterSystem spellSystem;

    [Header("Input (New System)")]
    [Tooltip("Collega qui l'azione UI/ScrollGrimoire dal tuo Input Asset.")]
    public InputActionReference scrollAction;

    [Header("Configurazione UI")]
    public GameObject entryPrefab;
    public Transform container;
    public int itemsPerPage = 5;

    [Header("Navigazione Visual")]
    public TextMeshProUGUI pageIndicatorText;
    public GameObject arrowDown;
    public GameObject arrowUp;

    // Stato Interno
    private int currentPage = 0;
    private List<Melody> sortedSpells = new List<Melody>();
    private List<GrimoireEntry> slotPool = new List<GrimoireEntry>();

    void Start()
    {
        InitializeSlots();

        if (spellSystem != null)
        {
            spellSystem.OnGrimoireUpdated += FullRefresh;
            spellSystem.OnInputChanged += SortAndRefresh;
            FullRefresh();
        }

        // --- ATTIVAZIONE INPUT ---
        if (scrollAction != null)
        {
            scrollAction.action.Enable();
            // Ci iscriviamo all'evento: quando premi, chiama OnScroll
            scrollAction.action.performed += OnScroll;
        }
    }

    void OnDestroy()
    {
        if (spellSystem != null)
        {
            spellSystem.OnGrimoireUpdated -= FullRefresh;
            spellSystem.OnInputChanged -= SortAndRefresh;
        }

        // --- DISATTIVAZIONE INPUT ---
        if (scrollAction != null)
        {
            scrollAction.action.performed -= OnScroll;
            scrollAction.action.Disable();
        }
    }

    // NON SERVE PIÙ IL UPDATE() PER L'INPUT

    // --- NUOVO METODO DI INPUT ---
    private void OnScroll(InputAction.CallbackContext context)
    {
        // Legge il valore dell'asse (-1 per Giù, +1 per Su)
        float value = context.ReadValue<float>();

        if (value > 0) ChangePage(-1); // Su -> Pagina Precedente
        else if (value < 0) ChangePage(1);  // Giù -> Pagina Successiva
    }

    void InitializeSlots()
    {
        foreach (Transform child in container) Destroy(child.gameObject);
        slotPool.Clear();
        for (int i = 0; i < itemsPerPage; i++)
        {
            GameObject obj = Instantiate(entryPrefab, container);
            GrimoireEntry entry = obj.GetComponent<GrimoireEntry>();
            obj.SetActive(false);
            slotPool.Add(entry);
        }
    }

    public void FullRefresh()
    {
        List<Melody> inventory = spellSystem.GetInventory();
        if (inventory == null) return;
        sortedSpells = new List<Melody>(inventory);
        SortAndRefresh();
    }

    void SortAndRefresh()
    {
        if (spellSystem == null) return;
        Melody readySpell = spellSystem.GetReadySpell();

        sortedSpells.Sort((a, b) => {
            bool readyA = (readySpell != null && a == readySpell);
            bool readyB = (readySpell != null && b == readySpell);
            if (readyA && !readyB) return -1;
            if (!readyA && readyB) return 1;

            int tierC = b.tier.CompareTo(a.tier);
            if (tierC != 0) return tierC;

            int levelC = b.level.CompareTo(a.level);
            if (levelC != 0) return levelC;

            return a.spellName.CompareTo(b.spellName);
        });

        if (readySpell != null) currentPage = 0;
        UpdateDisplay();
    }

    void ChangePage(int direction)
    {
        int maxPages = Mathf.CeilToInt((float)sortedSpells.Count / itemsPerPage);
        if (maxPages == 0) maxPages = 1;

        int newPage = currentPage + direction;

        if (newPage >= 0 && newPage < maxPages)
        {
            currentPage = newPage;
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        int totalSpells = sortedSpells.Count;
        int startIndex = currentPage * itemsPerPage;
        Melody readySpell = spellSystem.GetReadySpell();

        for (int i = 0; i < itemsPerPage; i++)
        {
            int dataIndex = startIndex + i;
            GrimoireEntry slot = slotPool[i];

            if (dataIndex < totalSpells)
            {
                Melody data = sortedSpells[dataIndex];
                slot.gameObject.SetActive(true);
                slot.Setup(data);
                bool isReady = (readySpell != null && data == readySpell);
                slot.SetHighlight(isReady);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }

        int totalPages = Mathf.CeilToInt((float)totalSpells / itemsPerPage);
        if (totalPages == 0) totalPages = 1;

        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{currentPage + 1}/{totalPages}";

        if (arrowUp != null) arrowUp.SetActive(currentPage > 0);
        if (arrowDown != null) arrowDown.SetActive(currentPage < totalPages - 1);
    }
}