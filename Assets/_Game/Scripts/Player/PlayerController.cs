using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [System.Serializable]
    public class AttackConfig
    {
        [Header("Visuals")]
        public GameObject prefab;
        public GameObject chargePrefab;
        [Tooltip("VFX spawnato all'impatto o fine estensione (es. Polvere/Detriti)")]
        public GameObject impactVfx; // <--- NUOVO CAMPO

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

    [Header("--- PARAMETRI FISICI ---")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 25f;
    public float gravity = -9.81f;

    [Header("--- CONFIGURAZIONE ATTACCHI ---")]
    public AttackConfig greenConfig;
    public AttackConfig blueConfig;
    public AttackConfig redConfig;
    public AttackConfig yellowConfig;

    [Header("--- BILANCIAMENTO ---")]
    public float minChargeTimeRed = 0.5f;
    public float tapThresholdYellow = 0.2f;

    [Header("--- SISTEMI ---")]
    public SpellCasterSystem spellSystem;
    public CinemachineImpulseSource impulseSource;

    // Stati e Variabili interne
    private CharacterController controller;
    private GameInputs inputActions;
    private Camera mainCamera;
    private Vector2 moveInput;
    private Vector2 mousePos;
    private Vector3 velocity;

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
        mainCamera = Camera.main;
        inputActions = new GameInputs();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Look.performed += ctx => mousePos = ctx.ReadValue<Vector2>();

        inputActions.Player.Skill1.performed += ctx => PerformGreenAttack();
        inputActions.Player.Skill2.performed += ctx => PerformBlueAttack();
        inputActions.Player.Skill3.started += ctx => StartChargingRed();
        inputActions.Player.Skill3.canceled += ctx => ReleaseRedAttack();
        inputActions.Player.Skill4.started += ctx => StartGuardingInput();
        inputActions.Player.Skill4.canceled += ctx => StopGuardingInput();
        inputActions.Player.Cast.performed += ctx => PerformCast();
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Update()
    {
        UpdateMovementAnimation();

        if (currentState == PlayerState.ChargingRed) HandleRedChargeFeedback();

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
                ApplyGravity();
                HandleRotation();
                break;
        }
    }

    // --- HELPER SPAWN (VFX INCLUSO) ---
    void SpawnImpactVFX(AttackConfig config, Vector3 position, Quaternion rotation)
    {
        if (config.impactVfx != null)
        {
            GameObject vfx = Instantiate(config.impactVfx, position, rotation);
            Destroy(vfx, 2.0f); // Auto-distruzione dopo 2 secondi
        }
    }

    GameObject SpawnAttackVisual(AttackConfig config, Color color)
    {
        Transform root = (config.originType == AttackOrigin.PlayerCenter) ? visualRoot : castPoint;
        GameObject obj = Instantiate(config.prefab, root.position, root.rotation);

        var col = obj.GetComponent<Collider>();
        if (col) col.isTrigger = true;

        obj.transform.SetParent(root);
        obj.transform.localPosition = new Vector3(0, config.heightOffset, config.forwardOffset);
        obj.transform.localRotation = Quaternion.identity;

        SetColor(obj, color);

        if (config.screenShake > 0 && impulseSource != null)
            impulseSource.GenerateImpulse(config.screenShake);

        return obj;
    }

    void SetColor(GameObject obj, Color c)
    {
        var rend = obj.GetComponent<Renderer>();
        if (rend) rend.material.color = c;
        foreach (var r in obj.GetComponentsInChildren<Renderer>()) r.material.color = c;
    }

    // --- ATTACCHI ---

    void PerformGreenAttack()
    {
        if (currentState != PlayerState.Normal) return;
        StartCoroutine(GreenPokeRoutine());
    }

    IEnumerator GreenPokeRoutine()
    {
        currentState = PlayerState.Attacking;
        spellSystem.PushNote(0);
        AttackConfig cfg = greenConfig;
        GameObject poke = SpawnAttackVisual(cfg, Color.green);

        float elapsed = 0;
        Vector3 startPos = poke.transform.localPosition;
        Vector3 targetPos = startPos + (Vector3.forward * 1.5f);

        while (elapsed < cfg.duration)
        {
            float t = elapsed / cfg.duration;
            float pingPong = Mathf.PingPong(t * 2, 1);
            poke.transform.localScale = Vector3.Lerp(cfg.startScale, cfg.endScale, pingPong);
            poke.transform.localPosition = Vector3.Lerp(startPos, targetPos, pingPong);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(poke);
        currentState = PlayerState.Normal;
    }

    void PerformBlueAttack()
    {
        if (currentState != PlayerState.Normal) return;
        StartCoroutine(BlueSlashRoutine());
    }

    IEnumerator BlueSlashRoutine()
    {
        currentState = PlayerState.Attacking;
        spellSystem.PushNote(1);
        AttackConfig cfg = blueConfig;
        Transform root = (cfg.originType == AttackOrigin.PlayerCenter) ? visualRoot : castPoint;

        GameObject pivot = new GameObject("SlashPivot");
        pivot.transform.position = root.position;
        pivot.transform.rotation = root.rotation;
        pivot.transform.SetParent(root);

        GameObject slash = Instantiate(cfg.prefab, pivot.transform);
        slash.transform.localPosition = new Vector3(0, cfg.heightOffset, cfg.forwardOffset);
        slash.transform.localScale = cfg.startScale;

        var col = slash.GetComponent<Collider>();
        if (col) col.isTrigger = true;
        SetColor(slash, Color.cyan);

        if (cfg.screenShake > 0 && impulseSource != null) impulseSource.GenerateImpulse(cfg.screenShake);

        float startAngle = 90f;
        float endAngle = -90f;
        float elapsed = 0;

        while (elapsed < cfg.duration)
        {
            float t = elapsed / cfg.duration;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
            pivot.transform.localRotation = Quaternion.Euler(0, currentAngle, 0);
            slash.transform.localScale = Vector3.Lerp(cfg.startScale, cfg.endScale, Mathf.Sin(t * Mathf.PI));
            elapsed += Time.deltaTime;
            yield return null;
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
        else
        {
            activeChargeVFX = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(activeChargeVFX.GetComponent<Collider>());
            activeChargeVFX.transform.SetParent(castPoint);
        }
        activeChargeVFX.transform.localPosition = Vector3.zero;
        activeChargeVFX.transform.localScale = Vector3.one * 0.15f;
        SetColor(activeChargeVFX, new Color(0.5f, 0, 0, 0.5f));
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
        if (Time.time - chargeStartTime >= minChargeTimeRed) StartCoroutine(RedSmashRoutine());
        else currentState = PlayerState.Normal;
    }

    IEnumerator RedSmashRoutine()
    {
        spellSystem.PushNote(2);
        AttackConfig cfg = redConfig;
        GameObject smash = SpawnAttackVisual(cfg, Color.red);

        float elapsed = 0;
        Vector3 finalScale = cfg.endScale;
        Vector3 initialScale = cfg.startScale;
        bool impactPlayed = false;

        while (elapsed < cfg.duration)
        {
            float t = elapsed / cfg.duration;
            float tEase = t * t;
            smash.transform.localScale = Vector3.Lerp(initialScale, finalScale, tEase);
            smash.transform.Translate(Vector3.forward * (Time.deltaTime * 5f), Space.Self);

            // SPAWN VFX DETRITI (Al 90% dell'estensione)
            if (t > 0.8f && !impactPlayed)
            {
                impactPlayed = true;
                // Spawna alla punta del cilindro
                Vector3 impactPos = smash.transform.position + (smash.transform.forward * (finalScale.z / 2));
                // Metti a terra
                impactPos.y = 0.1f;
                SpawnImpactVFX(cfg, impactPos, Quaternion.LookRotation(Vector3.up));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(smash);
        currentState = PlayerState.Normal;
    }

    void StartGuardingInput()
    {
        if (currentState != PlayerState.Normal) return;
        guardStartTime = Time.time;
        currentState = PlayerState.GuardingYellow;
        AttackConfig cfg = yellowConfig;
        activeShieldInstance = SpawnAttackVisual(cfg, new Color(1f, 1f, 0f, 0.3f));
        activeShieldInstance.transform.localScale = cfg.startScale;
    }

    void StopGuardingInput()
    {
        if (currentState != PlayerState.GuardingYellow) return;
        float duration = Time.time - guardStartTime;
        if (duration <= tapThresholdYellow) PerformYellowRepel();
        else spellSystem.PushNote(3);
        if (activeShieldInstance != null) Destroy(activeShieldInstance);
        currentState = PlayerState.Normal;
    }

    void PerformYellowRepel()
    {
        spellSystem.PushNote(3);

        // Spawn Onda d'urto alla base
        AttackConfig cfg = yellowConfig;
        SpawnImpactVFX(cfg, visualRoot.position + Vector3.up * 0.1f, Quaternion.Euler(90, 0, 0));

        if (activeShieldInstance != null)
        {
            activeShieldInstance.transform.SetParent(null);
            StartCoroutine(ShieldPulseRoutine(activeShieldInstance));
            activeShieldInstance = null;
        }
    }

    IEnumerator ShieldPulseRoutine(GameObject shieldObj)
    {
        AttackConfig cfg = yellowConfig;
        float elapsed = 0;
        Vector3 start = shieldObj.transform.localScale;
        if (cfg.screenShake > 0 && impulseSource != null) impulseSource.GenerateImpulse(cfg.screenShake);
        while (elapsed < 0.15f)
        {
            float t = elapsed / 0.15f;
            shieldObj.transform.localScale = Vector3.Lerp(start, cfg.endScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(shieldObj);
    }

    void UpdateMovementAnimation()
    {
        if (animator == null) return;
        if (currentState != PlayerState.Normal) { animator.SetFloat(AnimVelocityZ, 0f, 0.1f, Time.deltaTime); return; }

        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);
        Vector3 localDir = visualRoot.InverseTransformDirection(inputDir);
        float forwardAmount = localDir.z;
        if (inputDir.magnitude > 0.1f && Mathf.Abs(forwardAmount) < 0.2f) forwardAmount = 0.5f;

        animator.SetFloat(AnimVelocityZ, forwardAmount, 0.1f, Time.deltaTime);
    }

    void HandleMovement()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        if (move.magnitude > 1f) move.Normalize();
        controller.Move(move * moveSpeed * Time.deltaTime);
        ApplyGravity();
    }

    void HandleRotation()
    {
        if (activeChargeVFX != null)
            activeChargeVFX.transform.position = castPoint.position + (Random.insideUnitSphere * 0.01f);

        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 lookDir = hitPoint - visualRoot.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, Quaternion.LookRotation(lookDir), rotationSpeed * Time.deltaTime);
        }
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }


    void PerformCast()
    {
        // 1. Controlli: Stato e Spell Pronta
        if (currentState != PlayerState.Normal) return;

        if (!spellSystem.HasSpellReady())
        {
            Debug.Log("Nessuna spell pronta!");
            return;
        }

        // 2. Blocca il player
        currentState = PlayerState.CastingSpell;

        // 3. Avvia Animazione
        animator.SetTrigger("Cast"); // Assicurati che il trigger nell'Animator si chiami "Cast"

        // 4. RETE DI SICUREZZA
        // Se l'animazione non ha l'evento o si blocca, spara comunque dopo 0.4s
        StartCoroutine(CastSafetyRoutine(0.4f));
    }

    // Coroutine di sicurezza
    IEnumerator CastSafetyRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Se siamo ancora bloccati nel casting...
        if (currentState == PlayerState.CastingSpell)
        {
            // Forza il fuoco
            OnSpellFireFrame();
            // Aspetta un attimo e sblocca
            yield return new WaitForSeconds(0.2f);
            OnCastEndFrame();
        }
    }

    // --- EVENTI DI ANIMAZIONE (Da aggiungere nella clip o chiamati dalla SafetyRoutine) ---

    public void OnSpellFireFrame()
    {
        // Evita doppi spari
        if (currentState != PlayerState.CastingSpell) return;

        // Spara usando il castPoint (punta del bastone)
        spellSystem.FireCurrentSpell(castPoint);
    }

    public void OnCastEndFrame()
    {
        // Torna normale
        currentState = PlayerState.Normal;
    }
}