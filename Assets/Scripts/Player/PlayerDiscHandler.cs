using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class PlayerDiscHandler : MonoBehaviour, IDiscInteractor
{
    // ScriptableObject References
    [SerializeField] private DiscHandlerData discHandlerData;
    
    // Component References
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private DiscController disc;

    [Header("If assigned, we will share PlayerController's input instance")]
    [SerializeField] private PlayerController playerController;

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

        // Fallback: create our own input instance
        _inputAction = new Main_InputSystem();
        _usingSharedInput = false;
    }

    private void BindInputs()
    {
        if (_inputAction == null) return;

        // These action names must exist in your Main_InputSystem input actions:
        // Player.Throw, Player.Catch, Player.Redirect
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

    // -------------------------------------------------------------------------
    // INPUT CALLBACKS (NO PlayerInput component needed)
    // -------------------------------------------------------------------------
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
    
    // Catch Logic
    private void AttemptProximityCatch()
    {
        if (!disc) return;
        if (_isChargingThrow) return;
        if (disc.currentState == DiscController.DiscState.Held) return;
        if (_catchAttemptBuffer > 0f) return;

        float distanceToDisc = Vector3.Distance(transform.position, disc.transform.position);
        if (distanceToDisc <= discHandlerData.catchRadius)
        {
            CatchDisc();
        }
    }

    private void AttemptDirectedCatch()
    {
        if (!disc) return;
        if (!cameraTransform) return;

        if (disc.currentState == DiscController.DiscState.Held) return;

        Ray catchRay = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit hit;

        bool discInCastPath = Physics.SphereCast(
            catchRay,
            discHandlerData.catchCastRadius,
            out hit,
            discHandlerData.catchCastRange,
            discHandlerData.discLayerMask
        );

        if (!discInCastPath) return;

        DiscController hitDisc = hit.collider.GetComponent<DiscController>();
        if (hitDisc != null && hitDisc == disc)
        {
            CatchDisc();
        }
    }

    private void CatchDisc()
    {
        if (!disc) return;

        _isHoldingDisc = true;
        _isChargingThrow = false;
        _currentChargeTime = 0f;
        _catchAttemptBuffer = CatchBufferDuration;

        disc.SetHeld(transform);
        onPlayerCaught.Invoke();
    }
    
    // Throw Logic
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

    // Redirect Logic
    private void AttemptRedirect()
    {
        if (!disc) return;
        if (!cameraTransform) return;

        if (disc.currentState == DiscController.DiscState.Held) return;

        // Require the disc to actually be moving (prevents redirecting a stopped disc)
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
        disc = discController;
        CatchDisc();
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