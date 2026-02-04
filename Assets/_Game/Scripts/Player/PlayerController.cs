using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Assicurati di usare Unity 6 / Cinemachine 3.0
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("--- BILANCIAMENTO ---")]
    [Tooltip("Velocità di movimento standard in metri/secondo.")]
    public float moveSpeed = 6f;
    [Tooltip("Velocità di rotazione del personaggio verso il mouse.")]
    public float rotationSpeed = 20f;
    [Tooltip("Forza di gravità (deve essere negativa).")]
    public float gravity = -9.81f;

    [Header("--- COMBAT MELEE (Attacchi 1-4) ---")]
    [Tooltip("Cooldown in secondi per ogni tasto [0=Verde, 1=Blu, 2=Rosso, 3=Giallo].")]
    public float[] attackCooldowns = new float[] { 0.4f, 0.8f, 1.5f, 1.0f };

    [Tooltip("Lista dei PREFAB VFX da spawnare. L'ordine deve corrispondere ai colori (0=Verde...).")]
    public GameObject[] skillVfxPrefabs;

    [Header("--- RIFERIMENTI VISUALI (Setup) ---")]
    [Tooltip("TRASCINA QUI: L'oggetto vuoto sulla PUNTA del bastone (per scia e hit).")]
    public Transform meleePoint;

    [Tooltip("TRASCINA QUI: L'oggetto vuoto 'SpellOrigin' (davanti al petto) per i proiettili.")]
    public Transform spellOrigin;

    [Tooltip("TRASCINA QUI: Il componente Cinemachine Impulse Source (sul Player) per il tremolio.")]
    public CinemachineImpulseSource impulseSource;

    [Header("--- SISTEMI ESTERNI ---")]
    [Tooltip("Trascina qui il GameObject con lo script SpellCasterSystem.")]
    public SpellCasterSystem spellSystem;
    [Tooltip("Trascina qui l'Animator del personaggio.")]
    public Animator animator;

    // --- VARIABILI INTERNE ---
    private CharacterController controller;
    private GameInputs inputActions;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Vector2 mousePos;
    private Vector3 velocity;
    private float[] nextAttackTime = new float[4];

    // Stati di Blocco
    private bool isCasting = false;     // True = Animazione Magia in corso
    private bool isAttacking = false;   // True = Animazione Melee in corso

    // Hash Animator (Ottimizzazione)
    private static readonly int AnimVelocityZ = Animator.StringToHash("VelocityZ");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimAttackType = Animator.StringToHash("AttackType");
    private static readonly int AnimCast = Animator.StringToHash("Cast");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;
        inputActions = new GameInputs();

        // Binding Input
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Look.performed += ctx => mousePos = ctx.ReadValue<Vector2>();

        inputActions.Player.Skill1.performed += ctx => PerformMeleeAttack(0);
        inputActions.Player.Skill2.performed += ctx => PerformMeleeAttack(1);
        inputActions.Player.Skill3.performed += ctx => PerformMeleeAttack(2);
        inputActions.Player.Skill4.performed += ctx => PerformMeleeAttack(3);

        inputActions.Player.Cast.performed += ctx => PerformCastAbility();

        // Tasto L per Loot Rapido (utile se non vuoi cliccare i bottoni a schermo)
        inputActions.Player.Loot.performed += ctx => spellSystem.LootRandom(1);
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Update()
    {
        // 1. GESTIONE STATI DI BLOCCO (Freeze)
        // Se stiamo attaccando o castando, il movimento WASD è disabilitato.
        if (isCasting || isAttacking)
        {
            ApplyGravity(); // Cadiamo comunque se siamo in aria

            // Opzionale: Permetti di mirare col mouse anche mentre sei fermo?
            // HandleRotation(); <--- Decommenta se vuoi ruotare mentre casti

            // Forza l'animazione di movimento a 0 (Idle)
            animator.SetFloat(AnimVelocityZ, 0f);
            return;
        }

        // 2. COMPORTAMENTO NORMALE
        HandleMovement();
        HandleRotation();
    }

    // --- LOGICA MOVIMENTO ---
    void HandleMovement()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        if (move.magnitude > 1f) move.Normalize();
        controller.Move(move * moveSpeed * Time.deltaTime);

        ApplyGravity();

        // Calcolo animazione locale (per gestire camminata all'indietro)
        Vector3 localMove = transform.InverseTransformDirection(move);
        animator.SetFloat(AnimVelocityZ, localMove.z, 0.1f, Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleRotation()
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 lookDir = hitPoint - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    // --- LOGICA COMBAT (MELEE - 1,2,3,4) ---
    void PerformMeleeAttack(int index)
    {
        if (Time.time < nextAttackTime[index]) return;
        nextAttackTime[index] = Time.time + attackCooldowns[index];

        // Blocca il movimento per un breve istante (Game Feel)
        StartCoroutine(MeleeLockRoutine(0.4f));

        // Ruota subito verso il nemico
        HandleRotation();

        // Invia nota e Anima
        spellSystem.PushNote(index);
        animator.SetInteger(AnimAttackType, index);
        animator.SetTrigger(AnimAttack);
    }

    IEnumerator MeleeLockRoutine(float duration)
    {
        isAttacking = true;
        yield return new WaitForSeconds(duration);
        isAttacking = false;
    }

    // --- LOGICA MAGIC (CAST - Spazio) ---
    void PerformCastAbility()
    {
        // Controlla se c'è qualcosa da lanciare PRIMA di bloccare il player
        if (!spellSystem.HasSpellReady())
        {
            Debug.Log("Nessuna melodia pronta! (Componi una sequenza valida)");
            return;
        }

        // Entra in stato Cast (Blocca finché l'animazione non finisce)
        isCasting = true;

        // Ruota verso il bersaglio un'ultima volta
        HandleRotation();

        // Trigger Animazione
        animator.SetTrigger(AnimCast);

        // NOTA: La spell partirà effettivamente all'evento OnSpellFireFrame
    }

    // =========================================================
    //              ANIMATION EVENTS (Chiamati dall'FBX)
    // =========================================================

    // 1. MELEE: Chiamato nel momento dell'impatto fisico
    public void OnAttackHit()
    {
        int index = animator.GetInteger(AnimAttackType);

        // Spawn VFX sulla punta del bastone
        if (skillVfxPrefabs.Length > index && skillVfxPrefabs[index] != null)
        {
            var vfx = Instantiate(skillVfxPrefabs[index], meleePoint.position, transform.rotation);

            // Se è lo Scudo (Giallo), lo attacchiamo al bastone/player
            if (index == 3) vfx.transform.SetParent(meleePoint);

            Destroy(vfx, 2f);
        }

        // Screen Shake (Solo per attacco Rosso/Pesante)
        if (index == 2 && impulseSource != null)
        {
            impulseSource.GenerateImpulse(0.5f);
        }
    }

    // 2. MAGIC: Chiamato quando il bastone è alto (Climax del Cast)
    public void OnSpellFireFrame()
    {
        // Dice al sistema di sparare usando lo SpellOrigin (punto fisso petto)
        spellSystem.FireCurrentSpell(spellOrigin);
    }

    // 3. MAGIC: Chiamato alla fine dell'animazione di Cast
    public void OnCastEndFrame()
    {
        isCasting = false; // Sblocca il movimento
    }
}