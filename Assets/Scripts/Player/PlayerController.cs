using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // Parameters
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance;
    
    // Component References
    [SerializeField] private Rigidbody rb;
    
    // Input
    private Main_InputSystem _inputAction;
    [SerializeField] private Vector2 moveInput;
    private bool _isGrounded;

    private void Awake()
    {
        _inputAction = new Main_InputSystem();
        
        _inputAction.Player.Jump.started += Jump;
        
        _inputAction.Player.Movement.performed += Movement;
        _inputAction.Player.Movement.canceled += Movement;
    }

    private void OnEnable()
    {
        _inputAction.Enable();
    }

    private void OnDisable()
    {
        _inputAction.Disable();
    }

    private void Jump(InputAction.CallbackContext context)
    {
        if (!_isGrounded) return;
        rb.AddForce(Vector3.up * jumpForce * Time.deltaTime, ForceMode.Impulse);
        _isGrounded = false;
    }

    private void Movement(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // check if player can jump(Handled in 'Jump' Method)
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.blue);
        
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
}
