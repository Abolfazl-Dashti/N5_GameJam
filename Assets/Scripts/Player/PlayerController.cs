using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, IStaggerable, IResettable
{
    // Parameters
    [SerializeField] private float moveSpeed = 25f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance;
    
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
    
    // Stagger & Combat Settings
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
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (_inputAction != null) _inputAction.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (_inputAction != null) _inputAction.Disable();
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Jump(InputAction.CallbackContext context)
    {
        if (_isStaggered || _isFrozen || !_isGrounded) return;
        if (_playerCombat && _playerCombat.IsDashing()) return;
        
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.blue);
        
        if (_isStaggered || _isFrozen) return;  
        if (_playerCombat && _playerCombat.IsDashing()) return; 

        RotatePlayerToCameraDirection();
        MovePlayer();
    }

    private void MovePlayer()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        if (moveDirection.magnitude > 1) moveDirection.Normalize();
        
        moveDirection = transform.TransformDirection(moveDirection);
        Vector3 targetVelocity = moveDirection * moveSpeed;

        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }
    
    private void RotatePlayerToCameraDirection()
    {
        if (!cameraTransform) return;

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
            Debug.Log($"[PlayerController] {gameObject.name} recovered from stagger");
        }
    }
    
    public void ApplyStagger(Vector3 knockBackDirection, float knockBackForce)
    {
        _isStaggered = true;
        _staggerTimer = 0f;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(knockBackDirection * (knockBackForce * 0.5f), ForceMode.Impulse);

        PlayerDiscHandler discHandler = GetComponent<PlayerDiscHandler>();
        if (discHandler && discHandler.IsHoldingDisc())
        {
            discHandler.OnDiscLost();
        }
        
        if (_playerCombat) _playerCombat.TriggerSelfStaggeredEvent();

        Debug.Log($"[PlayerController] {gameObject.name} IS STAGGERED!");
    }

    public bool IsStaggered() => _isStaggered;

    #region IResettable
    public void ResetToSpawn(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        _isStaggered = false;
        _staggerTimer = 0f;
        moveInput = Vector2.zero;
    }

    public void FreezePlayer()
    {
        _isFrozen = true;
        rb.linearVelocity = Vector3.zero;
        moveInput = Vector2.zero;
    }

    public void UnfreezePlayer() => _isFrozen = false;
    #endregion
}