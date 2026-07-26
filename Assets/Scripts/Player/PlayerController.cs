using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, IStaggerable, IResettable
{
    // Parameters
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private float groundCheckOriginOffset = 0.9f;
    
    // Look Settings
    [SerializeField] private Transform cameraTransform;
    
    // Component References
    [SerializeField] private Rigidbody rb;
    
    // Input
    private Main_InputSystem _inputAction;
    [SerializeField] private Vector2 moveInput;
    private bool _isGrounded;
    
    // Property
    public Main_InputSystem MainInputSystem => _inputAction;
    
    // Stagger Settings
    private bool _isStaggered;
    private float _staggerTimer;
    private PlayerCombat _playerCombat;
    
    // Parameter for IResettable
    private bool _isFrozen;

    private void Awake()
    {
        _inputAction = new Main_InputSystem();
        
        _inputAction.Player.Jump.started += Jump;
        
        _inputAction.Player.Movement.performed += Movement;
        _inputAction.Player.Movement.canceled += Movement;

        _playerCombat = GetComponent<PlayerCombat>();
    }

    private void OnEnable()
    {
        _inputAction.Enable();

        // Lock & hide mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        _inputAction.Disable();
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Jump(InputAction.CallbackContext context)
    {
        if (_isStaggered) return;
        if (_isFrozen) return;
        if (!_isGrounded) return;
        
        rb.AddForce(Vector3.up * jumpForce * Time.deltaTime, ForceMode.Impulse);
        _isGrounded = false;
    }

    private void Movement(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void Update()
    {
        TickStagger();
    }

    private void FixedUpdate()
    {
        // check if player can jump(Handled in 'Jump' Method)
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.blue);
        
        if (_isStaggered || _isFrozen) return;  // Don't move while stagger or freezing
        
        RotatePlayerToCameraDirection();
        MovePlayer();
    }

    private void MovePlayer()
    {
        // convert vector2 into vector3 for moving character
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);

        if (moveDirection.magnitude > 1) moveDirection.Normalize();
        
        // Transforms direction from local space to world space
        moveDirection = transform.TransformDirection(moveDirection);
        Vector3 targetVelocity = moveDirection * moveSpeed;

        // We keep the current Y velocity so gravity and jumping aren't interrupted
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }
    
    private void RotatePlayerToCameraDirection()
    {
        if (!cameraTransform) return;

        // بدنه پلیر هم‌جهت با زاویه افقی (Y) دوربین می‌چرخد
        Vector3 targetForward = cameraTransform.forward;
        targetForward.y = 0;

        if (targetForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetForward);
            rb.MoveRotation(targetRotation);
        }
    }

    private void TickStagger()
    {
        if (!_isStaggered) return;

        _staggerTimer += Time.deltaTime;
        
        float staggerDuration = 1.5f;
        if (_playerCombat) staggerDuration = _playerCombat.GetStaggerDuration();

        if (_staggerTimer >= staggerDuration)
        {
            _isStaggered = false;
            _staggerTimer = 0f;
            Debug.Log($"PlayerController: {gameObject.name} recovered from stagger");
        }
    }
    
    public void ApplyStagger(Vector3 knockBackDirection, float knockBackForce)
    {
        _isStaggered = true;
        _staggerTimer = 0f;

        // Kill current velocity so the stagger feels impactful
        rb.linearVelocity = Vector3.zero;

        // Small physical knockback on the staggered player
        rb.AddForce(knockBackDirection * (knockBackForce * 0.5f), ForceMode.Impulse);

        // Force disc drop — get PlayerDiscHandler on this object
        PlayerDiscHandler discHandler = GetComponent<PlayerDiscHandler>();
        if (discHandler && discHandler.IsHoldingDisc())
        {
            discHandler.OnDiscLost();
        }
        
        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat) combat.TriggerSelfStaggeredEvent();

        Debug.Log($"PlayerController: {gameObject.name} is staggered");
    }

    public bool IsStaggered()
    {
        return _isStaggered;
    }

    #region IResettable
    public void ResetToSpawn(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Teleport
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        // Clear any stagger state on respawn
        _isStaggered = false;
        _staggerTimer = 0f;

        // Clear move input so player doesn't drift after teleport
        moveInput = Vector2.zero;

        Debug.Log($"[PlayerController] {gameObject.name} reset to spawn at {spawnPosition}.");
    }

    public void FreezePlayer()
    {
        _isFrozen = true;
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
    }

    public void UnfreezePlayer()
    {
        _isFrozen = false;
    }
    #endregion
}
