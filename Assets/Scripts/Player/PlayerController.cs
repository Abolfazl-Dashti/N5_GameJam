using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float jumpForce;
    [SerializeField] private Rigidbody rb;

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            Debug.Log(context.phase);
            Debug.Log("Jump");
            rb.AddForce(Vector3.up * jumpForce * Time.deltaTime, ForceMode.Impulse);
        }
    }
}
