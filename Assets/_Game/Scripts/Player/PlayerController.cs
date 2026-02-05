using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))] // Assicura che ci siano le stats
public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class AttackConfig
    {
        [Header("Bilanciamento")]
        public float damage;
        public float knockback;
        public float staminaCost = 20f;
        public float cooldown = 0.2f;

        [Header("Visuals")]
        public GameObject prefab;
        public GameObject chargePrefab;
        public GameObject impactVfx;

        [Header("Posizionamento")]
        public AttackOrigin originType = AttackOrigin.CastPoint;
        public float forwardOffset = 0.0f;
        public float heightOffset = 0.0f;

        [Header("Trasformazione")]
        public Vector3 startScale = Vector3.one;
        public Vector3 endScale = Vector3.one;
        public float duration = 0.2f;

        [Header("Juice")]
        public float screenShake = 0.0f;
    }

    public enum AttackOrigin { CastPoint, PlayerCenter }

    [Header("--- RIFERIMENTI ---")]
    public Transform visualRoot;
    public Transform castPoint;
    public Animator animator;
    public CinemachineImpulseSource impulseSource;

    [Header("--- PARAMETRI FISICI ---")]
    public float moveSpeed = 6f;
    public float exhaustedSpeedFactor = 0.5f;
    public float rotationSpeed = 25f;
    public float gravity = -9.81f;

    [Header("--- ATTACCHI (Stamina Based) ---")]

    [SerializeField]
    public AttackConfig greenConfig = new AttackConfig
    {
        damage = 12f,
        knockback = 2f,
        staminaCost = 15f,
        cooldown = 0.3f,
        duration = 0.15f,
        screenShake = 0.1f,
        forwardOffset = 0.5f
    };

    [SerializeField]
    public AttackConfig blueConfig = new AttackConfig
    {
        damage = 25f,
        knockback = 6f,
        staminaCost = 30f,
        cooldown = 0.6f,
        duration = 0.3f,
        screenShake = 0.3f,
        startScale = Vector3.one,
        endScale = new Vector3(3, 1, 3)
    };

    [SerializeField]
    public AttackConfig redConfig = new AttackConfig
    {
        damage = 60f,
        knockback = 18f,
        staminaCost = 60f,
        cooldown = 1.0f,
        duration = 0.4f,
        screenShake = 2.5f,
        startScale = Vector3.one,
        endScale = Vector3.one * 3.5f
    };

    [SerializeField]
    public AttackConfig yellowConfig = new AttackConfig
    {
        damage = 0f,
        knockback = 10f,
        staminaCost = 10f,
        cooldown = 0.5f,
        duration = 0.5f,
        startScale = Vector3.one * 1.5f
    };

    [Header("--- BILANCIAMENTO EXTRA ---")]
    public float minChargeTimeRed = 0.5f;
    public float parryWindowDuration = 0.5f;
    [Tooltip("Stamina consumata al secondo per tenere lo scudo alzato.")]
    public float shieldDrainPerSecond = 15f;
    [Tooltip("Stamina persa quando si blocca un colpo (non parry).")]
    public float shieldHitPenalty = 10f;
    [Tooltip("Stamina recuperata quando si esegue un Parry Perfetto.")]
    public float parryStaminaReward = 30f;

    [Header("--- SISTEMI ---")]
    public SpellCasterSystem spellSystem;

    [Header("Stato")]
    public bool isChanneling = false;

    // Componenti
    private PlayerStats stats;
    private CharacterController controller;
    private GameInputs inputActions;
    private Camera mainCamera;

    // Input
    private Vector2 moveInput;
    private Vector2 mousePos;
    private Vector3 velocity;

    private float globalActionTimer = 0f;

    private enum PlayerState { Normal, Attacking, ChargingRed, GuardingYellow, CastingSpell }
    [SerializeField] private PlayerState currentState = PlayerState.Normal;

    private float chargeStartTime;
    private float guardStartTime;
    private bool redChargeReadyFeedbackPlayed = false;
    private GameObject activeShieldInstance;
    private GameObject activeChargeVFX;
    private static readonly int AnimVelocityZ = Animator.StringToHash("VelocityZ");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        mainCamera = Camera.main;
        inputActions = new GameInputs();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Look.performed += ctx => mousePos = ctx.ReadValue<Vector2>();

        inputActions.Player.Skill1.performed += ctx => OnGreenInput();
        inputActions.Player.Skill2.performed += ctx => OnBlueInput();
        inputActions.Player.Skill3.started += ctx => OnRedInputStart();
        inputActions.Player.Skill3.canceled += ctx => OnRedInputEnd();
        inputActions.Player.Skill4.started += ctx => OnYellowInputStart();
        inputActions.Player.Skill4.canceled += ctx => OnYellowInputEnd();
        inputActions.Player.Cast.performed += ctx => PerformCast();
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Update()
    {
        if (globalActionTimer > 0) globalActionTimer -= Time.deltaTime;

        HandleShieldLogic();
        UpdateMovementAnimation();

        if (currentState == PlayerState.ChargingRed) HandleRedChargeFeedback();

        ApplyGravity();

        switch (currentState)
        {
            case PlayerState.Normal:
                HandleMovement();
                HandleRotation();
                break;
            case PlayerState.Attacking:
            case PlayerState.ChargingRed:
            case PlayerState.GuardingYellow:
            case PlayerState.CastingSpell:
                HandleRotation();
                break;
        }
    }

    // --- COMUNICAZIONE ESTERNA ---
    public void SetChanneling(bool active)
    {
        isChanneling = active;
    }

    // --- INPUT WRAPPERS ---

    bool CanPerformAction()
    {
        if (globalActionTimer > 0) return false;
        if (isChanneling) return false;

        // UNICO BLOCCO: Sei già esausto?
        if (stats.isExhausted) return false;

        // Se hai 0 stamina ma non sei esausto (caso raro), blocchiamo per evitare loop
        if (stats.currentStamina <= 0) return false;

        return true;
    }

    void OnGreenInput()
    {
        // Non passiamo più il costo al controllo, verifichiamo solo lo stato
        if (!CanPerformAction()) return;
        PerformGreenAttack();
    }

    void OnBlueInput()
    {
        if (!CanPerformAction()) return;
        PerformBlueAttack();
    }

    void OnRedInputStart()
    {
        if (!CanPerformAction()) return;
        StartChargingRed();
    }
    // ReleaseRedAttack consuma la stamina DOPO, quindi va bene così com'è.

    void OnRedInputEnd() { ReleaseRedAttack(); }

    void OnYellowInputStart()
    {
        if (!CanPerformAction()) return;
        StartGuardingInput();
    }
    void OnYellowInputEnd() { StopGuardingInput(); }

    // --- LOGICA AZIONI ---

    void PerformGreenAttack()
    {
        if (currentState != PlayerState.Normal) return;
        stats.ConsumeStamina(greenConfig.staminaCost);
        globalActionTimer = greenConfig.cooldown;
        StartCoroutine(GreenPokeRoutine());
    }

    IEnumerator GreenPokeRoutine()
    {
        currentState = PlayerState.Attacking;
        spellSystem.PushNote(0);
        GameObject poke = SpawnAttackVisual(greenConfig, Color.green);
        float elapsed = 0;
        Vector3 startPos = poke.transform.localPosition;
        Vector3 targetPos = startPos + (Vector3.forward * greenConfig.forwardOffset);
        while (elapsed < greenConfig.duration)
        {
            float t = elapsed / greenConfig.duration;
            poke.transform.localPosition = Vector3.Lerp(startPos, targetPos, Mathf.PingPong(t * 2, 1));
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(poke);
        currentState = PlayerState.Normal;
    }

    void PerformBlueAttack()
    {
        if (currentState != PlayerState.Normal) return;
        stats.ConsumeStamina(blueConfig.staminaCost);
        globalActionTimer = blueConfig.cooldown;
        StartCoroutine(BlueSlashRoutine());
    }

    IEnumerator BlueSlashRoutine()
    {
        currentState = PlayerState.Attacking;
        spellSystem.PushNote(1);
        AttackConfig cfg = blueConfig;
        Transform root = (cfg.originType == AttackOrigin.PlayerCenter) ? visualRoot : castPoint;
        GameObject pivot = new GameObject("SlashPivot");
        pivot.transform.position = root.position; pivot.transform.rotation = root.rotation; pivot.transform.SetParent(root);
        GameObject slash = Instantiate(cfg.prefab, pivot.transform);
        slash.transform.localPosition = new Vector3(0, cfg.heightOffset, cfg.forwardOffset); slash.transform.localScale = cfg.startScale;

        Collider col = slash.GetComponent<Collider>(); if (col == null) col = slash.GetComponentInChildren<Collider>();
        if (col != null) { col.isTrigger = true; var hb = col.gameObject.AddComponent<BasicAttackHitbox>(); hb.Setup(cfg.damage, cfg.knockback); }

        SetColor(slash, Color.cyan);
        float elapsed = 0;
        while (elapsed < cfg.duration)
        {
            float t = elapsed / cfg.duration;
            pivot.transform.localRotation = Quaternion.Euler(0, Mathf.Lerp(90f, -90f, t), 0);
            slash.transform.localScale = Vector3.Lerp(cfg.startScale, cfg.endScale, Mathf.Sin(t * Mathf.PI));
            elapsed += Time.deltaTime; yield return null;
        }
        Destroy(pivot);
        currentState = PlayerState.Normal;
    }

    void StartChargingRed()
    {
        if (currentState != PlayerState.Normal) return;
        currentState = PlayerState.ChargingRed;
        chargeStartTime = Time.time;
        redChargeReadyFeedbackPlayed = false;

        if (redConfig.chargePrefab != null) activeChargeVFX = Instantiate(redConfig.chargePrefab, castPoint);
        else { activeChargeVFX = GameObject.CreatePrimitive(PrimitiveType.Sphere); DestroyImmediate(activeChargeVFX.GetComponent<Collider>()); activeChargeVFX.transform.SetParent(castPoint); }
        activeChargeVFX.transform.localPosition = Vector3.zero; activeChargeVFX.transform.localScale = Vector3.one * 0.15f; SetColor(activeChargeVFX, new Color(0.5f, 0, 0, 0.5f));
    }

    void HandleRedChargeFeedback()
    {
        if (activeChargeVFX == null) return;
        float chargeDuration = Time.time - chargeStartTime;
        activeChargeVFX.transform.localPosition = Random.insideUnitSphere * 0.01f;

        if (chargeDuration >= minChargeTimeRed && !redChargeReadyFeedbackPlayed)
        {
            redChargeReadyFeedbackPlayed = true;
            SetColor(activeChargeVFX, Color.red);
            activeChargeVFX.transform.localScale *= 2.0f;
        }
    }

    void ReleaseRedAttack()
    {
        if (activeChargeVFX != null) Destroy(activeChargeVFX);
        if (currentState != PlayerState.ChargingRed) return;

        if (Time.time - chargeStartTime >= minChargeTimeRed)
        {
            stats.ConsumeStamina(redConfig.staminaCost);
            globalActionTimer = redConfig.cooldown;
            StartCoroutine(RedSmashRoutine());
        }
        else
        {
            currentState = PlayerState.Normal;
        }
    }

    IEnumerator RedSmashRoutine()
    {
        spellSystem.PushNote(2);
        AttackConfig cfg = redConfig;
        GameObject smash = SpawnAttackVisual(cfg, Color.red);
        float elapsed = 0; bool impactPlayed = false;
        while (elapsed < cfg.duration)
        {
            float t = elapsed / cfg.duration;
            smash.transform.localScale = Vector3.Lerp(cfg.startScale, cfg.endScale, t * t);
            smash.transform.Translate(Vector3.forward * (Time.deltaTime * 5f), Space.Self);
            if (t > 0.8f && !impactPlayed) { impactPlayed = true; if (cfg.impactVfx) Instantiate(cfg.impactVfx, smash.transform.position, Quaternion.identity); }
            elapsed += Time.deltaTime; yield return null;
        }
        Destroy(smash);
        currentState = PlayerState.Normal;
    }

    // --- LOGICA GIALLA (SCUDO STAMINA) ---

    void StartGuardingInput()
    {
        if (currentState != PlayerState.Normal) return;
        currentState = PlayerState.GuardingYellow;
        guardStartTime = Time.time;
        stats.isShielded = true; // Attiva flag stats

        activeShieldInstance = SpawnAttackVisual(yellowConfig, Color.clear, true);
        ParryShield parry = activeShieldInstance.GetComponent<ParryShield>();
        if (parry == null) parry = activeShieldInstance.AddComponent<ParryShield>();
        parry.Setup(this, parryWindowDuration);

        activeShieldInstance.transform.localScale = yellowConfig.startScale;
    }

    void StopGuardingInput()
    {
        if (currentState != PlayerState.GuardingYellow) return;

        spellSystem.PushNote(3);
        globalActionTimer = 0.2f;
        stats.isShielded = false; // Disattiva flag stats

        if (activeShieldInstance != null) Destroy(activeShieldInstance);
        currentState = PlayerState.Normal;
    }

    void HandleShieldLogic()
    {
        if (currentState == PlayerState.GuardingYellow)
        {
            stats.ConsumeStaminaOverTime(shieldDrainPerSecond);
            if (stats.isExhausted)
            {
                StopGuardingInput();
                Debug.Log("Guardia rotta per esaurimento stamina!");
            }
        }
    }

    // --- CALLBACKS DALLO SCUDO ---

    public void OnParrySuccess()
    {
        // RICOMPENSA: Recupera stamina e azzera il global timer
        if (stats != null)
        {
            // Aggiungiamo stamina manualmente accedendo alla variabile pubblica (o tramite Heal se preferisci)
            stats.currentStamina = Mathf.Min(stats.currentStamina + parryStaminaReward, stats.maxStamina);
            stats.OnStatsChanged?.Invoke(); // Aggiorna UI
        }

        globalActionTimer = 0f; // Permette di agire subito dopo il parry
        Debug.Log("<color=green>PARRY! Stamina Recuperata.</color>");
    }

    public void OnShieldHit()
    {
        // PUNIZIONE: Perdi stamina extra quando blocchi un colpo (senza parry)
        if (stats != null)
        {
            stats.ConsumeStamina(shieldHitPenalty);
        }

        // Feedback fisico
        if (impulseSource != null) impulseSource.GenerateImpulse(0.2f);
    }

    // --- MOVEMENT & UTILS ---

    void HandleMovement()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);

        // Normalizza solo se supera 1 (per pad analogici)
        if (move.magnitude > 1f) move.Normalize();

        float speed = moveSpeed;

        // LOGICA RALLENTAMENTI CUMULATIVI
        if (isChanneling)
            speed *= 0.3f; // Molto lento mentre usi il raggio

        else if (currentState == PlayerState.GuardingYellow)
            speed *= 0.5f; // Lento mentre pari

        // RALLENTAMENTO DA STANCHEZZA (Prioritario)
        if (stats.isExhausted)
        {
            speed *= exhaustedSpeedFactor; // Es. 0.4x velocità normale

            // Opzionale: impedisci di correre, forza la camminata nell'animator
            if (animator) animator.speed = 0.5f; // Rallenta anche l'animazione di corsa
        }
        else
        {
            if (animator) animator.speed = 1f; // Ripristina velocità animazione
        }

        controller.Move(move * speed * Time.deltaTime);
    }

    GameObject SpawnAttackVisual(AttackConfig config, Color color, bool isShield = false)
    {
        Transform root = (config.originType == AttackOrigin.PlayerCenter) ? visualRoot : castPoint;
        GameObject obj = Instantiate(config.prefab, root.position, root.rotation);

        Collider col = obj.GetComponent<Collider>();
        if (col == null) col = obj.GetComponentInChildren<Collider>();
        if (col) col.isTrigger = true;

        if (!isShield && col != null)
        {
            // Aggiungi script Hitbox
            var hitbox = col.gameObject.AddComponent<BasicAttackHitbox>();

            // --- FIX QUI: Passiamo SIA il danno CHE il knockback ---
            hitbox.Setup(config.damage, config.knockback);
            // ------------------------------------------------------
        }

        obj.transform.SetParent(root);
        obj.transform.localPosition = new Vector3(0, config.heightOffset, config.forwardOffset);
        obj.transform.localRotation = Quaternion.identity;

        if (!isShield) SetColor(obj, color);
        if (config.screenShake > 0 && impulseSource != null) impulseSource.GenerateImpulse(config.screenShake);
        return obj;
    }

    void SetColor(GameObject obj, Color c)
    {
        var rend = obj.GetComponent<Renderer>(); if (rend) rend.material.color = c;
        foreach (var r in obj.GetComponentsInChildren<Renderer>()) r.material.color = c;
    }

    void HandleRotation()
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 lookDir = hitPoint - visualRoot.position; lookDir.y = 0;
            if (lookDir != Vector3.zero) visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, Quaternion.LookRotation(lookDir), rotationSpeed * Time.deltaTime);
        }
    }

    void ApplyGravity() { if (!controller.isGrounded) controller.Move(Vector3.up * gravity * Time.deltaTime); }

    void UpdateMovementAnimation()
    {
        if (animator)
        {
            Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);
            Vector3 localDir = visualRoot.InverseTransformDirection(inputDir);
            animator.SetFloat(AnimVelocityZ, (inputDir.magnitude > 0.1f) ? localDir.z : 0f, 0.1f, Time.deltaTime);
        }
    }

    void PerformCast() { if (currentState != PlayerState.Normal || !spellSystem.HasSpellReady()) return; currentState = PlayerState.CastingSpell; animator.SetTrigger("Cast"); StartCoroutine(CastSafetyRoutine(0.4f)); }
    IEnumerator CastSafetyRoutine(float delay) { yield return new WaitForSeconds(delay); if (currentState == PlayerState.CastingSpell) { OnSpellFireFrame(); yield return new WaitForSeconds(0.2f); OnCastEndFrame(); } }
    public void OnSpellFireFrame() { if (currentState == PlayerState.CastingSpell) spellSystem.FireCurrentSpell(castPoint); }
    public void OnCastEndFrame() { currentState = PlayerState.Normal; }
}