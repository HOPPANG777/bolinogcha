using UnityEngine;

public class QuarterViewCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform _target;

    [Header("Camera Offset")]
    [SerializeField] Vector3 _offset = new Vector3(0f, 10f, -10f);
    [SerializeField] bool _rotateOffsetWithTargetYaw;

    [Header("Camera Rotation")]
    [SerializeField] Vector3 _rotation = new Vector3(45f, 0f, 0f);
    [SerializeField] bool _lookAtTarget;
    [SerializeField] Transform _lookTarget;
    [SerializeField] Vector3 _lookOffset = new Vector3(0f, 1.5f, 8f);

    [Header("Follow")]
    [SerializeField] float _followSpeed = 10f;
    [SerializeField] float _rotationSpeed = 12f;
    [SerializeField] bool _snapOnEnable = true;

    bool _hasSnapped;

    void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        FollowTarget();
        RotateCamera();
    }

    void FollowTarget()
    {
        Vector3 targetPosition = _target.position + GetOffset();

        if (_snapOnEnable && _hasSnapped == false)
        {
            transform.position = targetPosition;
            _hasSnapped = true;
            return;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * _followSpeed);
    }

    void RotateCamera()
    {
        Quaternion targetRotation = _lookAtTarget
            ? Quaternion.LookRotation((GetLookPoint() - transform.position).normalized, Vector3.up)
            : Quaternion.Euler(_rotation);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
    }

    Vector3 GetOffset()
    {
        if (_rotateOffsetWithTargetYaw == false)
        {
            return _offset;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, _target.eulerAngles.y, 0f);
        return yawRotation * _offset;
    }

    Vector3 GetLookPoint()
    {
        if (_lookTarget != null)
        {
            return _lookTarget.position + Vector3.up * _lookOffset.y;
        }

        if (_rotateOffsetWithTargetYaw)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, _target.eulerAngles.y, 0f);
            return _target.position + yawRotation * _lookOffset;
        }

        return _target.position + _lookOffset;
    }
}
