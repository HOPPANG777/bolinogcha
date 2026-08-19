using UnityEngine;

public class TankProjectileLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TankAimController _aimController;
    [SerializeField] GameObject _projectilePrefab;
    [SerializeField] Transform _firePoint;
    [SerializeField] Camera _mainCamera;
    [SerializeField] ProjectileKillCamController _killCamController;

    [Header("Collision")]
    [SerializeField] LayerMask _groundLayer;

    [Header("Flight")]
    [SerializeField] bool _useAimTrajectorySettings = true;
    [SerializeField] float _launchAngle = 55f;
    [SerializeField] float _gravity = 9.81f;

    [Header("Impact")]
    [SerializeField] GameObject _explosionPrefab;
    [SerializeField] float _explosionLifetime = 6f;
    [SerializeField] float _damage = 30f;
    [SerializeField] float _splashRadius = 2.5f;
    [SerializeField] LayerMask _damageMask = ~0;

    [Header("Fallback Projectile")]
    [SerializeField] bool _createFallbackProjectile = true;
    [SerializeField] float _fallbackProjectileScale = 0.35f;
    [SerializeField] Color _fallbackProjectileColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    void Awake()
    {
        if (_aimController == null)
        {
            _aimController = GetComponent<TankAimController>();
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_killCamController == null && _mainCamera != null)
        {
            _killCamController = _mainCamera.GetComponent<ProjectileKillCamController>();
        }

        if (_killCamController == null && _mainCamera != null)
        {
            _killCamController = _mainCamera.gameObject.AddComponent<ProjectileKillCamController>();
        }
    }

    void OnEnable()
    {
        if (_aimController == null)
        {
            _aimController = GetComponent<TankAimController>();
        }

        if (_aimController != null)
        {
            _aimController.Fired += LaunchProjectile;
        }
    }

    void OnDisable()
    {
        if (_aimController != null)
        {
            _aimController.Fired -= LaunchProjectile;
        }
    }

    void LaunchProjectile(Vector3 direction, float distance, Vector3 targetPoint)
    {
        BallisticProjectile projectile = CreateProjectile(null);
        if (projectile == null)
        {
            return;
        }

        Vector3 origin = GetFirePoint();
        float launchAngle = _useAimTrajectorySettings && _aimController != null
            ? _aimController.TrajectoryLaunchAngle
            : _launchAngle;
        float gravity = _useAimTrajectorySettings && _aimController != null
            ? _aimController.TrajectoryGravity
            : _gravity;

        projectile.ConfigureImpact(_explosionPrefab, _explosionLifetime, _damage, _splashRadius, _damageMask, transform);
        projectile.Launch(origin, direction, distance, launchAngle, gravity, _groundLayer);

        if (_killCamController != null)
        {
            _killCamController.BeginKillCam(projectile, _groundLayer);
        }
    }

    public BallisticProjectile LaunchScripted(
        Vector3 direction,
        float distance,
        float launchAngle,
        float gravity,
        GameObject projectilePrefabOverride,
        GameObject explosionPrefabOverride,
        float damage,
        float splashRadius,
        LayerMask groundLayer,
        LayerMask damageMask,
        Transform owner,
        bool useKillCam)
    {
        return LaunchScripted(
            direction,
            distance,
            launchAngle,
            gravity,
            projectilePrefabOverride,
            explosionPrefabOverride,
            damage,
            splashRadius,
            groundLayer,
            damageMask,
            owner,
            useKillCam,
            false,
            Vector3.zero);
    }

    public BallisticProjectile LaunchScripted(
        Vector3 direction,
        float distance,
        float launchAngle,
        float gravity,
        GameObject projectilePrefabOverride,
        GameObject explosionPrefabOverride,
        float damage,
        float splashRadius,
        LayerMask groundLayer,
        LayerMask damageMask,
        Transform owner,
        bool useKillCam,
        Vector3 targetPosition)
    {
        return LaunchScripted(
            direction,
            distance,
            launchAngle,
            gravity,
            projectilePrefabOverride,
            explosionPrefabOverride,
            damage,
            splashRadius,
            groundLayer,
            damageMask,
            owner,
            useKillCam,
            true,
            targetPosition);
    }

    BallisticProjectile LaunchScripted(
        Vector3 direction,
        float distance,
        float launchAngle,
        float gravity,
        GameObject projectilePrefabOverride,
        GameObject explosionPrefabOverride,
        float damage,
        float splashRadius,
        LayerMask groundLayer,
        LayerMask damageMask,
        Transform owner,
        bool useKillCam,
        bool useExactTarget,
        Vector3 targetPosition)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        direction.Normalize();

        BallisticProjectile projectile = CreateProjectile(projectilePrefabOverride);
        if (projectile == null)
        {
            return null;
        }

        LayerMask resolvedGroundLayer = groundLayer.value != 0 ? groundLayer : _groundLayer;
        LayerMask resolvedDamageMask = damageMask.value != 0 ? damageMask : _damageMask;
        GameObject resolvedExplosionPrefab = explosionPrefabOverride != null ? explosionPrefabOverride : _explosionPrefab;
        Transform resolvedOwner = owner != null ? owner : transform;

        projectile.ConfigureImpact(
            resolvedExplosionPrefab,
            _explosionLifetime,
            damage,
            splashRadius,
            resolvedDamageMask,
            resolvedOwner);

        Vector3 firePoint = GetScriptedFirePoint(direction);
        if (useExactTarget)
        {
            projectile.LaunchAtTarget(
                firePoint,
                targetPosition,
                launchAngle,
                gravity,
                resolvedGroundLayer);
        }
        else
        {
            projectile.Launch(
                firePoint,
                direction,
                Mathf.Max(0.1f, distance),
                launchAngle,
                gravity,
                resolvedGroundLayer);
        }

        if (useKillCam && _killCamController != null)
        {
            _killCamController.BeginKillCam(projectile, resolvedGroundLayer);
        }

        return projectile;
    }

    BallisticProjectile CreateProjectile(GameObject projectilePrefabOverride)
    {
        GameObject projectileObject = null;
        GameObject resolvedPrefab = projectilePrefabOverride != null ? projectilePrefabOverride : _projectilePrefab;

        if (resolvedPrefab != null)
        {
            projectileObject = Instantiate(resolvedPrefab);
        }
        else if (_createFallbackProjectile)
        {
            projectileObject = CreateFallbackProjectile();
        }

        if (projectileObject == null)
        {
            Debug.LogWarning("Projectile prefab is missing.", this);
            return null;
        }

        BallisticProjectile projectile = projectileObject.GetComponent<BallisticProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<BallisticProjectile>();
        }

        return projectile;
    }

    GameObject CreateFallbackProjectile()
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        projectile.name = "Fallback Missile";
        projectile.transform.localScale = Vector3.one * _fallbackProjectileScale;

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                Material material = new Material(shader);
                material.color = _fallbackProjectileColor;
                renderer.sharedMaterial = material;
            }
        }

        return projectile;
    }

    Vector3 GetFirePoint()
    {
        if (_firePoint != null)
        {
            return _firePoint.position;
        }

        if (_aimController != null)
        {
            return _aimController.AimOrigin;
        }

        return transform.position + Vector3.up;
    }

    Vector3 GetScriptedFirePoint(Vector3 direction)
    {
        if (_firePoint != null)
        {
            return _firePoint.position;
        }

        if (_aimController != null && _aimController.AimOrigin != transform.position)
        {
            return _aimController.AimOrigin;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        return transform.position + Vector3.up * 1.05f + direction.normalized * 0.8f;
    }
}
