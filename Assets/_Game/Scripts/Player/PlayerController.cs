using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
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
    public CinemachineImpulseSource impulseSource; // Usato per attacchi

    [Header("--- JUICE (CAMERA SHAKE DANNO) ---")]
    [Tooltip("Intensità del tremolio quando il player prende danno.")]
    public float damageShakeIntensity = 2f;
    [Tooltip("Durata del tremolio.")]
    public float damageShakeDuration = 0.2f;

    [Header("--- PARAMETRI FISICI ---")]
    public float moveSpeed = 6f;
    public float exhaustedSpeedFactor = 0.5f;
    public float rotationSpeed = 25f;
    public float gravity = -9.81f;

    // --- ATTACCHI CONFIG ---
    [Header("--- ATTACCHI (Stamina Based) ---")]
    [SerializeField] public AttackConfig greenConfig;
    [SerializeField] public AttackConfig blueConfig;
    [SerializeField] public AttackConfig redConfig;
    [SerializeField] public AttackConfig yellowConfig;

    [Header("--- BILANCIAMENTO EXTRA ---")]
    public float minChargeTimeRed = 0.5f;
    public float parryWindowDuration = 0.5f;
    public float shieldDrainPerSecond = 15f;
    public float shieldHitPenalty = 10f;
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

    // Riferimento al Noise della Cinemachine per lo shake manuale
    private CinemachineBasicMultiChannelPerlin cinemachineNoise;

    // Input
    private Vector2 moveInput;
    private Vector2 mousePos;
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

        // Setup Inputs...
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

    void Start()
    {
        // Setup Camera Shake (cerca la virtual camera attiva)
        var vCam = FindFirstObjectByType<CinemachineCamera>();
        if (vCam != null)
            cinemachineNoise = vCam.GetComponent<CinemachineBasicMultiChannelPerlin>();

        // Iscriviti all'evento danno per scuotere la camera
        if (stats) stats.OnTakeDamage += TriggerDamageShake;
    }

    void OnDestroy()
    {
        if (stats) stats.OnTakeDamage -= TriggerDamageShake;
        inputActions.Disable();
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

    // --- CAMERA SHAKE ---
    void TriggerDamageShake()
    {
        StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        if (cinemachineNoise)
        {
            cinemachineNoise.AmplitudeGain = damageShakeIntensity;
            yield return new WaitForSeconds(damageShakeDuration);
            cinemachineNoise.AmplitudeGain = 0f;
        }
    }

    // --- MOVEMENT ---
    void HandleMovement()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        if (move.magnitude > 1f) move.Normalize();

        float speed = moveSpeed;

        // APPLICA IL MOLTIPLICATORE DEI BUFF (NUOVO)
        if (stats != null) speed *= stats.speedMultiplier;

        // Rallentamenti cumulativi
        if (isChanneling) speed *= 0.3f;
        else if (currentState == PlayerState.GuardingYellow) speed *= 0.5f;

        if (stats.isExhausted)
        {
            speed *= exhaustedSpeedFactor;
            if (animator) animator.speed = 0.5f;
        }
        else
        {
            if (animator) animator.speed = 1f;
        }

        controller.Move(move * speed * Time.deltaTime);
    }

    // --- COMUNICAZIONE ESTERNA ---
    public void SetChanneling(bool active) => isChanneling = active;

    // --- INPUT WRAPPERS ---
    bool CanPerformAction()
    {
        if (globalActionTimer > 0) return false;
        if (isChanneling) return false;
        if (stats.isExhausted) return false;
        if (stats.currentStamina <= 0) return false;
        return true;
    }

    void OnGreenInput() { if (!CanPerformAction()) return; PerformGreenAttack(); }
    void OnBlueInput() { if (!CanPerformAction()) return; PerformBlueAttack(); }
    void OnRedInputStart() { if (!CanPerformAction()) return; StartChargingRed(); }
    void OnRedInputEnd() { ReleaseRedAttack(); }
    void OnYellowInputStart() { if (!CanPerformAction()) return; StartGuardingInput(); }
    void OnYellowInputEnd() { StopGuardingInput(); }

    // --- AZIONI ATTACCO (Logica Originale Mantenuta) ---

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

        // APPLICA DANNO MOLTIPLICATO
        float finalDmg = greenConfig.damage * stats.damageMultiplier;

        GameObject poke = SpawnAttackVisual(greenConfig, Color.green, finalDmg); // Overload con danno

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

        float finalDmg = cfg.damage * stats.damageMultiplier;

        Transform root = (cfg.originType == AttackOrigin.PlayerCenter) ? visualRoot : castPoint;
        GameObject pivot = new GameObject("SlashPivot");
        pivot.transform.position = root.position; pivot.transform.rotation = root.rotation; pivot.transform.SetParent(root);

        GameObject slash = Instantiate(cfg.prefab, pivot.transform);
        slash.transform.localPosition = new Vector3(0, cfg.heightOffset, cfg.forwardOffset); slash.transform.localScale = cfg.startScale;

        Collider col = slash.GetComponent<Collider>(); if (col == null) col = slash.GetComponentInChildren<Collider>();
        if (col != null) { col.isTrigger = true; var hb = col.gameObject.AddComponent<BasicAttackHitbox>(); hb.Setup(finalDmg, cfg.knockback); }

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

        float finalDmg = cfg.damage * stats.damageMultiplier;

        GameObject smash = SpawnAttackVisual(cfg, Color.red, finalDmg);
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

    // --- LOGICA GIALLA (SCUDO) ---
    void StartGuardingInput()
    {
        if (currentState != PlayerState.Normal) return;
        currentState = PlayerState.GuardingYellow;
        guardStartTime = Time.time;
        stats.isShielded = true;

        activeShieldInstance = SpawnAttackVisual(yellowConfig, Color.clear, -1, true); // -1 danno perché è scudo
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
        stats.isShielded = false;

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
                Debug.Log("Guardia rotta!");
            }
        }
    }

    public void OnParrySuccess()
    {
        if (stats != null)
        {
            stats.currentStamina = Mathf.Min(stats.currentStamina + parryStaminaReward, stats.maxStamina);
            stats.OnStatsChanged?.Invoke();
        }
        globalActionTimer = 0f;
        Debug.Log("<color=green>PARRY!</color>");
    }

    public void OnShieldHit()
    {
        if (stats != null) stats.ConsumeStamina(shieldHitPenalty);
        if (impulseSource != null) impulseSource.GenerateImpulse(0.2f);
    }

    // --- UTILS GRAFICI ---
    GameObject SpawnAttackVisual(AttackConfig config, Color color, float overrideDamage = -1, bool isShield = false)
    {
        Transform root = (config.originType == AttackOrigin.PlayerCenter) ? visualRoot : castPoint;
        GameObject obj = Instantiate(config.prefab, root.position, root.rotation);

        Collider col = obj.GetComponent<Collider>();
        if (col == null) col = obj.GetComponentInChildren<Collider>();
        if (col) col.isTrigger = true;

        if (!isShield && col != null)
        {
            var hitbox = col.gameObject.AddComponent<BasicAttackHitbox>();
            float dmg = (overrideDamage >= 0) ? overrideDamage : config.damage;
            hitbox.Setup(dmg, config.knockback);
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

    void PerformCast()
    {
        if (currentState != PlayerState.Normal) return;
        if (!spellSystem.HasSpellReady()) return;

        spellSystem.SpawnSuccessVFX(transform.position);
        currentState = PlayerState.CastingSpell;
        animator.SetTrigger("Cast");
        StartCoroutine(CastSafetyRoutine(0.4f));
    }

    IEnumerator CastSafetyRoutine(float delay) { yield return new WaitForSeconds(delay); if (currentState == PlayerState.CastingSpell) { OnSpellFireFrame(); yield return new WaitForSeconds(0.2f); OnCastEndFrame(); } }
    public void OnSpellFireFrame() { if (currentState == PlayerState.CastingSpell) spellSystem.FireCurrentSpell(castPoint); }
    public void OnCastEndFrame() { currentState = PlayerState.Normal; }
}