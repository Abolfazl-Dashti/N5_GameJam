using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DiscController : MonoBehaviour
{
    // ScriptableObjects References
    [SerializeField] private DiscData discData;

    [Header("Catch Assist")]
    [Tooltip("Duration of the smooth 'magnetic pull' animation when a character catches the disc.")]
    [SerializeField] private float catchPullDuration = 0.08f;

    // Parameters
    private Rigidbody _rb;
    public DiscState currentState = DiscState.Free;
    private Transform _currentHolder;
    private Vector3 _velocityBeforeCollision = Vector3.zero;
    private int _wallLayer;
    private int _floorLayer;
    private int _ceilingLayer;
    private Collider _collider;

    // Catch pull state
    private Coroutine _pullRoutine;
    private bool _isBeingPulled;

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
        _collider = GetComponent<Collider>();
        ValidateRigidbodySettings();
        CacheLayerIntegers();
    }

    private void FixedUpdate()
    {
        // Track velocity every physics tick so OnCollisionEnter
        // has access to the pre-collision velocity accurately
        if (currentState != DiscState.Held)
        {
            _velocityBeforeCollision = _rb.linearVelocity;
        }

        ApplyCustomGravity();
        EnforceSpeedLimits();
    }

    private void LateUpdate()
    {
        if (currentState != DiscState.Held || !_currentHolder) return;

        // Let the catch-pull coroutine control position/rotation during the
        // brief catch animation window — don't fight it here.
        if (_isBeingPulled) return;

        transform.position = _currentHolder.TransformPoint(discData.holdOffset);
        transform.rotation = _currentHolder.rotation;
    }

    private void ApplyCustomGravity()
    {
        if (currentState == DiscState.Held) return;
        Vector3 customGravity = Physics.gravity * discData.gravityScale;
        _rb.AddForce(customGravity, ForceMode.Acceleration);
    }

    // Collision & Rebound Logic
    private void OnCollisionEnter(Collision collision)
    {
        // Only process rebounds when disc is Free or Passed
        if (currentState == DiscState.Held) return;

        int hitLayer = collision.gameObject.layer;
        bool hitWall = hitLayer == _wallLayer;
        bool hitFloor = hitLayer == _floorLayer;
        bool hitCeiling = hitLayer == _ceilingLayer;

        if (!hitWall && !hitFloor && !hitCeiling)
        {
            // SAFETY NET: the disc should never physically collide with a character.
            // The Layer Collision Matrix (Disc <-> Character unchecked) should prevent
            // this entirely, but if it still happens due to misconfiguration, cancel
            // whatever bounce impulse PhysX already applied so the disc keeps flying
            // on its original trajectory instead of feeling like a solid wall.
            IDiscInteractor interactor = collision.gameObject.GetComponent<IDiscInteractor>();
            if (interactor != null)
            {
                _rb.linearVelocity = _velocityBeforeCollision;
            }
            return;
        }

        // Grab the first contact normal — most reliable single-contact surface read
        Vector3 surfaceNormal = collision.contacts[0].normal;
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
        float boostedSpeed = Mathf.Min(previousSpeed * boostMultiplier, discData.maxSpeed);

        // Apply the new clean velocity — overrides whatever the physics solver did
        Vector3 newVelocity = reflectedDirection * boostedSpeed;
        _rb.linearVelocity = newVelocity;

        // Broadcast rebound event so VFX, SFX, and AI can react
        onDiscRebounded.Invoke(newVelocity);
    }

    /// <summary>
    /// Smooth "magnetic pull" catch — animates the disc from its current position
    /// into the holder's hand over a short duration for better game feel.
    /// currentState becomes Held IMMEDIATELY (preventing double-catch races) even
    /// though the visual position finishes animating a few frames later.
    /// </summary>
    public void RequestCatch(Transform holder)
    {
        if (!holder) return;
        if (currentState == DiscState.Held) return;

        CancelPullRoutine();
        _pullRoutine = StartCoroutine(PullAndHoldRoutine(holder));
    }

    private IEnumerator PullAndHoldRoutine(Transform holder)
    {
        _isBeingPulled = true;

        currentState = DiscState.Held;
        _currentHolder = holder;

        // Zero out velocity WHILE STILL DYNAMIC — must happen before going kinematic,
        // otherwise Unity warns ("Setting velocity of a kinematic body is not supported").
        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // NOW switch to kinematic so the hand-follow position isn't fought by physics.
        _rb.isKinematic = true;

        if (_collider) _collider.isTrigger = true;

        transform.SetParent(null, true);

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < catchPullDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / catchPullDuration);

            Vector3 targetPosition = holder.TransformPoint(discData.holdOffset);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, holder.rotation, t);

            yield return null;
        }

        transform.position = holder.TransformPoint(discData.holdOffset);
        transform.rotation = holder.rotation;

        _isBeingPulled = false;
        _pullRoutine = null;

        onDiscHeld.Invoke(holder);
    }

    private void CancelPullRoutine()
    {
        if (_pullRoutine != null)
        {
            StopCoroutine(_pullRoutine);
            _pullRoutine = null;
        }
        _isBeingPulled = false;
    }

    public void SetPassed(Vector3 direction, float speedOverride = -1f)
    {
        if (currentState != DiscState.Held)
        {
            Debug.LogWarning("[DiscController] SetPassed called but disc is not currently Held.");
            return;
        }

        CancelPullRoutine();

        Transform thrower = _currentHolder;

        transform.SetParent(null, true);

        currentState = DiscState.Passed;
        _currentHolder = null;

        if (_collider) _collider.isTrigger = false;
        _rb.isKinematic = false;

        float throwSpeed = speedOverride > 0f ? speedOverride : discData.throwSpeed;
        Vector3 throwVelocity = direction.normalized * throwSpeed;
        _rb.linearVelocity = throwVelocity;

        Vector3 spinAxis = Vector3.Cross(direction.normalized, Vector3.up);
        _rb.AddTorque(spinAxis * discData.spinTorque, ForceMode.Impulse);

        _velocityBeforeCollision = throwVelocity;
        onDiscPassed.Invoke(thrower);
    }

    public void SetFree(Vector3 releaseVelocity = default)
    {
        CancelPullRoutine();

        Transform lastHolder = _currentHolder;

        transform.SetParent(null, true);

        currentState = DiscState.Free;
        _currentHolder = null;

        if (_collider) _collider.isTrigger = false;
        _rb.isKinematic = false;
        _rb.linearVelocity = releaseVelocity;

        _velocityBeforeCollision = releaseVelocity;
        onDiscReleased.Invoke(lastHolder);
    }

    public void Redirect(Vector3 redirectDirection, float speedRetention = 0.9f)
    {
        if (currentState == DiscState.Held)
        {
            Debug.LogWarning("[DiscController] Cannot Redirect a held disc — use SetPassed instead.");
            return;
        }

        if (_collider) _collider.isTrigger = false;

        float currentSpeed = _rb.linearVelocity.magnitude;
        float redirectSpeed = Mathf.Min(currentSpeed * speedRetention, discData.maxSpeed);
        Vector3 newVelocity = redirectDirection.normalized * redirectSpeed;

        _rb.linearVelocity = newVelocity;
        _velocityBeforeCollision = newVelocity;

        currentState = DiscState.Free;
        _currentHolder = null;

        onDiscReleased.Invoke(null);
    }

    private void EnforceSpeedLimits()
    {
        if (currentState == DiscState.Held) return;

        float speed = _rb.linearVelocity.magnitude;

        if (speed > discData.maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * discData.maxSpeed;
        }
        else if (currentState == DiscState.Free && speed < discData.minFreeSpeed && speed > 0.01f)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * discData.minFreeSpeed;
        }
    }

    private void CacheLayerIntegers()
    {
        _wallLayer = LayerMask.NameToLayer(discData.wallLayerName);
        _floorLayer = LayerMask.NameToLayer(discData.floorLayerName);
        _ceilingLayer = LayerMask.NameToLayer(discData.ceilingLayerName);
    }

    private void ValidateRigidbodySettings()
    {
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.useGravity = false;
        _rb.linearDamping = discData.freeDrag;
        _rb.angularDamping = 0.1f;
    }

    public void DiscForceRelease(Vector3 releaseVelocity)
    {
        if (currentState != DiscState.Held)
        {
            Debug.LogWarning("[DiscController] ForceRelease called but disc is not Held.");
            return;
        }

        CancelPullRoutine();

        transform.SetParent(null, true);
        currentState = DiscState.Free;
        _currentHolder = null;

        if (_collider) _collider.isTrigger = false;
        _rb.isKinematic = false;
        _rb.linearVelocity = releaseVelocity;
        _velocityBeforeCollision = releaseVelocity;

        onDiscReleased.Invoke(null);
    }
}