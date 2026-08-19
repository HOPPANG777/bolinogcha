using UnityEngine;

public class ProjectileKillCamController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera _camera;
    [SerializeField] Behaviour _defaultCameraController;

    [Header("Follow")]
    [SerializeField] float _followBackDistance = 5f;
    [SerializeField] float _followHeight = 2f;
    [SerializeField] float _followPositionSpeed = 12f;
    [SerializeField] float _followLookSpeed = 14f;

    [Header("Kill Cam Lock")]
    [SerializeField] float _lockGroundDistance = 2.2f;
    [SerializeField] float _lockBackDistance = 4f;
    [SerializeField] float _lockHeight = 1.6f;
    [SerializeField] float _impactHoldTime = 1.8f;

    BallisticProjectile _projectile;
    LayerMask _groundLayer;
    Vector3 _lockedPosition;
    Vector3 _lastLookPoint;
    float _impactTime;
    bool _isActive;
    bool _isLocked;
    bool _hasImpact;

    void Awake()
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
        }

        if (_defaultCameraController == null)
        {
            _defaultCameraController = GetComponent<QuarterViewCameraController>();
        }
    }

    void LateUpdate()
    {
        if (_isActive == false)
        {
            return;
        }

        if (_hasImpact && Time.time - _impactTime >= _impactHoldTime)
        {
            EndKillCam();
            return;
        }

        Vector3 lookPoint = GetLookPoint();

        if (_projectile != null && _projectile.HasImpacted == false)
        {
            if (_isLocked == false && ShouldLockCamera())
            {
                LockCamera();
            }

            if (_isLocked == false)
            {
                FollowProjectile(lookPoint);
                return;
            }
        }

        LookAt(lookPoint, _followLookSpeed);
    }

    public void BeginKillCam(BallisticProjectile projectile, LayerMask groundLayer)
    {
        if (projectile == null)
        {
            return;
        }

        if (_projectile != null)
        {
            _projectile.Impacted -= OnProjectileImpacted;
        }

        _projectile = projectile;
        _groundLayer = groundLayer;
        _isActive = true;
        _isLocked = false;
        _hasImpact = false;
        _lastLookPoint = projectile.transform.position;

        if (_defaultCameraController != null)
        {
            _defaultCameraController.enabled = false;
        }

        _projectile.Impacted += OnProjectileImpacted;
        FollowProjectile(_lastLookPoint, true);
    }

    public void EndKillCam()
    {
        if (_projectile != null)
        {
            _projectile.Impacted -= OnProjectileImpacted;
        }

        _projectile = null;
        _isActive = false;
        _isLocked = false;
        _hasImpact = false;

        if (_defaultCameraController != null)
        {
            _defaultCameraController.enabled = true;
        }
    }

    void FollowProjectile(Vector3 lookPoint, bool snap = false)
    {
        Vector3 direction = GetProjectileDirection();
        Vector3 desiredPosition = lookPoint - direction * _followBackDistance + Vector3.up * _followHeight;

        if (snap)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                Time.deltaTime * _followPositionSpeed
            );
        }

        LookAt(lookPoint, snap ? 1000f : _followLookSpeed);
    }

    bool ShouldLockCamera()
    {
        if (_projectile == null || _projectile.Velocity.y > 0f)
        {
            return false;
        }

        Vector3 probeStart = _projectile.transform.position + Vector3.up * 0.2f;
        return Physics.Raycast(
            probeStart,
            Vector3.down,
            out RaycastHit hit,
            _lockGroundDistance + 0.2f,
            _groundLayer,
            QueryTriggerInteraction.Ignore);
    }

    void LockCamera()
    {
        if (_projectile == null)
        {
            return;
        }

        Vector3 direction = GetProjectileDirection();
        _lockedPosition = _projectile.transform.position - direction * _lockBackDistance + Vector3.up * _lockHeight;
        transform.position = _lockedPosition;
        _isLocked = true;
    }

    Vector3 GetLookPoint()
    {
        if (_projectile != null)
        {
            _lastLookPoint = _projectile.transform.position;
        }

        return _lastLookPoint;
    }

    Vector3 GetProjectileDirection()
    {
        if (_projectile != null && _projectile.Velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 direction = _projectile.Velocity;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
        }

        return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
    }

    void LookAt(Vector3 lookPoint, float speed)
    {
        Vector3 direction = lookPoint - transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * speed
        );
    }

    void OnProjectileImpacted(BallisticProjectile projectile, Vector3 impactPoint)
    {
        _lastLookPoint = impactPoint;
        _impactTime = Time.time;
        _hasImpact = true;

        if (_isLocked == false)
        {
            LockCamera();
        }
    }

    void OnDisable()
    {
        if (_projectile != null)
        {
            _projectile.Impacted -= OnProjectileImpacted;
        }
    }
}
