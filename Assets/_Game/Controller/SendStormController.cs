using UnityEngine;

public class SandStormController : MonoBehaviour
{
    [Header("Particle")]
    [SerializeField] ParticleSystem _particleSystem;

    [Header("Wind")]
    [SerializeField] Vector3 _windDirection = Vector3.right;
    [SerializeField] float _windStrength = 5f;
    [SerializeField] float _directionChangeSpeed = 3f;

    ParticleSystem.VelocityOverLifetimeModule _velocityModule;
    Vector3 _currentWindDirection = Vector3.right;

    void Awake()
    {
        if (_particleSystem == null)
        {
            _particleSystem = GetComponentInChildren<ParticleSystem>();
        }

        Configure(_particleSystem);
    }

    void Update()
    {
        if (_particleSystem == null)
        {
            return;
        }

        UpdateWindDirection();
        UpdateParticleVelocity();
        UpdateEffectRotation();
    }

    public void Configure(ParticleSystem particleSystem)
    {
        _particleSystem = particleSystem;
        if (_particleSystem == null)
        {
            Debug.LogWarning("ParticleSystem is missing.", this);
            enabled = false;
            return;
        }

        enabled = true;
        _velocityModule = _particleSystem.velocityOverLifetime;
        _velocityModule.enabled = true;
        _velocityModule.space = ParticleSystemSimulationSpace.World;
        _currentWindDirection = _windDirection == Vector3.zero ? Vector3.right : _windDirection.normalized;
    }

    public void SetWind(Vector3 direction, float strength)
    {
        if (direction == Vector3.zero)
        {
            return;
        }

        _windDirection = direction.normalized;
        _windStrength = strength;
    }

    void UpdateWindDirection()
    {
        if (_windDirection == Vector3.zero)
        {
            return;
        }

        _currentWindDirection = Vector3.Lerp(
            _currentWindDirection,
            _windDirection.normalized,
            Time.deltaTime * _directionChangeSpeed);
    }

    void UpdateParticleVelocity()
    {
        Vector3 velocity = _currentWindDirection * _windStrength;
        _velocityModule.x = velocity.x;
        _velocityModule.y = velocity.y;
        _velocityModule.z = velocity.z;
    }

    void UpdateEffectRotation()
    {
        if (_currentWindDirection == Vector3.zero)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(_currentWindDirection);
    }
}
