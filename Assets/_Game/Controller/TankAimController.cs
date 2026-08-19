using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TankAimController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera _mainCamera;
    [SerializeField] Transform _turret;
    [SerializeField] Transform _muzzle;
    [SerializeField] string _turretName = "Turret";
    [SerializeField] string _muzzleName = "Muzzle";

    [Header("Input")]
    [SerializeField] LayerMask _aimStartMask = ~0;
    [SerializeField] float _rayDistance = 1000f;
    [SerializeField] float _holdToAimDelay = 0.12f;
    [SerializeField] float _minAimDragDistance = 0.15f;
    [SerializeField] float _minFireDragDistance = 0.4f;

    [Header("Aim")]
    [SerializeField] float _rangeCoefficient = 3f;
    [SerializeField] float _maxDragDistance = 5f;

    [Header("Turret Rotation")]
    [SerializeField] float _turretRotationSpeed = 14f;
    [SerializeField] Vector3 _turretLocalYawAxis = Vector3.forward;
    [SerializeField] Vector3 _turretLocalForwardAxis = Vector3.right;
    [SerializeField] bool _flipCalibratedBarrel = true;
    [SerializeField] bool _useRuntimeTurretPivot = true;

    [Header("Preview")]
    [SerializeField] bool _showAimTrajectory = true;
    [SerializeField] int _trajectoryDotCount = 28;
    [SerializeField] float _trajectoryDotSpacing = 0.45f;
    [SerializeField] float _trajectoryLaunchAngle = 55f;
    [SerializeField] float _trajectoryGravity = 9.81f;
    [SerializeField, Range(0.1f, 1f)] float _trajectoryVisiblePortion = 0.72f;
    [SerializeField, Range(0f, 1f)] float _trajectoryFadeStart = 0.45f;
    [SerializeField] float _trajectoryStartDotSize = 0.14f;
    [SerializeField] float _trajectoryEndDotSize = 0.015f;
    [SerializeField] Color _trajectoryDotColor = new Color(0.95f, 0.06f, 0.03f, 1f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly List<Transform> _trajectoryDots = new List<Transform>();
    readonly List<Renderer> _trajectoryDotRenderers = new List<Renderer>();

    Material _trajectoryDotMaterial;
    MaterialPropertyBlock _dotPropertyBlock;
    Transform _visualTurret;
    Quaternion _initialTurretLocalRotation;
    Quaternion _initialTurretWorldRotation;
    Vector3 _calibratedBarrelLocalForward;
    bool _usingRuntimeTurretPivot;
    bool _hasInitialTurretRotation;
    bool _hasCalibratedBarrelForward;
    Vector3 _pressWorldPoint;
    Vector3 _aimDirection = Vector3.forward;
    float _pressTime;
    float _currentDragDistance;
    float _aimDistance;
    Vector3 _trajectoryTarget;
    bool _isPrimingAim;
    bool _isAiming;
    bool _hasAimDirection;
    bool _hasTrajectoryTarget;

    public event Action<Vector3, float, Vector3> Fired;

    public bool IsAiming => _isAiming;
    public Vector3 AimOrigin => GetAimOrigin();
    public Vector3 AimDirection => _aimDirection;
    public float AimDistance => _aimDistance;
    public Vector3 AimPoint => GetAimOrigin() + _aimDirection * _aimDistance;
    public float TrajectoryLaunchAngle => _trajectoryLaunchAngle;
    public float TrajectoryGravity => _trajectoryGravity;

    void Awake()
    {
        ResolveAimParts();
        EnsureTrajectoryDots();
        SetTrajectoryVisible(false);
    }

    void ResolveAimParts()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_turret == null)
        {
            _visualTurret = FindFirstChildByName(transform, _turretName, "Cannon", "Turret", "head");
            _turret = _useRuntimeTurretPivot
                ? CreateRuntimeTurretPivot(_visualTurret)
                : _visualTurret;
        }
        else if (_visualTurret == null)
        {
            _visualTurret = _turret;
        }

        if (_muzzle == null)
        {
            _muzzle = FindFirstChildByName(transform, _muzzleName, "Muzzle", "gun");
        }

        if (_turret != null && _hasInitialTurretRotation == false)
        {
            _initialTurretLocalRotation = _turret.localRotation;
            _initialTurretWorldRotation = _turret.rotation;
            _hasInitialTurretRotation = true;
            CalibrateBarrelForward();
        }
    }

    Transform CreateRuntimeTurretPivot(Transform visualTurret)
    {
        if (visualTurret == null || visualTurret.parent == null)
        {
            return visualTurret;
        }

        if (visualTurret.parent.name == "Runtime Turret Pivot")
        {
            _usingRuntimeTurretPivot = true;
            return visualTurret.parent;
        }

        GameObject pivotObject = new GameObject("Runtime Turret Pivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(visualTurret.parent, true);
        pivot.position = GetTurretVisualCenter(visualTurret);
        pivot.rotation = visualTurret.parent.rotation;
        pivot.localScale = Vector3.one;

        visualTurret.SetParent(pivot, true);
        _usingRuntimeTurretPivot = true;
        return pivot;
    }

    Vector3 GetTurretVisualCenter(Transform visualTurret)
    {
        Renderer[] renderers = visualTurret.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(visualTurret.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is LineRenderer)
            {
                continue;
            }

            if (hasBounds == false)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderers[i].bounds);
        }

        return hasBounds ? bounds.center : visualTurret.position;
    }

    // The muzzle sits at the barrel tip, so turret -> muzzle measured at rest pose
    // gives the true barrel direction regardless of how the model axes are set up.
    void CalibrateBarrelForward()
    {
        if (_muzzle == null)
        {
            return;
        }

        Vector3 yawAxisWorld = _turret.rotation * SafeNormalized(_turretLocalYawAxis, Vector3.forward);
        Vector3 barrelWorld = Vector3.ProjectOnPlane(_muzzle.position - _turret.position, yawAxisWorld);

        if (barrelWorld.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _calibratedBarrelLocalForward = Quaternion.Inverse(_turret.rotation) * barrelWorld.normalized;

        if (_flipCalibratedBarrel)
        {
            _calibratedBarrelLocalForward = -_calibratedBarrelLocalForward;
        }

        _hasCalibratedBarrelForward = true;
    }

    void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            BeginAimIfPressedOnTank();
        }

        if (_isPrimingAim == false)
        {
            return;
        }

        if (Mouse.current.rightButton.isPressed || Mouse.current.rightButton.wasReleasedThisFrame)
        {
            UpdateAimGesture();
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            EndAimGesture();
        }
    }

    void BeginAimIfPressedOnTank()
    {
        if (TryGetMouseRay(out Ray ray) == false)
        {
            return;
        }

        if (RayHitsSelf(ray) == false)
        {
            return;
        }

        if (TryGetPointerPlanePoint(out _pressWorldPoint) == false)
        {
            return;
        }

        _pressTime = Time.time;
        _currentDragDistance = 0f;
        _aimDistance = 0f;
        _isPrimingAim = true;
        _isAiming = false;
        _hasAimDirection = false;
        SetTrajectoryVisible(false);
    }

    void UpdateAimGesture()
    {
        if (TryGetPointerPlanePoint(out Vector3 currentPoint) == false)
        {
            return;
        }

        Vector3 drag = currentPoint - _pressWorldPoint;
        drag.y = 0f;

        float rawDragDistance = drag.magnitude;
        _currentDragDistance = Mathf.Min(rawDragDistance, _maxDragDistance);

        bool heldLongEnough = Time.time - _pressTime >= _holdToAimDelay;
        bool draggedEnoughToAim = rawDragDistance >= _minAimDragDistance;

        if (_isAiming == false && (heldLongEnough || draggedEnoughToAim))
        {
            _isAiming = true;
        }

        if (_isAiming == false || rawDragDistance <= Mathf.Epsilon)
        {
            return;
        }

        _aimDirection = -drag.normalized;
        _aimDistance = _currentDragDistance * _rangeCoefficient;
        _hasAimDirection = true;

        RotateTurretTowardAim();
        UpdateAimTrajectory();
    }

    void EndAimGesture()
    {
        bool shouldFire = _isAiming && _hasAimDirection && _currentDragDistance >= _minFireDragDistance;

        if (shouldFire)
        {
            FireCurrentAim();
        }

        ResetAim();
    }

    void FireCurrentAim()
    {
        Fired?.Invoke(_aimDirection, _aimDistance, AimPoint);
        Debug.Log($"Tank aim fired. Direction: {_aimDirection}, Distance: {_aimDistance:0.00}, Target: {AimPoint}", this);
    }

    void ResetAim()
    {
        _isPrimingAim = false;
        _isAiming = false;
        _hasAimDirection = false;
        _hasTrajectoryTarget = false;
        _currentDragDistance = 0f;
        _aimDistance = 0f;
        SetTrajectoryVisible(false);
    }

    void RotateTurretTowardAim()
    {
        ResolveAimParts();

        if (_turret == null || _hasAimDirection == false)
        {
            return;
        }

        if (_usingRuntimeTurretPivot)
        {
            RotateRuntimePivotTowardAim();
            return;
        }

        Vector3 yawAxisLocal = SafeNormalized(_turretLocalYawAxis, Vector3.forward);
        Vector3 forwardAxisLocal = _hasCalibratedBarrelForward
            ? _calibratedBarrelLocalForward
            : SafeNormalized(_turretLocalForwardAxis, Vector3.right);

        Quaternion baseRotation = _turret.parent != null
            ? _turret.parent.rotation * _initialTurretLocalRotation
            : _initialTurretWorldRotation;

        Vector3 yawAxis = (baseRotation * yawAxisLocal).normalized;
        Vector3 baseForward = Vector3.ProjectOnPlane(baseRotation * forwardAxisLocal, yawAxis);
        Vector3 targetForward = Vector3.ProjectOnPlane(_aimDirection, yawAxis);

        if (baseForward.sqrMagnitude <= 0.0001f || targetForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float yawAngle = Vector3.SignedAngle(baseForward.normalized, targetForward.normalized, yawAxis);
        Quaternion targetRotation = Quaternion.AngleAxis(yawAngle, yawAxis) * baseRotation;

        _turret.rotation = Quaternion.Slerp(
            _turret.rotation,
            targetRotation,
            Time.deltaTime * _turretRotationSpeed
        );
    }

    void RotateRuntimePivotTowardAim()
    {
        Vector3 baseForward = GetRuntimePivotBarrelForward();
        Vector3 targetForward = _aimDirection;
        baseForward.y = 0f;
        targetForward.y = 0f;

        if (baseForward.sqrMagnitude <= 0.0001f || targetForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float yawAngle = Vector3.SignedAngle(baseForward.normalized, targetForward.normalized, Vector3.up);
        Quaternion targetRotation = Quaternion.AngleAxis(yawAngle, Vector3.up) * _turret.rotation;

        _turret.rotation = Quaternion.Slerp(
            _turret.rotation,
            targetRotation,
            Time.deltaTime * _turretRotationSpeed
        );
    }

    Vector3 GetRuntimePivotBarrelForward()
    {
        if (_muzzle != null && _turret != null)
        {
            Vector3 muzzleDirection = _muzzle.position - _turret.position;
            return _flipCalibratedBarrel ? -muzzleDirection : muzzleDirection;
        }

        return _visualTurret != null ? _visualTurret.forward : transform.forward;
    }

    void EnsureTrajectoryDots()
    {
        if (_showAimTrajectory == false)
        {
            return;
        }

        EnsureTrajectoryMaterial();

        int targetCount = Mathf.Max(0, _trajectoryDotCount);
        while (_trajectoryDots.Count < targetCount)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Aim Trajectory Dot";
            dot.hideFlags = HideFlags.DontSave;
            dot.transform.SetParent(transform, false);

            Collider dotCollider = dot.GetComponent<Collider>();
            if (dotCollider != null)
            {
                Destroy(dotCollider);
            }

            Renderer dotRenderer = dot.GetComponent<Renderer>();
            if (dotRenderer != null)
            {
                dotRenderer.sharedMaterial = _trajectoryDotMaterial;
                dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dotRenderer.receiveShadows = false;
            }

            dot.SetActive(false);
            _trajectoryDots.Add(dot.transform);
            _trajectoryDotRenderers.Add(dotRenderer);
        }

        for (int i = targetCount; i < _trajectoryDots.Count; i++)
        {
            _trajectoryDots[i].gameObject.SetActive(false);
        }
    }

    void EnsureTrajectoryMaterial()
    {
        if (_trajectoryDotMaterial != null)
        {
            return;
        }

        Shader dotShader = Shader.Find("Sprites/Default");
        if (dotShader == null)
        {
            dotShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (dotShader == null)
        {
            dotShader = Shader.Find("Standard");
        }

        if (dotShader != null)
        {
            _trajectoryDotMaterial = new Material(dotShader);
            _trajectoryDotMaterial.name = "Runtime Aim Trajectory Dot";
            _trajectoryDotMaterial.color = _trajectoryDotColor;
        }

        _dotPropertyBlock = new MaterialPropertyBlock();
    }

    void UpdateAimTrajectory()
    {
        if (_showAimTrajectory == false || _hasAimDirection == false || _aimDistance <= 0.01f)
        {
            SetTrajectoryVisible(false);
            return;
        }

        EnsureTrajectoryDots();

        if (_trajectoryDots.Count == 0)
        {
            return;
        }

        float gravity = Mathf.Max(0.01f, _trajectoryGravity);
        float launchAngle = Mathf.Clamp(_trajectoryLaunchAngle, 5f, 85f) * Mathf.Deg2Rad;
        Vector3 origin = GetAimOrigin();
        float range = Mathf.Max(0.1f, _aimDistance);
        float verticalDelta = 0f;

        if (_hasTrajectoryTarget)
        {
            Vector3 targetOffset = _trajectoryTarget - origin;
            targetOffset.y = 0f;
            range = Mathf.Max(0.1f, targetOffset.magnitude);
            verticalDelta = _trajectoryTarget.y - origin.y;
        }

        float cos = Mathf.Cos(launchAngle);
        float denominator = 2f * cos * cos * (range * Mathf.Tan(launchAngle) - verticalDelta);
        float launchSpeed;

        if (denominator > 0.01f)
        {
            launchSpeed = Mathf.Sqrt((gravity * range * range) / denominator);
        }
        else
        {
            float sinDoubleAngle = Mathf.Sin(2f * launchAngle);
            if (sinDoubleAngle <= 0.01f)
            {
                SetTrajectoryVisible(false);
                return;
            }

            launchSpeed = Mathf.Sqrt(range * gravity / sinDoubleAngle);
        }

        float horizontalSpeed = cos * launchSpeed;
        float verticalSpeed = Mathf.Sin(launchAngle) * launchSpeed;

        if (horizontalSpeed <= 0.01f)
        {
            SetTrajectoryVisible(false);
            return;
        }

        float totalFlightTime = range / horizontalSpeed;
        float visibleFlightTime = totalFlightTime * Mathf.Clamp01(_trajectoryVisiblePortion);
        int activeDotCount = GetActiveTrajectoryDotCount(range);

        for (int i = 0; i < _trajectoryDots.Count; i++)
        {
            if (i >= activeDotCount)
            {
                _trajectoryDots[i].gameObject.SetActive(false);
                continue;
            }

            float normalized = (i + 1f) / activeDotCount;
            float time = visibleFlightTime * normalized;
            Vector3 position =
                origin +
                _aimDirection * (horizontalSpeed * time) +
                Vector3.up * (verticalSpeed * time - 0.5f * gravity * time * time);

            float fade = Mathf.InverseLerp(_trajectoryFadeStart, 1f, normalized);
            float alpha = Mathf.Lerp(_trajectoryDotColor.a, 0f, fade);
            float dotSize = Mathf.Lerp(_trajectoryStartDotSize, _trajectoryEndDotSize, fade);

            if (alpha <= 0.02f || dotSize <= 0.001f)
            {
                _trajectoryDots[i].gameObject.SetActive(false);
                continue;
            }

            _trajectoryDots[i].position = position;
            _trajectoryDots[i].localScale = Vector3.one * dotSize;
            _trajectoryDots[i].gameObject.SetActive(true);
            ApplyTrajectoryDotColor(i, alpha);
        }
    }

    int GetActiveTrajectoryDotCount(float range)
    {
        float visibleDistance = range * Mathf.Clamp01(_trajectoryVisiblePortion);
        float spacing = Mathf.Max(0.05f, _trajectoryDotSpacing);
        int activeDotCount = Mathf.CeilToInt(visibleDistance / spacing);
        return Mathf.Clamp(activeDotCount, 3, Mathf.Max(3, _trajectoryDotCount));
    }

    void ApplyTrajectoryDotColor(int dotIndex, float alpha)
    {
        Renderer dotRenderer = _trajectoryDotRenderers[dotIndex];
        if (dotRenderer == null || _dotPropertyBlock == null)
        {
            return;
        }

        Color color = _trajectoryDotColor;
        color.a = alpha;

        _dotPropertyBlock.Clear();
        _dotPropertyBlock.SetColor(BaseColorId, color);
        _dotPropertyBlock.SetColor(ColorId, color);
        dotRenderer.SetPropertyBlock(_dotPropertyBlock);
    }

    void SetTrajectoryVisible(bool isVisible)
    {
        for (int i = 0; i < _trajectoryDots.Count; i++)
        {
            _trajectoryDots[i].gameObject.SetActive(_showAimTrajectory && isVisible);
        }
    }

    bool TryGetPointerPlanePoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (TryGetMouseRay(out Ray ray) == false)
        {
            return false;
        }

        Plane aimPlane = new Plane(Vector3.up, GetAimOrigin());
        if (aimPlane.Raycast(ray, out float enter) == false)
        {
            return false;
        }

        point = ray.GetPoint(enter);
        return true;
    }

    bool TryGetMouseRay(out Ray ray)
    {
        ray = new Ray();

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null || Mouse.current == null)
        {
            return false;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        ray = _mainCamera.ScreenPointToRay(mousePosition);
        return true;
    }

    bool RayHitsSelf(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, _rayDistance, _aimStartMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return true;
            }
        }

        return RayIntersectsRenderBounds(ray);
    }

    bool RayIntersectsRenderBounds(Ray ray)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Bounds renderBounds = new Bounds();
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is LineRenderer)
            {
                continue;
            }

            if (hasBounds == false)
            {
                renderBounds = renderers[i].bounds;
                hasBounds = true;
                continue;
            }

            renderBounds.Encapsulate(renderers[i].bounds);
        }

        return hasBounds && renderBounds.IntersectRay(ray, out float distance) && distance <= _rayDistance;
    }

    Vector3 GetAimOrigin()
    {
        if (_muzzle != null)
        {
            return _muzzle.position;
        }

        if (_turret != null)
        {
            return _turret.position;
        }

        return transform.position;
    }

    Transform FindChildByName(Transform root, string childName)
    {
        if (string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform match = FindChildByName(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    Transform FindFirstChildByName(Transform root, params string[] childNames)
    {
        for (int i = 0; i < childNames.Length; i++)
        {
            Transform match = FindChildByName(root, childNames[i]);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    Vector3 SafeNormalized(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude <= 0.0001f)
        {
            return fallback.normalized;
        }

        return value.normalized;
    }

    public void SetRangeCoefficient(float coefficient)
    {
        _rangeCoefficient = Mathf.Max(0f, coefficient);
    }

    public void AimAt(Vector3 worldTarget, bool showTrajectory)
    {
        ResolveAimParts();
        Vector3 origin = GetAimOrigin();
        Vector3 direction = worldTarget - origin;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _aimDirection = direction.normalized;
        _aimDistance = Mathf.Max(0f, direction.magnitude);
        _trajectoryTarget = worldTarget;
        _hasTrajectoryTarget = true;
        _hasAimDirection = true;
        _isPrimingAim = false;
        _isAiming = showTrajectory;

        RotateTurretTowardAim();

        if (showTrajectory)
        {
            UpdateAimTrajectory();
        }
        else
        {
            SetTrajectoryVisible(false);
        }
    }

    public void SetScriptedAim(Vector3 direction, float distance, bool showTrajectory)
    {
        ResolveAimParts();
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _aimDirection = direction.normalized;
        _aimDistance = Mathf.Max(0f, distance);
        _hasTrajectoryTarget = false;
        _hasAimDirection = true;
        _isPrimingAim = false;
        _isAiming = showTrajectory;

        RotateTurretTowardAim();

        if (showTrajectory)
        {
            UpdateAimTrajectory();
        }
        else
        {
            SetTrajectoryVisible(false);
        }
    }

    public void ClearScriptedAim()
    {
        ResetAim();
    }
}
