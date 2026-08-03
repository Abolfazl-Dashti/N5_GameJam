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

    [Tooltip("Transform used as the forward-facing dash origin point. " +
             "Usually the player root or a chest bone child.")]
    [SerializeField] private Transform dashOrigin;

    [Header("Events")]
    [Tooltip("Fires when the player starts a dash.")]
    public UnityEvent onDashStarted;

    [Tooltip("Fires when the dash ends (either completed or hit opponent).")]
    public UnityEvent onDashEnded;

    [Tooltip("Fires when this player successfully staggers an opponent. " +
             "Passes the staggered opponent's Transform.")]
    public UnityEvent<Transform> onOpponentStaggered;

    [Tooltip("Fires when this player is staggered by an opponent.")]
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

    // Tracks opponents already hit this dash — prevents double-stagger on same target
    private Collider[] _hitCollidersBuffer = new Collider[8];
    private bool _hasHitOpponentThisDash = false;

    // Input
    private Main_InputSystem _inputActions;
    
    private void Awake()
    {
        SetupInput();
    }

    private void OnEnable()
    {
        BindInputs();
    }

    private void OnDisable()
    {
        UnbindInputs();
    }

    private void Update()
    {
        TickDashCooldown();
    }

    private void FixedUpdate()
    {
        if (_isDashing)
        {
            TickDash();
        }
    }
    
    private void SetupInput()
    {
        // Share input instance from PlayerController to avoid duplication
        if (playerController && playerController.MainInputSystem != null)
        {
            _inputActions = playerController.MainInputSystem;
            return;
        }

        // Fallback
        _inputActions = new Main_InputSystem();
        _inputActions.Enable();
    }

    private void BindInputs()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Dash.started += OnDashStarted;
    }

    private void UnbindInputs()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Dash.started -= OnDashStarted;
    }
    
    private void OnDashStarted(InputAction.CallbackContext context)
    {
        TryStartDash();
    }
    
    /// Validates conditions and starts the dash if allowed
    private void TryStartDash()
    {
        if (_isDashing) return;
        if (_dashOnCooldown) return;

        // Cannot dash while staggered
        if (playerController && playerController.IsStaggered()) return;

        StartDash();
    }

    private void StartDash()
    {
        _isDashing = true;
        _dashTimer = 0f;
        _hasHitOpponentThisDash = false;
        _collisionCheckTimer = 0f;

        // Dash in the direction the player is currently facing (camera-flat forward)
        _dashDirection = GetFlatForward();

        // Cancel any existing velocity so dash direction is clean
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        onDashStarted.Invoke();

        Debug.Log($"PlayerCombat: {gameObject.name} dashed!");
    }
    
    /// Called every FixedUpdate while dashing
    /// Moves the player and checks for opponent collisions
    private void TickDash()
    {
        _dashTimer += Time.fixedDeltaTime;

        // Apply dash velocity directly — overrides normal movement
        rb.linearVelocity = new Vector3(_dashDirection.x * staggerData.dashSpeed, rb.linearVelocity.y,
            _dashDirection.z * staggerData.dashSpeed);

        // Periodically check for opponent hits during the dash window
        _collisionCheckTimer += Time.fixedDeltaTime;

        if (_collisionCheckTimer >= staggerData.collisionCheckInterval)
        {
            _collisionCheckTimer = 0f;
            CheckForOpponentCollision();
        }

        // End dash when duration expires
        if (_dashTimer >= staggerData.dashDuration)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        _isDashing = false;
        _dashTimer = 0f;

        // Bleed off dash speed — don't let player rocket away post-dash
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x * 0.3f,
            rb.linearVelocity.y,
            rb.linearVelocity.z * 0.3f
        );

        // Start cooldown
        _dashOnCooldown = true;
        _dashCooldownTimer = staggerData.dashCooldown;

        onDashEnded.Invoke();
    }
    
    // OverlapSphere around the player during dash to find opponents
    // Only triggers stagger if the opponent is currently holding the disc
    private void CheckForOpponentCollision()
    {
        if (_hasHitOpponentThisDash) return;

        Vector3 checkOrigin = dashOrigin ? dashOrigin.position : transform.position;

        int hitCount = Physics.OverlapSphereNonAlloc(checkOrigin, staggerData.dashHitRadius, _hitCollidersBuffer,
            staggerData.opponentLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hitCollidersBuffer[i];

            // Don't hit yourself
            if (hitCollider.gameObject == gameObject) continue;

            // Check if opponent has a disc handler and is holding the disc
            PlayerDiscHandler opponentDiscHandler = hitCollider.GetComponent<PlayerDiscHandler>();
            bool opponentHasDisc = opponentDiscHandler && opponentDiscHandler.IsHoldingDisc();

            if (!opponentHasDisc) continue;

            // Check if opponent is staggerable
            IStaggerable staggerable = hitCollider.GetComponent<IStaggerable>();
            if (staggerable == null) continue;

            // Already staggered — don't double-stagger
            if (staggerable.IsStaggered()) continue;

            // Execute stagger
            ApplyStaggerToOpponent(staggerable, opponentDiscHandler, hitCollider.transform);

            // Only hit one opponent per dash
            _hasHitOpponentThisDash = true;

            // End dash immediately on successful hit — feels more impactful
            EndDash();
            break;
        }
    }
    
    // Applies stagger to the opponent and knocks the disc loose.
    private void ApplyStaggerToOpponent(IStaggerable staggerable, PlayerDiscHandler opponentDiscHandler, Transform opponentTransform)
    {
        // Direction from opponent outward (away from the dasher)
        Vector3 knockBackDirection = (opponentTransform.position - transform.position).normalized;

        // Notify the handler the player lost the disc
        opponentDiscHandler.OnDiscLost();
        
        // Force disc to release with knockaway velocity in one clean call
        if (discController)
        {
            Vector3 discKnockAway = (knockBackDirection + Vector3.up * staggerData.discKnockBackUpward).normalized;
            discController.DiscForceRelease(discKnockAway * staggerData.discKnockBackForce);
        }
        
        // Apply stagger to opponent's movement
        staggerable.ApplyStagger(knockBackDirection, staggerData.discKnockBackForce);

        // Force opponent to lose the disc first
        opponentDiscHandler.OnDiscLost();

        onOpponentStaggered.Invoke(opponentTransform);

        Debug.Log($"PlayerCombat: {gameObject.name} staggered {opponentTransform.name} and knocked the disc loose!");
    }
    
    private void TickDashCooldown()
    {
        if (!_dashOnCooldown) return;

        _dashCooldownTimer -= Time.deltaTime;

        if (_dashCooldownTimer <= 0f)
        {
            _dashOnCooldown = false;
            _dashCooldownTimer = 0f;
            Debug.Log($"PlayerCombat: {gameObject.name} dash ready");
        }
    }
    
    // Returns the stagger duration from the data file & Called by PlayerController.TickStagger() to know when to recover
    public float GetStaggerDuration()
    {
        if (!staggerData) return 1.5f;
        return staggerData.staggerDuration;
    }

    public bool IsDashing()
    {
        return _isDashing;
    }

    public bool IsDashOnCooldown()
    {
        return _dashOnCooldown;
    }
    
    // Returns 0-1 normalized cooldown progress for UI display
    // 0 = just used dash, 1 = fully recharged
    public float GetDashCooldownProgress()
    {
        if (!_dashOnCooldown) return 1f;
        if (!staggerData) return 0f;

        float elapsed = staggerData.dashCooldown - _dashCooldownTimer;
        return elapsed / staggerData.dashCooldown;
    }
    
    private Vector3 GetFlatForward()
    {
        Transform camTransform = cameraTransform ? cameraTransform : transform;

        Vector3 flat = camTransform.forward;
        flat.y = 0f;

        if (flat.sqrMagnitude < 0.001f)
        {
            flat = transform.forward;
        }

        return flat.normalized;
    }
    
    // Call this when the player gets staggered by an opponent
    public void TriggerSelfStaggeredEvent()
    {
        onSelfStaggered.Invoke();
    }
}
