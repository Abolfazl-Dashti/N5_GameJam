using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private StaggerData staggerData;

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerDiscHandler playerDiscHandler;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private DiscController discController;

    [Tooltip("Transform used as the forward-facing dash origin point")]
    [SerializeField] private Transform dashOrigin;

    [Header("Events")]
    public UnityEvent onDashStarted;
    public UnityEvent onDashEnded;
    public UnityEvent<Transform> onOpponentStaggered;
    public UnityEvent onSelfStaggered;

    // Dash state
    private bool _isDashing;
    private float _dashTimer;
    private Vector3 _dashDirection = Vector3.forward;

    // Cooldown
    private bool _dashOnCooldown;
    private float _dashCooldownTimer;

    // Collision check interval
    private float _collisionCheckTimer;

    // Hit Buffer
    private Collider[] _hitCollidersBuffer = new Collider[16];
    private bool _hasHitOpponentThisDash = false;

    // Input Actions
    private Main_InputSystem _inputActions;

    private void Awake()
    {
        if (!playerController) playerController = GetComponent<PlayerController>();
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        BindDashInput();
    }

    private void OnDisable()
    {
        UnbindDashInput();
    }

    private void BindDashInput()
    {
        if (playerController && playerController.MainInputSystem != null)
        {
            _inputActions = playerController.MainInputSystem;
        }
        else if (_inputActions == null)
        {
            _inputActions = new Main_InputSystem();
        }

        _inputActions.Enable();
        _inputActions.Player.Dash.started -= OnDashStarted;
        _inputActions.Player.Dash.started += OnDashStarted;
    }

    private void UnbindDashInput()
    {
        if (_inputActions != null)
        {
            _inputActions.Player.Dash.started -= OnDashStarted;
        }
    }

    private void OnDashStarted(InputAction.CallbackContext context)
    {
        TryStartDash();
    }

    public void TryStartDash()
    {
        if (!staggerData)
        {
            Debug.LogError("[PlayerCombat] StaggerData Asset is NOT assigned in Inspector!", this);
            return;
        }

        if (!rb)
        {
            Debug.LogError("[PlayerCombat] Rigidbody is NOT assigned in Inspector!", this);
            return;
        }

        if (_isDashing) return;

        if (_dashOnCooldown)
        {
            Debug.Log($"[PlayerCombat] Dash on cooldown ({_dashCooldownTimer:F1}s left)");
            return;
        }

        if (playerController && playerController.IsStaggered())
        {
            Debug.Log("[PlayerCombat] Cannot dash while staggered!");
            return;
        }

        StartDash();
    }

    private void StartDash()
    {
        _isDashing = true;
        _dashTimer = 0f;
        _hasHitOpponentThisDash = false;
        _collisionCheckTimer = 0f;

        _dashDirection = GetFlatForward();

        // صفر کردن سرعت قبلی Y/XZ برای حرکت دش شفاف و بدون لگد
        rb.linearVelocity = Vector3.zero;

        onDashStarted.Invoke();

        Debug.Log($"[PlayerCombat] {gameObject.name} DASH STARTED! Speed: {staggerData.dashSpeed}");
    }

    private void Update()
    {
        TickDashCooldown();
        
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame)
        {
            TryStartDash();
        }
    }

    private void FixedUpdate()
    {
        if (_isDashing)
        {
            TickDash();
        }
    }

    private void TickDash()
    {
        _dashTimer += Time.fixedDeltaTime;
        
        // اعمال مستقیم سرعت Dash به فیزیک
        rb.linearVelocity = new Vector3(
            _dashDirection.x * staggerData.dashSpeed, 
            rb.linearVelocity.y, 
            _dashDirection.z * staggerData.dashSpeed
        );

        _collisionCheckTimer += Time.fixedDeltaTime;
        if (_collisionCheckTimer >= staggerData.collisionCheckInterval)
        {
            _collisionCheckTimer = 0f;
            CheckForOpponentCollision();
        }

        if (_dashTimer >= staggerData.dashDuration)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        _isDashing = false;
        _dashTimer = 0f;

        // افت سرعت جهت جلوگیری از پرتاب ناگهانی بعد از دش
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y, rb.linearVelocity.z * 0.2f);

        _dashOnCooldown = true;
        _dashCooldownTimer = staggerData.dashCooldown;

        onDashEnded.Invoke();
        Debug.Log($"[PlayerCombat] Dash ended. Cooldown started ({staggerData.dashCooldown}s)");
    }

    private void CheckForOpponentCollision()
    {
        if (_hasHitOpponentThisDash) return;

        Vector3 checkOrigin = dashOrigin ? dashOrigin.position : transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(checkOrigin, staggerData.dashHitRadius, _hitCollidersBuffer,
            staggerData.opponentLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hitCollidersBuffer[i];
            
            // عدم برخورد با خود پلیر
            if (hitCollider.transform.root == transform.root) continue;
            
            IStaggerable staggerable = hitCollider.GetComponentInParent<IStaggerable>();
            if (staggerable == null || staggerable.IsStaggered()) continue;

            PlayerDiscHandler opponentDiscHandler = hitCollider.GetComponentInParent<PlayerDiscHandler>();

            ApplyStaggerToOpponent(staggerable, opponentDiscHandler, hitCollider.transform.root);

            _hasHitOpponentThisDash = true;
            EndDash(); // اتمام دش پس از اولین برخورد موفق
            break;
        }
    }

    private void ApplyStaggerToOpponent(IStaggerable staggerable, PlayerDiscHandler opponentDiscHandler, Transform opponentTransform)
    {
        Vector3 knockBackDirection = (opponentTransform.position - transform.position).normalized;

        // اگر حریف دیسک را در دست دارد، دیسک رها شده و پرتاب می‌شود
        if (opponentDiscHandler && opponentDiscHandler.IsHoldingDisc())
        {
            opponentDiscHandler.OnDiscLost();

            if (discController)
            {
                Vector3 discKnockAway = (knockBackDirection + Vector3.up * staggerData.discKnockBackUpward).normalized;
                discController.DiscForceRelease(discKnockAway * staggerData.discKnockBackForce);
            }
        }

        // اعمال فلج/گیج شدن به حریف
        staggerable.ApplyStagger(knockBackDirection, staggerData.discKnockBackForce);
        onOpponentStaggered.Invoke(opponentTransform);

        Debug.Log($"[PlayerCombat] SUCCESSFULLY STAGGERED OPPONENT: {opponentTransform.name}");
    }

    private void TickDashCooldown()
    {
        if (!_dashOnCooldown) return;

        _dashCooldownTimer -= Time.deltaTime;

        if (_dashCooldownTimer <= 0f)
        {
            _dashOnCooldown = false;
            _dashCooldownTimer = 0f;
            Debug.Log("[PlayerCombat] Dash is ready!");
        }
    }

    public float GetStaggerDuration() => staggerData ? staggerData.staggerDuration : 1.5f;
    public bool IsDashing() => _isDashing;
    public bool IsDashOnCooldown() => _dashOnCooldown;

    private Vector3 GetFlatForward()
    {
        Transform camTransform = cameraTransform ? cameraTransform : transform;
        Vector3 flat = camTransform.forward;
        flat.y = 0f;
        return flat.sqrMagnitude < 0.001f ? transform.forward : flat.normalized;
    }

    public void TriggerSelfStaggeredEvent() => onSelfStaggered.Invoke();
}