using System;
using System.Collections.Generic;
using UnityEngine;

public class BallisticProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] bool _rotateToVelocity = true;
    [SerializeField] Vector3 _modelForwardAxis = Vector3.forward;
    [SerializeField] float _hitRadius = 0.15f;
    [SerializeField] float _groundProbeDistance = 0.35f;
    [SerializeField] float _maxLifeTime = 15f;

    [Header("VFX")]
    [SerializeField] ParticleSystem _exhaust;
    [SerializeField] float _lifeAfterImpact = 2f;

    Vector3 _startPosition;
    Vector3 _horizontalDirection;
    Vector3 _lastPosition;
    Vector3 _velocity;
    LayerMask _groundLayer;
    float _horizontalSpeed;
    float _verticalSpeed;
    float _gravity;
    float _elapsed;
    bool _isLaunched;
    bool _hasImpacted;

    GameObject _explosionPrefab;
    Transform _owner;
    LayerMask _damageMask;
    float _damage;
    float _splashRadius;
    float _explosionLifetime = 6f;
    bool _hasForcedImpactTarget;
    Vector3 _forcedImpactTarget;
    float _forcedImpactRadius = 0.08f;

    public event Action<BallisticProjectile, Vector3> Impacted;

    public Vector3 Velocity => _velocity;
    public bool IsLaunched => _isLaunched;
    public bool HasImpacted => _hasImpacted;

    public void SetHitRadius(float hitRadius)
    {
        _hitRadius = Mathf.Max(0.01f, hitRadius);
    }

    public void ForceImpactAtTarget(Vector3 target, float radius)
    {
        _forcedImpactTarget = target;
        _forcedImpactRadius = Mathf.Max(0.01f, radius);
        _hasForcedImpactTarget = true;
    }

    void Awake()
    {
        if (_exhaust == null)
        {
            _exhaust = GetComponentInChildren<ParticleSystem>();
        }
    }

    public void ConfigureImpact(
        GameObject explosionPrefab,
        float explosionLifetime,
        float damage,
        float splashRadius,
        LayerMask damageMask,
        Transform owner)
    {
        _explosionPrefab = explosionPrefab;
        _explosionLifetime = explosionLifetime;
        _damage = damage;
        _splashRadius = splashRadius;
        _damageMask = damageMask;
        _owner = owner;
    }

    public void Launch(
        Vector3 origin,
        Vector3 horizontalDirection,
        float range,
        float launchAngle,
        float gravity,
        LayerMask groundLayer)
    {
        _startPosition = origin;
        _horizontalDirection = horizontalDirection;
        _horizontalDirection.y = 0f;

        if (_horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            _horizontalDirection = transform.forward;
            _horizontalDirection.y = 0f;
        }

        _horizontalDirection.Normalize();

        _gravity = Mathf.Max(0.01f, gravity);
        _groundLayer = groundLayer;

        float angle = Mathf.Clamp(launchAngle, 5f, 85f) * Mathf.Deg2Rad;
        float sinDoubleAngle = Mathf.Max(0.01f, Mathf.Sin(2f * angle));
        float launchSpeed = Mathf.Sqrt(Mathf.Max(0.1f, range) * _gravity / sinDoubleAngle);

        _horizontalSpeed = Mathf.Cos(angle) * launchSpeed;
        _verticalSpeed = Mathf.Sin(angle) * launchSpeed;
        _elapsed = 0f;
        _isLaunched = true;
        _hasImpacted = false;
        _hasForcedImpactTarget = false;
        _lastPosition = origin;
        _velocity = _horizontalDirection * _horizontalSpeed + Vector3.up * _verticalSpeed;

        transform.position = origin;
        RotateTowardVelocity();

        if (_exhaust != null)
        {
            _exhaust.Play(true);
        }
    }

    public void LaunchAtTarget(
        Vector3 origin,
        Vector3 target,
        float launchAngle,
        float gravity,
        LayerMask groundLayer)
    {
        Vector3 horizontalOffset = target - origin;
        horizontalOffset.y = 0f;

        if (horizontalOffset.sqrMagnitude <= 0.0001f)
        {
            horizontalOffset = transform.forward;
            horizontalOffset.y = 0f;
        }

        float range = Mathf.Max(0.1f, horizontalOffset.magnitude);
        Vector3 horizontalDirection = horizontalOffset.normalized;
        float resolvedGravity = Mathf.Max(0.01f, gravity);
        float angle = Mathf.Clamp(launchAngle, 5f, 85f) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float verticalDelta = target.y - origin.y;
        float denominator = 2f * cos * cos * (range * Mathf.Tan(angle) - verticalDelta);
        float launchSpeed;

        if (denominator > 0.01f)
        {
            launchSpeed = Mathf.Sqrt((resolvedGravity * range * range) / denominator);
        }
        else
        {
            float sinDoubleAngle = Mathf.Max(0.01f, Mathf.Sin(2f * angle));
            launchSpeed = Mathf.Sqrt(range * resolvedGravity / sinDoubleAngle);
        }

        _startPosition = origin;
        _horizontalDirection = horizontalDirection;
        _gravity = resolvedGravity;
        _groundLayer = groundLayer;
        _horizontalSpeed = cos * launchSpeed;
        _verticalSpeed = Mathf.Sin(angle) * launchSpeed;
        _elapsed = 0f;
        _isLaunched = true;
        _hasImpacted = false;
        _hasForcedImpactTarget = false;
        _lastPosition = origin;
        _velocity = _horizontalDirection * _horizontalSpeed + Vector3.up * _verticalSpeed;

        transform.position = origin;
        RotateTowardVelocity();

        if (_exhaust != null)
        {
            _exhaust.Play(true);
        }
    }

    void Update()
    {
        if (_isLaunched == false || _hasImpacted)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        Vector3 nextPosition =
            _startPosition +
            _horizontalDirection * (_horizontalSpeed * _elapsed) +
            Vector3.up * (_verticalSpeed * _elapsed - 0.5f * _gravity * _elapsed * _elapsed);

        _velocity = _horizontalDirection * _horizontalSpeed + Vector3.up * (_verticalSpeed - _gravity * _elapsed);

        if (_hasForcedImpactTarget && HasReachedForcedImpactTarget(nextPosition))
        {
            transform.position = _forcedImpactTarget;
            Impact(_forcedImpactTarget);
            return;
        }

        if (_hasForcedImpactTarget == false && TryHitObstacle(nextPosition, out Vector3 impactPoint))
        {
            transform.position = impactPoint;
            Impact(impactPoint);
            return;
        }

        transform.position = nextPosition;
        RotateTowardVelocity();
        _lastPosition = nextPosition;

        if (_elapsed >= _maxLifeTime)
        {
            Impact(transform.position);
        }
    }

    bool HasReachedForcedImpactTarget(Vector3 nextPosition)
    {
        Vector3 toTarget = _forcedImpactTarget - _startPosition;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return Vector3.Distance(nextPosition, _forcedImpactTarget) <= _forcedImpactRadius;
        }

        Vector3 progressVector = nextPosition - _startPosition;
        progressVector.y = 0f;
        float targetDistance = toTarget.magnitude;
        float progress = Vector3.Dot(progressVector, _horizontalDirection);

        if (progress >= targetDistance - _forcedImpactRadius)
        {
            return true;
        }

        return Vector3.Distance(nextPosition, _forcedImpactTarget) <= _forcedImpactRadius;
    }

    bool TryHitObstacle(Vector3 nextPosition, out Vector3 impactPoint)
    {
        impactPoint = nextPosition;

        Vector3 segment = nextPosition - _lastPosition;
        if (segment.sqrMagnitude > 0.0001f)
        {
            int combinedMask = _groundLayer.value | _damageMask.value;
            RaycastHit[] hits = Physics.SphereCastAll(
                _lastPosition,
                _hitRadius,
                segment.normalized,
                segment.magnitude,
                combinedMask,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            bool hasHit = false;

            for (int i = 0; i < hits.Length; i++)
            {
                if (ShouldIgnoreHit(hits[i].transform))
                {
                    continue;
                }

                if (hits[i].distance < closestDistance)
                {
                    closestDistance = hits[i].distance;
                    impactPoint = hits[i].point;
                    hasHit = true;
                }
            }

            if (hasHit)
            {
                return true;
            }
        }

        if (_velocity.y <= 0f)
        {
            Vector3 probeStart = nextPosition + Vector3.up * _groundProbeDistance;
            float probeDistance = _groundProbeDistance * 2f;

            if (Physics.Raycast(
                    probeStart,
                    Vector3.down,
                    out RaycastHit hit,
                    probeDistance,
                    _groundLayer,
                    QueryTriggerInteraction.Ignore))
            {
                impactPoint = hit.point;
                return true;
            }
        }

        return false;
    }

    void RotateTowardVelocity()
    {
        if (_rotateToVelocity == false || _velocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 localForward = SafeNormalized(_modelForwardAxis, Vector3.forward);
        Quaternion axisCorrection = Quaternion.FromToRotation(localForward, Vector3.forward);
        transform.rotation = Quaternion.LookRotation(_velocity.normalized, Vector3.up) * axisCorrection;
    }

    bool ShouldIgnoreHit(Transform hitTransform)
    {
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return true;
        }

        if (_owner != null && (hitTransform == _owner || hitTransform.IsChildOf(_owner)))
        {
            return true;
        }

        return false;
    }

    void Impact(Vector3 impactPoint)
    {
        if (_hasImpacted)
        {
            return;
        }

        _hasImpacted = true;
        _isLaunched = false;

        if (_exhaust != null)
        {
            _exhaust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        HideProjectileBody();
        SpawnExplosion(impactPoint);
        ApplySplashDamage(impactPoint);

        Impacted?.Invoke(this, impactPoint);

        EditorSelectionClearOnDestroy.Attach(gameObject);
        Destroy(gameObject, Mathf.Max(0f, _lifeAfterImpact));
    }

    void HideProjectileBody()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer)
            {
                continue;
            }

            renderers[i].enabled = false;
        }
    }

    void SpawnExplosion(Vector3 impactPoint)
    {
        if (_explosionPrefab == null)
        {
            return;
        }

        GameObject explosion = Instantiate(_explosionPrefab, impactPoint, Quaternion.identity);
        VfxUtility.MakeOneShot(explosion, _explosionLifetime);
    }

    void ApplySplashDamage(Vector3 impactPoint)
    {
        if (_damage <= 0f || _splashRadius <= 0f)
        {
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(
            impactPoint,
            _splashRadius,
            _damageMask,
            QueryTriggerInteraction.Ignore);

        HashSet<TankHealth> damagedTargets = new HashSet<TankHealth>();

        for (int i = 0; i < colliders.Length; i++)
        {
            TankHealth health = colliders[i].GetComponentInParent<TankHealth>();
            if (health == null || ShouldIgnoreHit(health.transform))
            {
                continue;
            }

            if (damagedTargets.Add(health))
            {
                health.TakeDamage(_damage, impactPoint);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isLaunched == false || _hasImpacted)
        {
            return;
        }

        if (ShouldIgnoreHit(collision.transform))
        {
            return;
        }

        Vector3 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        Impact(impactPoint);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isLaunched == false || _hasImpacted)
        {
            return;
        }

        if (ShouldIgnoreHit(other.transform))
        {
            return;
        }

        Impact(transform.position);
    }

    Vector3 SafeNormalized(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude <= 0.0001f)
        {
            return fallback.normalized;
        }

        return value.normalized;
    }
}
