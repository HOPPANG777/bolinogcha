using UnityEngine;
using UnityEngine.InputSystem;

public class MoveController : MonoBehaviour
{
    [Header("Component")]
    [SerializeField] CharacterController _characterController;
    [SerializeField] Camera _mainCamera;

    [Header("Move")]
    [SerializeField] float _moveSpeed = 5f;
    [SerializeField] float _rotationSpeed = 12f;
    [SerializeField] float _stopDistance = 0.1f;

    [Header("Ground")]
    [SerializeField] LayerMask _groundLayer;
    [SerializeField] bool _blockMoveWhenClickingSelf = true;
    [SerializeField] bool _allowMouseInput = true;

    [Header("Gravity")]
    [SerializeField] float _gravity = -9.81f;

    Vector3 _targetPosition;
    Vector3 _velocity;
    bool _hasTarget;

    public bool HasTarget => _hasTarget;
    public Vector3 TargetPosition => _targetPosition;
    public float MoveSpeed => _moveSpeed;

    public float RemainingFlatDistance
    {
        get
        {
            Vector3 direction = _targetPosition - transform.position;
            direction.y = 0f;
            return direction.magnitude;
        }
    }

    void Start()
    {
        ResolveCharacterController();
        _targetPosition = transform.position;
    }

    void Update()
    {
        if (_allowMouseInput)
        {
            CheckMouseInput();
        }

        MoveToTarget(Time.deltaTime);
        ApplyGravity(Time.deltaTime);
    }

    public void Configure(CharacterController characterController, Camera mainCamera, LayerMask groundLayer)
    {
        _characterController = characterController;
        _mainCamera = mainCamera;
        _groundLayer = groundLayer;
    }

    public void SetMouseInputEnabled(bool enabled)
    {
        _allowMouseInput = enabled;
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        _moveSpeed = Mathf.Max(0.01f, moveSpeed);
    }

    public void MoveTo(Vector3 targetPosition)
    {
        _targetPosition = targetPosition;
        _targetPosition.y = transform.position.y;
        _hasTarget = true;
    }

    public void StopMove()
    {
        _targetPosition = transform.position;
        _hasTarget = false;
    }

    public void TickScriptedMove(float deltaTime)
    {
        MoveToTarget(deltaTime);
        ApplyGravity(deltaTime);
    }

    public void TickScriptedMove(float deltaTime, float lockedY)
    {
        MoveToTarget(deltaTime, false);

        Vector3 position = transform.position;
        position.y = lockedY;
        transform.position = position;
        _velocity = Vector3.zero;
    }

    void CheckMouseInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame == false)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

        if (IsPointerOnSelfBeforeGround(ray))
        {
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayer))
        {
            MoveTo(hit.point);
        }
    }

    bool IsPointerOnSelfBeforeGround(Ray ray)
    {
        if (_blockMoveWhenClickingSelf == false)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        float closestSelfDistance = float.PositiveInfinity;
        float closestGroundDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                closestSelfDistance = Mathf.Min(closestSelfDistance, hit.distance);
                continue;
            }

            if (IsInLayerMask(hit.collider.gameObject.layer, _groundLayer))
            {
                closestGroundDistance = Mathf.Min(closestGroundDistance, hit.distance);
            }
        }

        if (TryGetSelfRenderBoundsDistance(ray, out float renderBoundsDistance))
        {
            closestSelfDistance = Mathf.Min(closestSelfDistance, renderBoundsDistance);
        }

        return closestSelfDistance < closestGroundDistance;
    }

    bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    bool TryGetSelfRenderBoundsDistance(Ray ray, out float distance)
    {
        distance = 0f;

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

        return hasBounds && renderBounds.IntersectRay(ray, out distance);
    }

    void MoveToTarget(float deltaTime)
    {
        MoveToTarget(deltaTime, true);
    }

    void MoveToTarget(float deltaTime, bool useCharacterController)
    {
        if (_hasTarget == false)
        {
            return;
        }

        ResolveCharacterController();

        Vector3 direction = _targetPosition - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;
        if (distance <= _stopDistance)
        {
            _hasTarget = false;
            return;
        }

        direction.Normalize();

        Vector3 moveDelta = direction * Mathf.Min(_moveSpeed * deltaTime, distance);
        if (useCharacterController && _characterController != null)
        {
            _characterController.Move(moveDelta);
        }
        else
        {
            transform.position += moveDelta;
        }

        Quaternion baseRotation = Quaternion.LookRotation(direction);
        Quaternion modelOffset = Quaternion.Euler(-90f, 90f, 0f);
        Quaternion targetRotation = baseRotation * modelOffset;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            deltaTime * _rotationSpeed);
    }

    void ApplyGravity(float deltaTime)
    {
        ResolveCharacterController();

        if (_characterController == null)
        {
            return;
        }

        if (_characterController.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f;
        }

        _velocity.y += _gravity * deltaTime;
        _characterController.Move(_velocity * deltaTime);
    }

    void ResolveCharacterController()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }
    }
}
