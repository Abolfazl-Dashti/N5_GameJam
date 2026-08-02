using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DiscController : MonoBehaviour
{
    [SerializeField] private DiscData discData;

    [Header("Catch Assist")]
    [SerializeField] private float catchPullDuration = 0.08f;

    private Rigidbody _rb;
    public DiscState currentState = DiscState.Free;
    private Transform _currentHolder;
    private Vector3 _velocityBeforeCollision = Vector3.zero;
    private int _wallLayer;
    private int _floorLayer;
    private int _ceilingLayer;
    private Collider _collider;

    private Coroutine _pullRoutine;
    private bool _isBeingPulled;

    public enum DiscState
    {
        Free,
        Held,
        Passed
    }

    public UnityEvent<Transform> onDiscReleased;
    public UnityEvent<Transform> onDiscHeld;
    public UnityEvent<Transform> onDiscPassed;
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

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState == DiscState.Held) return;

        int hitLayer = collision.gameObject.layer;
        bool hitWall = hitLayer == _wallLayer;
        bool hitFloor = hitLayer == _floorLayer;
        bool hitCeiling = hitLayer == _ceilingLayer;

        if (!hitWall && !hitFloor && !hitCeiling)
        {
            IDiscInteractor interactor = collision.gameObject.GetComponent<IDiscInteractor>();
            if (interactor != null)
            {
                _rb.linearVelocity = _velocityBeforeCollision;
            }
            return;
        }

        Vector3 surfaceNormal = collision.contacts[0].normal;
        Vector3 reflectedDirection = Vector3.Reflect(_velocityBeforeCollision.normalized, surfaceNormal);

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

        float previousSpeed = _velocityBeforeCollision.magnitude;
        float boostedSpeed = Mathf.Min(previousSpeed * boostMultiplier, discData.maxSpeed);

        Vector3 newVelocity = reflectedDirection * boostedSpeed;
        _rb.linearVelocity = newVelocity;

        onDiscRebounded.Invoke(newVelocity);
    }

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

        // اصلاح ۱: فرستادن بلافاصله‌ی ایونت بدون تأخیر فریم برای هوش مصنوعی
        onDiscHeld.Invoke(holder);

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = true;
        if (_collider) _collider.isTrigger = true;

        transform.SetParent(null, true);

        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < catchPullDuration)
        {
            if (!holder) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / catchPullDuration);

            Vector3 targetPosition = holder.TransformPoint(discData.holdOffset);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, holder.rotation, t);

            yield return null;
        }

        if (holder)
        {
            transform.position = holder.TransformPoint(discData.holdOffset);
            transform.rotation = holder.rotation;
        }

        _isBeingPulled = false;
        _pullRoutine = null;
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

        Transform thrower = _currentHolder;
        Collider throwerCollider = thrower ? thrower.GetComponent<Collider>() : null;

        CancelPullRoutine();

        transform.SetParent(null, true);

        currentState = DiscState.Passed;
        _currentHolder = null;

        if (_collider) _collider.isTrigger = false;
        _rb.isKinematic = false;

        // اصلاح ۲: نادیده گرفتن موقت برخورد با پرتاب‌کننده برای جلوگیری از گیر کردن دیسک
        if (_collider && throwerCollider)
        {
            StartCoroutine(IgnoreCollisionTemporarily(_collider, throwerCollider, 0.25f));
        }

        float throwSpeed = speedOverride > 0f ? speedOverride : discData.throwSpeed;
        Vector3 throwVelocity = direction.normalized * throwSpeed;
        _rb.linearVelocity = throwVelocity;

        Vector3 spinAxis = Vector3.Cross(direction.normalized, Vector3.up);
        _rb.AddTorque(spinAxis * discData.spinTorque, ForceMode.Impulse);

        _velocityBeforeCollision = throwVelocity;
        onDiscPassed.Invoke(thrower);
    }

    private IEnumerator IgnoreCollisionTemporarily(Collider discCol, Collider throwerCol, float duration)
    {
        Physics.IgnoreCollision(discCol, throwerCol, true);
        yield return new WaitForSeconds(duration);
        if (discCol && throwerCol)
        {
            Physics.IgnoreCollision(discCol, throwerCol, false);
        }
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