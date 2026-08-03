using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerDiscHandler : MonoBehaviour, IDiscInteractor
{
    [SerializeField] private DiscHandlerData discHandlerData;

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private DiscController disc;

    [Header("If assigned, we will share PlayerController's input instance")]
    [SerializeField] private PlayerController playerController;

    [Header("Catch Assist")]
    [Tooltip("Max angle (degrees) between camera forward and disc direction " +
             "for the E-key catch to succeed when the disc is beyond catchRadius. " +
             "Higher = more forgiving.")]
    [SerializeField] private float catchAssistAngle = 85f;

    [Header("Events")]
    public UnityEvent onPlayerCaught;
    public UnityEvent<float> onPlayerThrew;
    public UnityEvent onPlayerRedirected;
    public UnityEvent onPlayerLostDisc;

    private Main_InputSystem _inputAction;
    private bool _usingSharedInput;

    private bool _isHoldingDisc;

    private bool _isChargingThrow;
    private float _currentChargeTime;

    private bool _redirectOnCooldown;
    private float _redirectCooldownTimer;

    private float _catchAttemptBuffer;
    private const float CatchBufferDuration = 0.15f;

    [Header("Redirect Extra Rules")]
    [SerializeField] private float minRedirectDiscSpeed = 2f;

    // Reusable buffer for OverlapSphere catch detection — avoids per-frame allocation
    private Collider[] _catchOverlapBuffer = new Collider[8];

    private void Awake()
    {
        SetupInputInstance();
    }

    private void OnEnable()
    {
        BindInputs();

        if (!_usingSharedInput && _inputAction != null)
        {
            _inputAction.Enable();
        }
    }

    private void OnDisable()
    {
        UnbindInputs();

        if (!_usingSharedInput && _inputAction != null)
        {
            _inputAction.Disable();
        }
    }

    private void Update()
    {
        TickCooldowns();

        if (_isChargingThrow)
        {
            TickThrowCharge();
        }

        if (!_isHoldingDisc)
        {
            AttemptProximityCatch();
        }
    }

    // Input Setup
    private void SetupInputInstance()
    {
        _usingSharedInput = false;

        if (playerController && playerController.MainInputSystem != null)
        {
            _inputAction = playerController.MainInputSystem;
            _usingSharedInput = true;
            return;
        }

        _inputAction = new Main_InputSystem();
        _usingSharedInput = false;
    }

    private void BindInputs()
    {
        if (_inputAction == null) return;

        _inputAction.Player.Throw.started += OnThrowStarted;
        _inputAction.Player.Throw.canceled += OnThrowCanceled;

        _inputAction.Player.Catch.performed += OnCatchPerformed;
        _inputAction.Player.Redirect.performed += OnRedirectPerformed;
    }

    private void UnbindInputs()
    {
        if (_inputAction == null) return;

        _inputAction.Player.Throw.started -= OnThrowStarted;
        _inputAction.Player.Throw.canceled -= OnThrowCanceled;

        _inputAction.Player.Catch.performed -= OnCatchPerformed;
        _inputAction.Player.Redirect.performed -= OnRedirectPerformed;
    }

    private void OnThrowStarted(InputAction.CallbackContext context)
    {
        if (!_isHoldingDisc) return;

        _isChargingThrow = true;
        _currentChargeTime = 0f;
    }

    private void OnThrowCanceled(InputAction.CallbackContext context)
    {
        if (!_isHoldingDisc) return;

        ExecuteThrow();
    }

    private void OnCatchPerformed(InputAction.CallbackContext context)
    {
        if (_isHoldingDisc) return;
        if (_catchAttemptBuffer > 0f) return;

        AttemptDirectedCatch();
    }

    private void OnRedirectPerformed(InputAction.CallbackContext context)
    {
        if (_isHoldingDisc) return;
        if (_redirectOnCooldown) return;

        AttemptRedirect();
    }
    
    // CATCH LOGIC
    // Passive auto-catch — triggers when standing close to a free/passed disc
    // No look-direction requirement at all
    private void AttemptProximityCatch()
    {
        if (!disc) return;
        if (_isChargingThrow) return;
        if (disc.currentState == DiscController.DiscState.Held) return;
        if (_catchAttemptBuffer > 0f) return;

        float distanceToDisc = Vector3.Distance(transform.position, disc.transform.position);
        if (distanceToDisc <= discHandlerData.catchRadius)
        {
            CatchDisc(disc);
        }
    }
    
    // 'E' key directed catch. Uses Physics.OverlapSphere for robust detection instead
    // of relying purely on a single assigned reference + raw transform distance
    // Close range = no angle requirement at all (fixes "clunky catch" feel)
    // Extended range = generous camera-orientation cone as a secondary allowance
    private void AttemptDirectedCatch()
    {
        DiscController foundDisc = FindNearestCatchableDisc(discHandlerData.catchCastRange);
        if (!foundDisc) return;

        float distanceToDisc = Vector3.Distance(transform.position, foundDisc.transform.position);

        // Close range — always catch, regardless of where the player is looking.
        if (distanceToDisc <= discHandlerData.catchRadius)
        {
            CatchDisc(foundDisc);
            return;
        }

        // Extended range — require the disc to be roughly in front of the camera.
        if (!cameraTransform) return;

        Vector3 directionToDisc = (foundDisc.transform.position - cameraTransform.position).normalized;
        float angleToDisc = Vector3.Angle(cameraTransform.forward, directionToDisc);

        if (angleToDisc <= catchAssistAngle)
        {
            CatchDisc(foundDisc);
        }
    }
    
    // Finds the nearest catchable (Free or Passed) disc within range using an
    // OverlapSphere against the dedicated disc layer mask
    private DiscController FindNearestCatchableDisc(float range)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, range, _catchOverlapBuffer, discHandlerData.discLayerMask);
        Debug.Log($"[PlayerDiscHandler] OverlapSphere found {hitCount} colliders " +
                  $"(range={range}, layerMask={discHandlerData.discLayerMask.value}).");

        DiscController nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            DiscController candidate = _catchOverlapBuffer[i].GetComponent<DiscController>();
            if (!candidate) continue;
            if (candidate.currentState == DiscController.DiscState.Held) continue;

            float dist = Vector3.Distance(transform.position, candidate.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void CatchDisc(DiscController targetDisc)
    {
        if (!targetDisc) return;

        disc = targetDisc;

        _isHoldingDisc = true;
        _isChargingThrow = false;
        _currentChargeTime = 0f;
        _catchAttemptBuffer = CatchBufferDuration;

        // Smooth magnetic-pull catch for better game feel
        disc.RequestCatch(transform);
        onPlayerCaught.Invoke();
    }
    
    // THROW LOGIC
    private void TickThrowCharge()
    {
        _currentChargeTime += Time.deltaTime;
        if (_currentChargeTime > discHandlerData.maxChargeTime)
        {
            _currentChargeTime = discHandlerData.maxChargeTime;
        }
    }

    private void ExecuteThrow()
    {
        if (!_isHoldingDisc) return;
        if (!disc) return;
        if (!cameraTransform) return;

        _isChargingThrow = false;

        float chargeRatio = 0f;
        if (discHandlerData.maxChargeTime > 0.001f)
        {
            chargeRatio = _currentChargeTime / discHandlerData.maxChargeTime;
        }
        _currentChargeTime = 0f;

        float throwSpeed = Mathf.Lerp(discHandlerData.minThrowSpeed, discHandlerData.maxThrowSpeed, chargeRatio);
        Vector3 throwDirection = GetLoftedThrowDirection();

        _isHoldingDisc = false;
        _catchAttemptBuffer = CatchBufferDuration;

        disc.SetPassed(throwDirection, throwSpeed);

        onPlayerThrew.Invoke(throwSpeed);
        onPlayerLostDisc.Invoke();
    }

    private Vector3 GetLoftedThrowDirection()
    {
        Quaternion loftRotation = Quaternion.AngleAxis(-discHandlerData.throwLoftAngle, cameraTransform.right);
        Vector3 loftedDirection = loftRotation * cameraTransform.forward;
        return loftedDirection.normalized;
    }

    // REDIRECT LOGIC
    private void AttemptRedirect()
    {
        if (!disc) return;
        if (!cameraTransform) return;

        if (disc.currentState == DiscController.DiscState.Held) return;

        float discSpeed = disc.GetComponent<Rigidbody>().linearVelocity.magnitude;
        if (discSpeed < minRedirectDiscSpeed) return;

        float distanceToDisc = Vector3.Distance(transform.position, disc.transform.position);
        if (distanceToDisc > discHandlerData.redirectRange) return;

        Vector3 redirectDirection = cameraTransform.forward;

        disc.Redirect(redirectDirection, discHandlerData.redirectSpeedRetention);

        _redirectOnCooldown = true;
        _redirectCooldownTimer = discHandlerData.redirectCooldown;

        onPlayerRedirected.Invoke();
    }

    // Interface Methods
    public Transform GetTransform()
    {
        return transform;
    }

    public void OnDiscReceived(DiscController discController)
    {
        CatchDisc(discController);
    }

    public void OnDiscLost()
    {
        if (!_isHoldingDisc) return;

        _isHoldingDisc = false;
        _isChargingThrow = false;
        _currentChargeTime = 0f;
        _catchAttemptBuffer = CatchBufferDuration;

        onPlayerLostDisc.Invoke();
    }

    public bool IsHoldingDisc()
    {
        return _isHoldingDisc;
    }

    private void TickCooldowns()
    {
        if (_catchAttemptBuffer > 0f)
        {
            _catchAttemptBuffer -= Time.deltaTime;
        }

        if (_redirectOnCooldown)
        {
            _redirectCooldownTimer -= Time.deltaTime;

            if (_redirectCooldownTimer <= 0f)
            {
                _redirectOnCooldown = false;
                _redirectCooldownTimer = 0f;
            }
        }
    }
}