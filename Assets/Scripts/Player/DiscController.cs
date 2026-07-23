using UnityEngine;
using UnityEngine.Events;

public class DiscController : MonoBehaviour
{
    // ScriptableObjects References
    [SerializeField] private DiscData discData;
    
    // Parameters
    private Rigidbody _rb;
    private DiscState _currentState = DiscState.Free;
    private Transform _currentHolder;
    private Vector3 _velocityBeforeCollision = Vector3.zero;
    private int _wallLayer;
    private int _floorLayer;
    private int _ceilingLayer;

    public enum DiscState
    {
        Free,  // no owner
        Held,  // owned by a player
        Passed  // when thrown and flying
    }

    [Tooltip("Fires when disc becomes Free (no owner). Passes last owner Transform (can be null).")]
    public UnityEvent<Transform> onDiscReleased;
    
    [Tooltip("Fires when disc is picked up / held. Passes the holder's Transform.")]
    public UnityEvent<Transform> onDiscHeld;
    
    [Tooltip("Fires when disc is thrown/passed. Passes the thrower's Transform.")]
    public UnityEvent<Transform> onDiscPassed;
    
    [Tooltip("Fires on any arena surface rebound. Passes the new velocity after boost.")]
    public UnityEvent<Vector3> onDiscRebounded;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        ValidateRigidbodySettings();
        CacheLayerIntegers();
    }

    private void FixedUpdate()
    {
        // Track velocity every physics tick so OnCollisionEnter
        // has access to the pre-collision velocity accurately
        if (_currentState != DiscState.Held)
        {
            _velocityBeforeCollision = _rb.linearVelocity;
        }

        ApplyCustomGravity();
        EnforceSpeedLimits();
        FollowHolderIfHeld();
    }
    
    private void ApplyCustomGravity()
    {
        if (_currentState == DiscState.Held)
        {
            return;
        }

        // Physics.gravity is (0, -9.81, 0) by default
        // Multiplying by gravityScale from the SO gives us a weaker pull
        Vector3 customGravity = Physics.gravity * discData.gravityScale;
        _rb.AddForce(customGravity, ForceMode.Acceleration);
    }
    
    // Collision & Rebound Logic
    private void OnCollisionEnter(Collision collision)
    {
        // Only process rebounds when disc is Free or Passed
        if (_currentState == DiscState.Held) return;

        int hitLayer = collision.gameObject.layer;
        bool hitWall = hitLayer == _wallLayer;
        bool hitFloor = hitLayer == _floorLayer;
        bool hitCeiling = hitLayer == _ceilingLayer;

        // Only apply our custom rebound to tagged arena surfaces
        if (!hitWall && !hitFloor && !hitCeiling) return;
        
        // Grab the first contact normal — most reliable single-contact surface read
        Vector3 surfaceNormal = collision.contacts[0].normal;

        // Reflect our PRE-collision velocity off the surface normal
        // This gives us a clean, predictable trajectory regardless of
        // how Unity's physics solver nudged the velocity during contact
        Vector3 reflectedDirection = Vector3.Reflect(_velocityBeforeCollision.normalized, surfaceNormal);

        // Choose the correct boost multiplier based on which surface was hit
        float boostMultiplier = 1f;

        if (hitWall)
        {
            boostMultiplier = discData.wallReboundBoostMultiplier;
        }
        else if (hitFloor)
        {
            boostMultiplier = discData.floorReboundBoostMultiplier;
        }
        else if (hitCeiling)
        {
            boostMultiplier = discData.ceilingReboundBoostMultiplier;
        }

        // New speed = previous speed * boost, clamped to maxSpeed
        float previousSpeed = _velocityBeforeCollision.magnitude;
        float boostedSpeed   = Mathf.Min(previousSpeed * boostMultiplier, discData.maxSpeed);

        // Apply the new clean velocity — overrides whatever the physics solver did
        Vector3 newVelocity = reflectedDirection * boostedSpeed;
        _rb.linearVelocity = newVelocity;

        // Broadcast rebound event so VFX, SFX, and AI can react
        onDiscRebounded.Invoke(newVelocity);
    }
    
    public void SetHeld(Transform holder)
    {
        if (!holder)
        {
            Debug.LogWarning("[DiscController] SetHeld called with null holder.");
            return;
        }

        _currentState  = DiscState.Held;
        _currentHolder = holder;

        // Make kinematic so physics doesn't fight the hand position
        _rb.isKinematic = true;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Parent to the holder so it moves with them automatically
        transform.SetParent(_currentHolder);
        transform.localPosition = discData.holdOffset;
        transform.localRotation = Quaternion.identity;

        onDiscHeld.Invoke(holder);
    }
    
    public void SetPassed(Vector3 direction, float speedOverride = -1f)
    {
        if (_currentState != DiscState.Held)
        {
            Debug.LogWarning("[DiscController] SetPassed called but disc is not currently Held.");
            return;
        }

        Transform thrower = _currentHolder;

        // Detach from holder before re-enabling physics
        transform.SetParent(null);

        _currentState  = DiscState.Passed;
        _currentHolder = null;

        // Re-enable full physics
        _rb.isKinematic = false;

        float throwSpeed  = (speedOverride > 0f) ? speedOverride : discData.throwSpeed;
        Vector3 throwVelocity = direction.normalized * throwSpeed;

        _rb.linearVelocity = throwVelocity;

        // Add spin torque for visual feedback — disc should rotate on its axis
        Vector3 spinAxis = Vector3.Cross(direction.normalized, Vector3.up);
        _rb.AddTorque(spinAxis * discData.spinTorque, ForceMode.Impulse);

        // Seed velocity tracker immediately so first collision has accurate data
        _velocityBeforeCollision = throwVelocity;

        onDiscPassed.Invoke(thrower);
    }
    
    public void SetFree(Vector3 releaseVelocity = default)
    {
        Transform lastHolder = _currentHolder;

        transform.SetParent(null);

        _currentState  = DiscState.Free;
        _currentHolder = null;

        _rb.isKinematic    = false;
        _rb.linearVelocity = releaseVelocity;

        // Seed tracker
        _velocityBeforeCollision = releaseVelocity;

        onDiscReleased.Invoke(lastHolder);
    }
    
    public void Redirect(Vector3 redirectDirection, float speedRetention = 0.9f)
    {
        if (_currentState == DiscState.Held)
        {
            Debug.LogWarning("[DiscController] Cannot Redirect a held disc — use SetPassed instead.");
            return;
        }

        float currentSpeed    = _rb.linearVelocity.magnitude;
        float redirectSpeed   = Mathf.Min(currentSpeed * speedRetention, discData.maxSpeed);
        Vector3 newVelocity   = redirectDirection.normalized * redirectSpeed;

        _rb.linearVelocity       = newVelocity;
        _velocityBeforeCollision = newVelocity;

        // State becomes Free momentarily — possession system decides ownership next
        _currentState  = DiscState.Free;
        _currentHolder = null;

        onDiscReleased.Invoke(null);
    }
    
    private void EnforceSpeedLimits()
    {
        if (_currentState == DiscState.Held)
        {
            return;
        }

        float speed = _rb.linearVelocity.magnitude;

        if (speed > discData.maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * discData.maxSpeed;
        }
        else if (_currentState == DiscState.Free && speed < discData.minFreeSpeed && speed > 0.01f)
        {
            // Nudge back up to minimum so disc never awkwardly crawls to a stop
            _rb.linearVelocity = _rb.linearVelocity.normalized * discData.minFreeSpeed;
        }
    }
    
    private void FollowHolderIfHeld()
    {
        if (_currentState != DiscState.Held || !_currentHolder)
        {
            return;
        }

        // Confirm local position hasn't drifted
        transform.localPosition = discData.holdOffset;
    }
    
    private void CacheLayerIntegers()
    {
        _wallLayer = LayerMask.NameToLayer(discData.wallLayerName);
        _floorLayer = LayerMask.NameToLayer(discData.floorLayerName);
        _ceilingLayer = LayerMask.NameToLayer(discData.ceilingLayerName);

        if (_wallLayer == -1)
            Debug.LogError($"[DiscController] Layer '{discData.wallLayerName}' not found! Check DiscDataSO.");
        if (_floorLayer == -1)
            Debug.LogError($"[DiscController] Layer '{discData.floorLayerName}' not found! Check DiscDataSO.");
        if (_ceilingLayer == -1)
            Debug.LogError($"[DiscController] Layer '{discData.ceilingLayerName}' not found! Check DiscDataSO.");
    }
    
    private void ValidateRigidbodySettings()
    {
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.useGravity = false;
        _rb.linearDamping = discData.freeDrag;
        _rb.angularDamping = 0.1f;
    }
}
