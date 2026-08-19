using System.Collections;
using UnityEngine;

public class StoryboardDirector : MonoBehaviour
{
    public enum StoryboardActor
    {
        Yellow,
        Green
    }

    public enum StoryboardAction
    {
        Hold,
        Move,
        FireCharged,
        FireGravity,
        MoveThenFireCharged,
        MoveThenFireGravity,
        SpawnObstacle,
        Reload
    }

    public enum StoryboardTarget
    {
        ManualPosition,
        GreenSideDestructible,
        YellowSideDestructible,
        UtilityWall,
        YellowTank,
        GreenTank
    }

    [System.Serializable]
    public class MapSet
    {
        public string name;
        public GameObject root;
    }

    [System.Serializable]
    public class StoryBeat
    {
        public string name;

        [TextArea(2, 4)]
        public string caption;

        public int mapSetIndex;
        public StoryboardActor actor;
        public StoryboardAction action;
        public Vector3 yellowPosition;
        public Vector3 greenPosition;
        public Vector3 movePosition;
        public Vector3 targetPosition;
        public Vector3 obstaclePosition;
        public StoryboardTarget targetOverride;
        public float duration = 1f;
        public float postDelay = 0.6f;
        public float launchAngle = 55f;
        public float damage = 30f;
        public float splashRadius = 2.5f;
        public float powerMultiplier = 1f;
        public Vector3 windDirection = Vector3.right;
        public float windStrength = 5f;
        public bool usePrimaryDestructibleAsTarget;
        public bool destroyDestructiblesOnImpact;
        public bool swapGreenTankWithDamageOnImpact;
        public float destructibleImpactRadius = 1.6f;
    }

    const string DamageTankPrefabEditorPath = "Assets/_Game/Model/Tank/Prefab/Tank_Damage_Prefab.prefab";
    const string DamageWallPrefabEditorPath = "Assets/_Game/Model/Wall/유틸벽_Damage_Prefab.prefab";

    [Header("Scene")]
    [SerializeField] MapSet[] _mapSets = new MapSet[0];
    [SerializeField] StoryBeat[] _beats = new StoryBeat[0];
    [SerializeField] Transform _yellowTank;
    [SerializeField] Transform _greenTank;
    [SerializeField] MoveController _yellowMoveController;
    [SerializeField] MoveController _greenMoveController;
    [SerializeField] TankAimController _yellowAimController;
    [SerializeField] TankAimController _greenAimController;
    [SerializeField] TankProjectileLauncher _yellowProjectileLauncher;
    [SerializeField] TankProjectileLauncher _greenProjectileLauncher;
    [SerializeField] Transform _primaryDestructibleProp;
    [SerializeField] Transform _yellowSideDestructibleProp;
    [SerializeField] Transform _yellowSideDestructibleAimTarget;
    [SerializeField] Transform _cameraTarget;
    [SerializeField] Camera _mainCamera;
    [SerializeField] ProjectileKillCamController _killCamController;
    [SerializeField] SandStormController _sandStorm;

    [Header("Prefabs")]
    [SerializeField] GameObject _chargedProjectilePrefab;
    [SerializeField] GameObject _gravityProjectilePrefab;
    [SerializeField] GameObject _obstacleWallPrefab;
    [SerializeField] GameObject _obstacleWallDamagePrefab;
    [SerializeField] GameObject _damageTankPrefab;
    [SerializeField] GameObject _explosionPrefab;

    [Header("Physics")]
    [SerializeField] LayerMask _groundLayer;
    [SerializeField] LayerMask _damageMask = ~0;
    [SerializeField] float _projectileGravity = 9.81f;
    [SerializeField] float _projectileImpactWait = 8f;

    [Header("Playback")]
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _loop;
    [SerializeField] bool _showCaptions;
    [SerializeField] float _mapSwitchDelay = 0.25f;
    [SerializeField] float _tankGroundY = 0.05f;
    [SerializeField] float _projectileGroundY = 0f;
    [SerializeField] float _tankTurnSpeed = 8f;
    [SerializeField] Vector3 _tankModelRotationOffset = new Vector3(-90f, 90f, 0f);
    [SerializeField] Vector3 _fallbackCameraOffset = new Vector3(0f, 12f, -16f);
    [SerializeField] bool _lockCameraToArenaCenter;
    [SerializeField] Vector3 _arenaCenter = Vector3.zero;
    [SerializeField] bool _cameraFollowsYellowTank = true;
    [SerializeField, Range(0f, 0.85f)] float _cameraLookAheadWeight = 0.15f;
    [SerializeField] float _cameraTargetSmooth = 5f;
    [SerializeField] float _cameraSnapDistance = 18f;
    [SerializeField] float _cameraTargetHeight = 0.7f;
    [SerializeField] bool _useProjectileKillCam;
    [SerializeField] bool _useProjectileKillCamForPlayerShots = true;
    [SerializeField] bool _useProjectileKillCamForEnemyShots;
    [SerializeField] bool _useMoveControllerForScriptedMoves = true;
    [SerializeField] bool _useTankAimControllerForScriptedAim = true;
    [SerializeField] bool _useTankProjectileLauncherForScriptedShots = true;
    [SerializeField] float _scriptedAimPreviewDuration = 1f;
    [SerializeField] float _preActionPause = 0.2f;
    [SerializeField] float _betweenMoveAndFirePause = 0.3f;
    [SerializeField] float _postAimPause = 0.2f;
    [SerializeField] float _postImpactPause = 0.25f;
    [SerializeField] float _killCamPostImpactPause = 1.35f;
    [SerializeField] bool _switchMapAfterBeatAction = true;

    [Header("Runtime Effects")]
    [SerializeField, Range(0.1f, 1f)] float _projectileVisualScale = 0.55f;
    [SerializeField, Range(0.01f, 0.2f)] float _scriptedProjectileHitRadius = 0.04f;
    [SerializeField] float _obstacleWallScale = 1.6f;
    [SerializeField] bool _placeObstacleInFrontOfActor = true;
    [SerializeField] float _obstacleWallDistanceFromTank = 2.1f;
    [SerializeField] float _obstacleWallYawOffset;
    [SerializeField] Vector3 _damagedWallLayDownEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] float _damagedWallGroundOffset = 0.03f;
    [SerializeField] Color _fallbackGravityColor = new Color(0.18f, 0.24f, 0.32f, 1f);
    [SerializeField] Color _fallbackChargedColor = new Color(0.1f, 0.8f, 1f, 1f);

    Coroutine _playRoutine;
    string _currentBeatName;
    string _currentCaption;
    int _activeMapSet = -1;
    GUIStyle _captionTitleStyle;
    GUIStyle _captionBodyStyle;
    Texture2D _captionBackground;
    bool _hasCameraTargetPosition;
    Transform _cameraFollowActorOverride;
    Transform _runtimeObjectRoot;
    Transform _lastObstacleWall;
    Vector3 _lastObstacleWallAimPoint;
    bool _hasLastObstacleWallAimPoint;
    bool _cameraActionPointOverride;
    Vector3 _cameraActionPointForward = Vector3.forward;

    void Awake()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_killCamController == null && _mainCamera != null)
        {
            _killCamController = _mainCamera.GetComponent<ProjectileKillCamController>();
        }

        ResolveControllerReferences();
        ActivateMapSet(0);
    }

    void Start()
    {
        ResolveControllerReferences();

        if (_playOnStart)
        {
            StartPlayback();
        }
    }

    public void Configure(
        MapSet[] mapSets,
        StoryBeat[] beats,
        Transform yellowTank,
        Transform greenTank,
        Transform cameraTarget,
        Camera mainCamera,
        ProjectileKillCamController killCamController,
        GameObject chargedProjectilePrefab,
        GameObject gravityProjectilePrefab,
        GameObject obstacleWallPrefab,
        GameObject obstacleWallDamagePrefab,
        GameObject damageTankPrefab,
        GameObject explosionPrefab,
        SandStormController sandStorm,
        LayerMask groundLayer,
        LayerMask damageMask)
    {
        _mapSets = mapSets;
        _beats = beats;
        _yellowTank = yellowTank;
        _greenTank = greenTank;
        _cameraTarget = cameraTarget;
        _mainCamera = mainCamera;
        _killCamController = killCamController;
        _chargedProjectilePrefab = chargedProjectilePrefab;
        _gravityProjectilePrefab = gravityProjectilePrefab;
        _obstacleWallPrefab = obstacleWallPrefab;
        _obstacleWallDamagePrefab = obstacleWallDamagePrefab;
        _damageTankPrefab = damageTankPrefab;
        _explosionPrefab = explosionPrefab;
        _sandStorm = sandStorm;
        _groundLayer = groundLayer;
        _damageMask = damageMask;
        ResolveControllerReferences();
    }

    public void SetPrimaryDestructible(Transform primaryDestructible)
    {
        _primaryDestructibleProp = primaryDestructible;
        if (_primaryDestructibleProp != null &&
            _primaryDestructibleProp.GetComponent<StoryboardDestructible>() == null)
        {
            _primaryDestructibleProp.gameObject.AddComponent<StoryboardDestructible>();
        }
    }

    public void SetYellowSideDestructible(Transform yellowSideDestructible, Transform aimTarget = null)
    {
        _yellowSideDestructibleProp = yellowSideDestructible;
        _yellowSideDestructibleAimTarget = aimTarget != null ? aimTarget : yellowSideDestructible;
        if (_yellowSideDestructibleProp != null &&
            _yellowSideDestructibleProp.GetComponent<StoryboardDestructible>() == null)
        {
            _yellowSideDestructibleProp.gameObject.AddComponent<StoryboardDestructible>();
        }
    }

    void ResolveControllerReferences()
    {
        _yellowMoveController = ResolveComponent(_yellowTank, _yellowMoveController);
        _greenMoveController = ResolveComponent(_greenTank, _greenMoveController);
        _yellowAimController = ResolveComponent(_yellowTank, _yellowAimController);
        _greenAimController = ResolveComponent(_greenTank, _greenAimController);
        _yellowProjectileLauncher = ResolveComponent(_yellowTank, _yellowProjectileLauncher);
        _greenProjectileLauncher = ResolveComponent(_greenTank, _greenProjectileLauncher);
    }

    T ResolveComponent<T>(Transform root, T current) where T : Component
    {
        if (current != null || root == null)
        {
            return current;
        }

        return root.GetComponent<T>();
    }

    [ContextMenu("Start Playback")]
    public void StartPlayback()
    {
        ResolveControllerReferences();

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
        }

        _playRoutine = StartCoroutine(PlaybackRoutine());
    }

    [ContextMenu("Stop Playback")]
    public void StopPlayback()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _currentBeatName = string.Empty;
        _currentCaption = string.Empty;
    }

    IEnumerator PlaybackRoutine()
    {
        if (_beats == null || _beats.Length == 0)
        {
            yield break;
        }

        ClearRuntimeObjects();

        do
        {
            for (int i = 0; i < _beats.Length; i++)
            {
                StoryBeat beat = _beats[i];
                if (beat == null)
                {
                    continue;
                }

                _currentBeatName = string.IsNullOrEmpty(beat.name) ? "Storyboard Beat" : beat.name;
                _currentCaption = beat.caption;

                if (_switchMapAfterBeatAction == false || i == 0)
                {
                    bool changedBeforeAction = ActivateMapSet(beat.mapSetIndex);
                    if (_switchMapAfterBeatAction == false && changedBeforeAction && _mapSwitchDelay > 0f)
                    {
                        yield return new WaitForSeconds(_mapSwitchDelay);
                    }
                }

                UpdateSandStorm(beat);
                PrepareTankPositions(beat, i == 0);
                UpdateCameraTarget(GetFocusPosition(beat));

                if (beat.action != StoryboardAction.Hold && _preActionPause > 0f)
                {
                    yield return new WaitForSeconds(_preActionPause);
                }

                switch (beat.action)
                {
                    case StoryboardAction.Move:
                        yield return MoveActor(beat.actor, beat.targetPosition, beat.duration);
                        break;
                    case StoryboardAction.MoveThenFireCharged:
                        yield return MoveActor(beat.actor, GetMovePosition(beat), beat.duration);
                        if (_betweenMoveAndFirePause > 0f)
                        {
                            yield return new WaitForSeconds(_betweenMoveAndFirePause);
                        }

                        yield return FireProjectile(beat, false);
                        break;
                    case StoryboardAction.MoveThenFireGravity:
                        yield return MoveActor(beat.actor, GetMovePosition(beat), beat.duration);
                        if (_betweenMoveAndFirePause > 0f)
                        {
                            yield return new WaitForSeconds(_betweenMoveAndFirePause);
                        }

                        yield return FireProjectile(beat, true);
                        break;
                    case StoryboardAction.FireCharged:
                        yield return FireProjectile(beat, false);
                        break;
                    case StoryboardAction.FireGravity:
                        yield return FireProjectile(beat, true);
                        break;
                    case StoryboardAction.SpawnObstacle:
                        yield return SpawnObstacleRoutine(beat);
                        break;
                    case StoryboardAction.Reload:
                        SpawnReloadPulse(GetActor(beat.actor));
                        yield return new WaitForSeconds(Mathf.Max(0.1f, beat.duration));
                        break;
                    default:
                        yield return new WaitForSeconds(Mathf.Max(0.1f, beat.duration));
                        break;
                }

                if (_switchMapAfterBeatAction)
                {
                    bool changedAfterAction = ActivateMapSet(beat.mapSetIndex);
                    if (changedAfterAction && _mapSwitchDelay > 0f)
                    {
                        yield return new WaitForSeconds(_mapSwitchDelay);
                    }
                }

                if (beat.postDelay > 0f)
                {
                    yield return new WaitForSeconds(beat.postDelay);
                }
            }
        }
        while (_loop);

        _playRoutine = null;
    }

    bool ActivateMapSet(int index)
    {
        if (_mapSets == null || _mapSets.Length == 0)
        {
            return false;
        }

        int clampedIndex = Mathf.Clamp(index, 0, _mapSets.Length - 1);
        if (_activeMapSet == clampedIndex)
        {
            return false;
        }

        for (int i = 0; i < _mapSets.Length; i++)
        {
            if (_mapSets[i] != null && _mapSets[i].root != null)
            {
                _mapSets[i].root.SetActive(i == clampedIndex);
            }
        }

        _activeMapSet = clampedIndex;
        return true;
    }

    void PrepareTankPositions(StoryBeat beat, bool forceSnap)
    {
        if (forceSnap == false)
        {
            return;
        }

        SetActorPosition(StoryboardActor.Yellow, beat.yellowPosition);
        SetActorPosition(StoryboardActor.Green, beat.greenPosition);
    }

    void SetActorPosition(StoryboardActor actor, Vector3 position)
    {
        Transform tank = GetActor(actor);
        if (tank == null)
        {
            return;
        }

        tank.position = Grounded(position);
        LookAt(tank, GetOtherActor(actor) != null ? GetOtherActor(actor).position : tank.position + Vector3.forward, 1f);
    }

    IEnumerator MoveActor(StoryboardActor actor, Vector3 targetPosition, float duration)
    {
        Transform tank = GetActor(actor);
        if (tank == null)
        {
            yield break;
        }

        Vector3 start = tank.position;
        Vector3 target = Grounded(targetPosition);
        float moveDuration = Mathf.Max(0.05f, duration);
        MoveController moveController = GetMoveController(actor);
        Transform previousCameraOverride = _cameraFollowActorOverride;
        _cameraFollowActorOverride = tank;

        if (_useMoveControllerForScriptedMoves && moveController != null)
        {
            moveController.enabled = false;
            moveController.SetMouseInputEnabled(false);
            moveController.SetMoveSpeed(Vector3.Distance(start, target) / moveDuration);
            moveController.MoveTo(target);

            float elapsed = 0f;
            while (moveController.HasTarget && elapsed < moveDuration + 0.75f)
            {
                elapsed += Time.deltaTime;
                moveController.TickScriptedMove(Time.deltaTime, _tankGroundY);
                UpdateCameraTarget(Vector3.Lerp(tank.position, GetOtherActorPosition(actor), 0.35f));
                yield return null;
            }

            moveController.StopMove();
            tank.position = target;
            LookAt(tank, target + (target - start), 1f);
            _cameraFollowActorOverride = previousCameraOverride;
            yield break;
        }

        float fallbackElapsed = 0f;
        while (fallbackElapsed < moveDuration)
        {
            fallbackElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fallbackElapsed / moveDuration);
            Vector3 position = Vector3.Lerp(start, target, SmoothStep(t));
            tank.position = position;
            LookAt(tank, target, Time.deltaTime * _tankTurnSpeed);
            UpdateCameraTarget(Vector3.Lerp(position, GetOtherActorPosition(actor), 0.35f));
            yield return null;
        }

        tank.position = target;
        LookAt(tank, target + (target - start), 1f);
        _cameraFollowActorOverride = previousCameraOverride;
    }

    IEnumerator FireProjectile(StoryBeat beat, bool isGravity)
    {
        Transform actor = GetActor(beat.actor);
        if (actor == null)
        {
            yield break;
        }

        Vector3 target = GetProjectileTargetPosition(beat, actor);
        Vector3 direction = target - actor.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = actor.forward;
            direction.y = 0f;
        }

        direction.Normalize();
        Vector3 origin = GetFireOrigin(actor, direction);
        float range = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(target.x, 0f, target.z));
        bool useKillCam = ShouldUseProjectileKillCam(beat.actor);
        yield return AimActor(actor, direction, Mathf.Max(0.1f, _scriptedAimPreviewDuration), range, true, true, target);

        if (_postAimPause > 0f)
        {
            yield return new WaitForSeconds(_postAimPause);
        }

        ClearAimPreview(actor);

        float multiplier = Mathf.Max(0.01f, beat.powerMultiplier);
        float splashRadius = Mathf.Max(0.1f, beat.splashRadius * multiplier);
        float damage = Mathf.Max(0f, beat.damage * multiplier);
        BallisticProjectile projectile = null;
        TankProjectileLauncher launcher = GetProjectileLauncher(beat.actor);

        if (_useTankProjectileLauncherForScriptedShots && launcher != null)
        {
            launcher.enabled = false;
            projectile = launcher.LaunchScripted(
                direction,
                range,
                Mathf.Max(5f, beat.launchAngle),
                _projectileGravity,
                isGravity ? _gravityProjectilePrefab : _chargedProjectilePrefab,
                _explosionPrefab,
                damage,
                splashRadius,
                _groundLayer,
                _damageMask,
                actor,
                useKillCam,
                target);
        }
        else
        {
            projectile = CreateProjectile(isGravity);
            if (projectile != null)
            {
                projectile.ConfigureImpact(_explosionPrefab, 6f, damage, splashRadius, _damageMask, actor);
                projectile.LaunchAtTarget(origin, target, Mathf.Max(5f, beat.launchAngle), _projectileGravity, _groundLayer);

                if (useKillCam && _killCamController != null)
                {
                    _killCamController.BeginKillCam(projectile, _groundLayer);
                }
            }
        }

        if (projectile == null)
        {
            yield break;
        }

        projectile.SetHitRadius(_scriptedProjectileHitRadius);
        if (ShouldForceExactImpact(beat))
        {
            projectile.ForceImpactAtTarget(target, _scriptedProjectileHitRadius * 2f);
        }

        projectile.transform.localScale *= Mathf.Max(0.1f, _projectileVisualScale);
        RegisterProjectileImpact(beat, projectile, isGravity, splashRadius);

        bool hasImpactPoint = false;
        Vector3 impactPoint = target;
        System.Action<BallisticProjectile, Vector3> captureImpact = delegate(BallisticProjectile p, Vector3 point)
        {
            hasImpactPoint = true;
            impactPoint = point;
        };

        projectile.Impacted += captureImpact;

        float elapsed = 0f;
        while (projectile != null && projectile.HasImpacted == false && elapsed < _projectileImpactWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (projectile != null)
        {
            projectile.Impacted -= captureImpact;
        }

        if (hasImpactPoint == false && projectile != null)
        {
            impactPoint = projectile.transform.position;
        }

        float impactPause = useKillCam && _killCamController != null
            ? Mathf.Max(_postImpactPause, _killCamPostImpactPause)
            : _postImpactPause;

        if (impactPause > 0f)
        {
            yield return HoldCameraOnImpactPoint(impactPoint, direction, impactPause);
        }
    }

    IEnumerator HoldCameraOnImpactPoint(Vector3 impactPoint, Vector3 shotDirection, float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        bool previousActionPointOverride = _cameraActionPointOverride;
        Vector3 previousActionPointForward = _cameraActionPointForward;
        Transform previousCameraOverride = _cameraFollowActorOverride;

        shotDirection.y = 0f;
        if (shotDirection.sqrMagnitude <= 0.0001f)
        {
            shotDirection = Vector3.forward;
        }

        _cameraActionPointOverride = true;
        _cameraActionPointForward = shotDirection.normalized;
        _cameraFollowActorOverride = null;
        _hasCameraTargetPosition = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            UpdateCameraTarget(impactPoint);
            yield return null;
        }

        _cameraActionPointOverride = previousActionPointOverride;
        _cameraActionPointForward = previousActionPointForward;
        _cameraFollowActorOverride = previousCameraOverride;
    }

    void RegisterProjectileImpact(StoryBeat beat, BallisticProjectile projectile, bool isGravity, float splashRadius)
    {
        if (projectile == null)
        {
            return;
        }

        bool targetsDestructible = beat.targetOverride == StoryboardTarget.GreenSideDestructible ||
            beat.targetOverride == StoryboardTarget.YellowSideDestructible ||
            beat.targetOverride == StoryboardTarget.UtilityWall;
        bool shouldDestroyProps = isGravity ||
            beat.destroyDestructiblesOnImpact ||
            beat.usePrimaryDestructibleAsTarget ||
            targetsDestructible ||
            beat.swapGreenTankWithDamageOnImpact;

        if (shouldDestroyProps == false)
        {
            return;
        }

        projectile.Impacted += delegate(BallisticProjectile p, Vector3 point)
        {
            if (beat.targetOverride == StoryboardTarget.UtilityWall)
            {
                ReplaceUtilityWallWithDamaged();
                return;
            }

            float radius = beat.destroyDestructiblesOnImpact
                ? Mathf.Max(0.1f, beat.destructibleImpactRadius)
                : splashRadius;

            CollapseDestructibles(point, radius);

            if (beat.usePrimaryDestructibleAsTarget)
            {
                CollapseDestructible(_primaryDestructibleProp);
            }

            if (beat.targetOverride == StoryboardTarget.GreenSideDestructible)
            {
                CollapseDestructible(_primaryDestructibleProp);
            }
            else if (beat.targetOverride == StoryboardTarget.YellowSideDestructible)
            {
                CollapseDestructible(_yellowSideDestructibleProp);
            }

            if (beat.swapGreenTankWithDamageOnImpact)
            {
                ReplaceGreenTankWithDamaged();
            }
        };
    }

    BallisticProjectile CreateProjectile(bool isGravity)
    {
        GameObject prefab = isGravity ? _gravityProjectilePrefab : _chargedProjectilePrefab;
        GameObject projectileObject = prefab != null ? Instantiate(prefab) : CreateFallbackProjectile(isGravity);

        if (projectileObject == null)
        {
            return null;
        }

        BallisticProjectile projectile = projectileObject.GetComponent<BallisticProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<BallisticProjectile>();
        }

        return projectile;
    }

    GameObject CreateFallbackProjectile(bool isGravity)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        projectile.name = isGravity ? "Storyboard Gravity Projectile" : "Storyboard Charged Projectile";
        projectile.transform.localScale = Vector3.one * 0.2f;

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(isGravity ? _fallbackGravityColor : _fallbackChargedColor);
        }

        return projectile;
    }

    IEnumerator AimActor(
        Transform actor,
        Vector3 direction,
        float duration,
        float aimDistance,
        bool keepPreviewVisible = false,
        bool hasAimTarget = false,
        Vector3 aimTarget = default)
    {
        float elapsed = 0f;
        TankAimController aimController = GetAimController(actor);
        Transform turret = FindChild(actor, "Turret");
        Transform rotatingPart = turret != null ? turret : actor;
        float previewDistance = Mathf.Max(1f, aimDistance);
        Transform previousCameraOverride = _cameraFollowActorOverride;
        _cameraFollowActorOverride = actor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 targetPoint = rotatingPart.position + direction;
            if (_useTankAimControllerForScriptedAim && aimController != null)
            {
                if (hasAimTarget)
                {
                    aimController.AimAt(aimTarget, true);
                }
                else
                {
                    aimController.SetScriptedAim(direction, previewDistance, true);
                }
            }
            else
            {
                LookAt(rotatingPart, targetPoint, Time.deltaTime * _tankTurnSpeed);
            }

            UpdateCameraTarget(actor.position + direction * 2f);
            yield return null;
        }

        if (keepPreviewVisible == false && _useTankAimControllerForScriptedAim && aimController != null)
        {
            aimController.ClearScriptedAim();
        }

        _cameraFollowActorOverride = previousCameraOverride;
    }

    void SpawnObstacle(StoryBeat beat)
    {
        Vector3 position = ResolveObstaclePosition(beat);
        GameObject obstacle = _obstacleWallPrefab != null
            ? Instantiate(_obstacleWallPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);
        Quaternion baseRotation = obstacle.transform.rotation;

        obstacle.name = "Scripted Obstacle Wall";
        obstacle.transform.position = position;
        obstacle.transform.SetParent(GetRuntimeObjectRoot(), true);
        obstacle.transform.localScale *= Mathf.Max(0.1f, _obstacleWallScale);
        RotateObstacleWallTowardTank(obstacle.transform, beat, baseRotation);

        if (_obstacleWallPrefab == null)
        {
            obstacle.transform.localScale = new Vector3(3.8f, 1.4f, 0.75f);
            Renderer renderer = obstacle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateRuntimeMaterial(new Color(0.12f, 0.24f, 0.95f, 1f));
            }
        }

        EnsureBoxCollider(obstacle, new Vector3(3.8f, 1.6f, 0.9f));
        AlignBottomToGround(obstacle, _tankGroundY + 0.03f);

        if (obstacle.GetComponent<StoryboardDestructible>() == null)
        {
            obstacle.AddComponent<StoryboardDestructible>();
        }

        _lastObstacleWall = obstacle.transform;
        _lastObstacleWallAimPoint = GetUtilityWallAimPoint(_lastObstacleWall, GetOtherActor(beat.actor));
        _hasLastObstacleWallAimPoint = true;
    }

    IEnumerator SpawnObstacleRoutine(StoryBeat beat)
    {
        Transform actor = GetActor(beat.actor);
        Transform previousCameraOverride = _cameraFollowActorOverride;
        _cameraFollowActorOverride = actor;

        Vector3 focusPosition = ResolveObstaclePosition(beat);
        UpdateCameraTarget(focusPosition);
        SpawnObstacle(beat);

        float duration = Mathf.Max(0.1f, beat.duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 target = _hasLastObstacleWallAimPoint ? _lastObstacleWallAimPoint : focusPosition;
            UpdateCameraTarget(target);
            yield return null;
        }

        _cameraFollowActorOverride = previousCameraOverride;
    }

    void ReplaceUtilityWallWithDamaged()
    {
        Transform wall = _lastObstacleWall;
        if (wall == null)
        {
            return;
        }

        Vector3 position = wall.position;
        Quaternion rotation = wall.rotation;
        Vector3 scale = wall.localScale;
        Transform parent = wall.parent != null ? wall.parent : GetRuntimeObjectRoot();

        wall.gameObject.SetActive(false);

        GameObject damagedPrefab = ResolveDamageWallPrefab();
        if (damagedPrefab == null)
        {
            _lastObstacleWall = null;
            return;
        }

        GameObject damagedWall = Instantiate(
            damagedPrefab,
            position,
            rotation * Quaternion.Euler(_damagedWallLayDownEuler),
            parent);

        damagedWall.name = "Scripted Obstacle Wall Damaged";
        damagedWall.transform.localScale = scale;
        AlignBottomToGround(damagedWall, _tankGroundY + _damagedWallGroundOffset);
        DisableColliders(damagedWall);
        _lastObstacleWall = null;
        _hasLastObstacleWallAimPoint = false;
    }

    void ReplaceGreenTankWithDamaged()
    {
        if (_greenTank == null)
        {
            return;
        }

        Transform originalTank = _greenTank;
        GameObject damagePrefab = ResolveDamageTankPrefab();

        if (damagePrefab == null)
        {
            originalTank.gameObject.SetActive(false);
            return;
        }

        Transform parent = originalTank.parent != null ? originalTank.parent : transform;
        GameObject damagedTank = Instantiate(damagePrefab, originalTank.position, originalTank.rotation, parent);
        damagedTank.name = "Tank_Green_Damage";
        damagedTank.transform.localScale = originalTank.localScale;

        originalTank.gameObject.SetActive(false);
        _greenTank = damagedTank.transform;
        _greenMoveController = null;
        _greenAimController = null;
        _greenProjectileLauncher = null;
        UpdateCameraTarget(_greenTank.position);
    }

    Vector3 ResolveObstaclePosition(StoryBeat beat)
    {
        if (_placeObstacleInFrontOfActor == false && beat.obstaclePosition.sqrMagnitude > 0.0001f)
        {
            return Grounded(beat.obstaclePosition);
        }

        Transform actor = GetActor(beat.actor);
        if (actor == null)
        {
            return Grounded(beat.obstaclePosition);
        }

        Vector3 guardDirection = GetObstacleGuardDirection(beat);
        if (guardDirection.sqrMagnitude <= 0.0001f)
        {
            return Grounded(beat.obstaclePosition);
        }

        return Grounded(actor.position + guardDirection.normalized * Mathf.Max(0.1f, _obstacleWallDistanceFromTank));
    }

    void RotateObstacleWallTowardTank(Transform obstacle, StoryBeat beat, Quaternion baseRotation)
    {
        if (obstacle == null)
        {
            return;
        }

        Transform actor = GetActor(beat.actor);
        Vector3 tankPosition = actor != null ? actor.position : GetActorPosition(beat.actor);
        Vector3 directionToTank = tankPosition - obstacle.position;
        directionToTank.y = 0f;

        if (directionToTank.sqrMagnitude <= 0.0001f)
        {
            directionToTank = GetOtherActorPosition(beat.actor) - obstacle.position;
            directionToTank.y = 0f;
        }

        if (directionToTank.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float yaw = Mathf.Atan2(directionToTank.x, directionToTank.z) * Mathf.Rad2Deg;
        obstacle.rotation = Quaternion.Euler(0f, yaw + _obstacleWallYawOffset, 0f) * baseRotation;
    }

    Vector3 GetObstacleGuardDirection(StoryBeat beat)
    {
        Transform actor = GetActor(beat.actor);
        if (actor == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = GetOtherActorPosition(beat.actor) - actor.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        direction = actor.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    void SpawnReloadPulse(Transform actor)
    {
        if (actor == null)
        {
            return;
        }

        GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pulse.name = "Reload Pulse";
        pulse.transform.position = actor.position + Vector3.up * 1.3f;
        pulse.transform.localScale = Vector3.one * 0.7f;

        Collider collider = pulse.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = pulse.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(new Color(0.15f, 1f, 0.35f, 0.7f));
        }

        EditorSelectionClearOnDestroy.Attach(pulse);
        Destroy(pulse, 1.1f);
    }

    void CollapseDestructibles(Vector3 center, float radius)
    {
        StoryboardDestructible[] destructibles = FindObjectsByType<StoryboardDestructible>(FindObjectsSortMode.None);

        for (int i = 0; i < destructibles.Length; i++)
        {
            StoryboardDestructible destructible = destructibles[i];
            if (destructible == null || destructible.gameObject.activeInHierarchy == false)
            {
                continue;
            }

            float distance = Vector3.Distance(destructible.transform.position, center);
            if (distance <= radius)
            {
                destructible.gameObject.SetActive(false);
            }
        }
    }

    void CollapseDestructible(Transform target)
    {
        if (target != null)
        {
            target.gameObject.SetActive(false);
        }
    }

    void UpdateCameraTarget(Vector3 position)
    {
        CameraAnchor anchor = GetCameraAnchor(position);
        Vector3 targetPosition = anchor.position;

        if (_cameraTarget != null)
        {
            if (_hasCameraTargetPosition == false ||
                Vector3.Distance(_cameraTarget.position, targetPosition) >= _cameraSnapDistance)
            {
                _cameraTarget.position = targetPosition;
                _hasCameraTargetPosition = true;
            }
            else
            {
                _cameraTarget.position = Vector3.Lerp(
                    _cameraTarget.position,
                    targetPosition,
                    Time.deltaTime * _cameraTargetSmooth);
            }

            _cameraTarget.rotation = Quaternion.Slerp(
                _cameraTarget.rotation,
                anchor.rotation,
                Time.deltaTime * Mathf.Max(1f, _cameraTargetSmooth));

            return;
        }

        if (_mainCamera != null)
        {
            _mainCamera.transform.position = targetPosition + _fallbackCameraOffset;
            _mainCamera.transform.LookAt(targetPosition);
        }
    }

    CameraAnchor GetCameraAnchor(Vector3 actionPosition)
    {
        if (_cameraActionPointOverride)
        {
            Vector3 forward = _cameraActionPointForward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 positions = Grounded(actionPosition);
            positions.y = _tankGroundY + _cameraTargetHeight;
            return new CameraAnchor(positions, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        if (_lockCameraToArenaCenter)
        {
            return new CameraAnchor(Grounded(_arenaCenter), Quaternion.identity);
        }

        Transform followTank = _cameraFollowActorOverride != null
            ? _cameraFollowActorOverride
            : _yellowTank;

        if (_cameraFollowsYellowTank == false && _cameraFollowActorOverride == null)
        {
            followTank = null;
        }

        if (followTank == null)
        {
            return new CameraAnchor(Grounded(actionPosition), Quaternion.identity);
        }

        Transform otherTank = followTank == _greenTank ? _yellowTank : _greenTank;
        Vector3 followPosition = followTank.position;
        Vector3 lookAheadPosition = actionPosition;

        if ((lookAheadPosition - followPosition).sqrMagnitude < 0.25f && otherTank != null)
        {
            lookAheadPosition = otherTank.position;
        }

        Vector3 direction = lookAheadPosition - followPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f && otherTank != null)
        {
            direction = otherTank.position - followPosition;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Vector3 position = Vector3.Lerp(followPosition, lookAheadPosition, _cameraLookAheadWeight);
        position = new Vector3(position.x, _tankGroundY + _cameraTargetHeight, position.z);
        return new CameraAnchor(position, rotation);
    }

    void UpdateSandStorm(StoryBeat beat)
    {
        if (_sandStorm == null || beat == null || beat.windDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _sandStorm.SetWind(beat.windDirection, Mathf.Max(0f, beat.windStrength));
    }

    Vector3 GetFocusPosition(StoryBeat beat)
    {
        switch (beat.action)
        {
            case StoryboardAction.FireCharged:
            case StoryboardAction.FireGravity:
                return Vector3.Lerp(GetActorPosition(beat.actor), GetTargetPosition(beat), 0.5f);
            case StoryboardAction.MoveThenFireCharged:
            case StoryboardAction.MoveThenFireGravity:
                return Vector3.Lerp(GetMovePosition(beat), GetTargetPosition(beat), 0.5f);
            case StoryboardAction.SpawnObstacle:
                return ResolveObstaclePosition(beat);
            case StoryboardAction.Move:
                return Vector3.Lerp(GetActorPosition(beat.actor), GetMovePosition(beat), 0.5f);
            default:
                return Vector3.Lerp(beat.yellowPosition, beat.greenPosition, 0.5f);
        }
    }

    Vector3 GetMovePosition(StoryBeat beat)
    {
        if (beat.movePosition.sqrMagnitude > 0.0001f)
        {
            return beat.movePosition;
        }

        return beat.targetPosition;
    }

    Vector3 GetTargetPosition(StoryBeat beat)
    {
        if (beat.targetOverride == StoryboardTarget.GreenSideDestructible && _primaryDestructibleProp != null)
        {
            return _primaryDestructibleProp.position;
        }

        if (beat.targetOverride == StoryboardTarget.YellowSideDestructible && _yellowSideDestructibleProp != null)
        {
            return GetYellowSideDestructibleAimTarget().position;
        }

        if (beat.targetOverride == StoryboardTarget.UtilityWall && _hasLastObstacleWallAimPoint)
        {
            return _lastObstacleWallAimPoint;
        }

        if (beat.targetOverride == StoryboardTarget.UtilityWall && _lastObstacleWall != null)
        {
            return GetUtilityWallAimPoint(_lastObstacleWall, GetActor(beat.actor));
        }

        if (beat.targetOverride == StoryboardTarget.YellowTank && _yellowTank != null)
        {
            return _yellowTank.position;
        }

        if (beat.targetOverride == StoryboardTarget.GreenTank && _greenTank != null)
        {
            return _greenTank.position;
        }

        if (beat.usePrimaryDestructibleAsTarget && _primaryDestructibleProp != null)
        {
            return _primaryDestructibleProp.position;
        }

        return beat.targetPosition;
    }

    bool ShouldForceExactImpact(StoryBeat beat)
    {
        return beat.targetOverride != StoryboardTarget.ManualPosition ||
            beat.swapGreenTankWithDamageOnImpact;
    }

    Vector3 GetProjectileTargetPosition(StoryBeat beat, Transform actor)
    {
        if (beat.targetOverride == StoryboardTarget.GreenSideDestructible && _primaryDestructibleProp != null)
        {
            return GetDestructibleAimPoint(_primaryDestructibleProp, actor);
        }

        if (beat.targetOverride == StoryboardTarget.YellowSideDestructible && _yellowSideDestructibleProp != null)
        {
            return GetDestructibleAimPoint(GetYellowSideDestructibleAimTarget(), actor);
        }

        if (beat.targetOverride == StoryboardTarget.UtilityWall && _hasLastObstacleWallAimPoint)
        {
            return _lastObstacleWallAimPoint;
        }

        if (beat.targetOverride == StoryboardTarget.UtilityWall && _lastObstacleWall != null)
        {
            return GetUtilityWallAimPoint(_lastObstacleWall, actor);
        }

        if (beat.targetOverride == StoryboardTarget.YellowTank && _yellowTank != null)
        {
            return GetObjectAimPoint(_yellowTank);
        }

        if (beat.targetOverride == StoryboardTarget.GreenTank && _greenTank != null)
        {
            return GetObjectAimPoint(_greenTank);
        }

        if (beat.usePrimaryDestructibleAsTarget && _primaryDestructibleProp != null)
        {
            return GetDestructibleAimPoint(_primaryDestructibleProp, actor);
        }

        return ProjectileGrounded(beat.targetPosition);
    }

    Transform GetYellowSideDestructibleAimTarget()
    {
        return _yellowSideDestructibleAimTarget != null ? _yellowSideDestructibleAimTarget : _yellowSideDestructibleProp;
    }

    Vector3 ProjectileGrounded(Vector3 position)
    {
        float groundY = Mathf.Abs(_projectileGroundY) > 0.0001f ? _projectileGroundY : _tankGroundY;
        return new Vector3(position.x, groundY, position.z);
    }

    Vector3 GetObjectAimPoint(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        if (TryGetTargetBounds(target, out Bounds bounds) == false)
        {
            return target.position;
        }

        Vector3 point = bounds.center;
        point.y = Mathf.Max(point.y, _tankGroundY + 0.65f);
        return point;
    }

    Vector3 GetDestructibleAimPoint(Transform target, Transform actor)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        if (TryGetTargetBounds(target, out Bounds bounds) == false)
        {
            return target.position;
        }

        Vector3 reference = actor != null ? actor.position : target.position;
        reference.y = Mathf.Clamp(
            reference.y,
            bounds.min.y + bounds.size.y * 0.25f,
            bounds.min.y + bounds.size.y * 0.55f);

        Vector3 point = bounds.ClosestPoint(reference);
        point.y = reference.y;
        return point;
    }

    Vector3 GetUtilityWallAimPoint(Transform wall, Transform actor)
    {
        if (wall == null)
        {
            return Vector3.zero;
        }

        if (TryGetLocalTargetBounds(wall, out Bounds localBounds) == false)
        {
            return GetDestructibleAimPoint(wall, actor);
        }

        int verticalAxis = GetMostVerticalLocalAxis(wall);
        int normalAxis = GetThinnestNonVerticalAxis(localBounds.extents, verticalAxis);
        float normalExtent = GetAxis(localBounds.extents, normalAxis);

        if (normalExtent <= 0.001f)
        {
            return GetDestructibleAimPoint(wall, actor);
        }

        Vector3 actorPosition = actor != null ? actor.position : wall.position - wall.forward;
        Vector3 localActor = wall.InverseTransformPoint(actorPosition);
        Vector3 localPoint = localBounds.center;
        float centerOnAxis = GetAxis(localBounds.center, normalAxis);
        float actorOnAxis = GetAxis(localActor, normalAxis);
        float side = actorOnAxis >= centerOnAxis ? 1f : -1f;

        SetAxis(ref localPoint, normalAxis, centerOnAxis + normalExtent * side);

        Vector3 point = wall.TransformPoint(localPoint);
        if (TryGetTargetBounds(wall, out Bounds worldBounds))
        {
            point.y = Mathf.Clamp(
                point.y,
                worldBounds.min.y + worldBounds.size.y * 0.45f,
                worldBounds.min.y + worldBounds.size.y * 0.62f);
        }

        return point;
    }

    bool TryGetTargetBounds(Transform target, out Bounds bounds)
    {
        bounds = new Bounds(target != null ? target.position : Vector3.zero, Vector3.zero);
        if (target == null)
        {
            return false;
        }

        bool hasBounds = false;
        Collider[] colliders = target.GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.enabled == false)
            {
                continue;
            }

            if (hasBounds)
            {
                bounds.Encapsulate(collider.bounds);
            }
            else
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.enabled == false)
            {
                continue;
            }

            if (hasBounds)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        return hasBounds;
    }

    bool TryGetLocalTargetBounds(Transform target, out Bounds bounds)
    {
        bounds = new Bounds(target != null ? target.position : Vector3.zero, Vector3.zero);
        if (target == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.enabled == false)
            {
                continue;
            }

            EncapsulateWorldBoundsInLocal(target, renderer.bounds, ref bounds, ref hasBounds);
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.enabled == false)
            {
                continue;
            }

            EncapsulateWorldBoundsInLocal(target, collider.bounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    void EncapsulateWorldBoundsInLocal(Transform target, Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(min.x, min.y, min.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(min.x, min.y, max.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(min.x, max.y, min.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(min.x, max.y, max.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(max.x, min.y, min.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(max.x, min.y, max.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(max.x, max.y, min.z)), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(target.InverseTransformPoint(new Vector3(max.x, max.y, max.z)), ref localBounds, ref hasBounds);
    }

    void EncapsulateLocalPoint(Vector3 point, ref Bounds bounds, ref bool hasBounds)
    {
        if (hasBounds)
        {
            bounds.Encapsulate(point);
            return;
        }

        bounds = new Bounds(point, Vector3.zero);
        hasBounds = true;
    }

    int GetMostVerticalLocalAxis(Transform target)
    {
        float x = Mathf.Abs(Vector3.Dot(target.TransformDirection(Vector3.right), Vector3.up));
        float y = Mathf.Abs(Vector3.Dot(target.TransformDirection(Vector3.up), Vector3.up));
        float z = Mathf.Abs(Vector3.Dot(target.TransformDirection(Vector3.forward), Vector3.up));

        if (x >= y && x >= z)
        {
            return 0;
        }

        return y >= z ? 1 : 2;
    }

    int GetThinnestNonVerticalAxis(Vector3 extents, int verticalAxis)
    {
        int bestAxis = verticalAxis == 0 ? 1 : 0;
        float bestExtent = GetAxis(extents, bestAxis);

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == verticalAxis)
            {
                continue;
            }

            float extent = GetAxis(extents, axis);
            if (extent < bestExtent)
            {
                bestAxis = axis;
                bestExtent = extent;
            }
        }

        return bestAxis;
    }

    float GetAxis(Vector3 value, int axis)
    {
        if (axis == 0)
        {
            return value.x;
        }

        return axis == 1 ? value.y : value.z;
    }

    void SetAxis(ref Vector3 value, int axis, float axisValue)
    {
        if (axis == 0)
        {
            value.x = axisValue;
            return;
        }

        if (axis == 1)
        {
            value.y = axisValue;
            return;
        }

        value.z = axisValue;
    }

    Vector3 GetFireOrigin(Transform actor, Vector3 direction)
    {
        Transform muzzle = FindChild(actor, "Muzzle");
        if (muzzle == null)
        {
            muzzle = FindChild(actor, "gun");
        }

        if (muzzle != null)
        {
            return muzzle.position;
        }

        TankAimController aimController = GetAimController(actor);
        if (aimController != null && aimController.AimOrigin != actor.position)
        {
            return aimController.AimOrigin;
        }

        return actor.position + Vector3.up * 1.05f + direction * 0.8f;
    }

    bool ShouldUseProjectileKillCam(StoryboardActor actor)
    {
        if (_useProjectileKillCam)
        {
            return true;
        }

        return actor == StoryboardActor.Yellow
            ? _useProjectileKillCamForPlayerShots
            : _useProjectileKillCamForEnemyShots;
    }

    Transform GetActor(StoryboardActor actor)
    {
        return actor == StoryboardActor.Yellow ? _yellowTank : _greenTank;
    }

    MoveController GetMoveController(StoryboardActor actor)
    {
        return actor == StoryboardActor.Yellow ? _yellowMoveController : _greenMoveController;
    }

    TankProjectileLauncher GetProjectileLauncher(StoryboardActor actor)
    {
        return actor == StoryboardActor.Yellow ? _yellowProjectileLauncher : _greenProjectileLauncher;
    }

    TankAimController GetAimController(StoryboardActor actor)
    {
        return actor == StoryboardActor.Yellow ? _yellowAimController : _greenAimController;
    }

    TankAimController GetAimController(Transform actor)
    {
        if (actor == _yellowTank)
        {
            return _yellowAimController;
        }

        if (actor == _greenTank)
        {
            return _greenAimController;
        }

        return actor != null ? actor.GetComponent<TankAimController>() : null;
    }

    void ClearAimPreview(Transform actor)
    {
        if (_useTankAimControllerForScriptedAim == false)
        {
            return;
        }

        TankAimController aimController = GetAimController(actor);
        if (aimController != null)
        {
            aimController.ClearScriptedAim();
        }
    }

    Transform GetOtherActor(StoryboardActor actor)
    {
        return actor == StoryboardActor.Yellow ? _greenTank : _yellowTank;
    }

    Vector3 GetActorPosition(StoryboardActor actor)
    {
        Transform tank = GetActor(actor);
        return tank != null ? tank.position : Vector3.zero;
    }

    Vector3 GetOtherActorPosition(StoryboardActor actor)
    {
        Transform tank = GetOtherActor(actor);
        return tank != null ? tank.position : Vector3.zero;
    }

    Transform GetActiveMapRoot()
    {
        if (_mapSets == null || _activeMapSet < 0 || _activeMapSet >= _mapSets.Length)
        {
            return transform;
        }

        return _mapSets[_activeMapSet] != null && _mapSets[_activeMapSet].root != null
            ? _mapSets[_activeMapSet].root.transform
            : transform;
    }

    Transform GetRuntimeObjectRoot()
    {
        if (_runtimeObjectRoot != null)
        {
            return _runtimeObjectRoot;
        }

        GameObject root = new GameObject("Runtime Storyboard Objects");
        root.transform.SetParent(transform, false);
        _runtimeObjectRoot = root.transform;
        return _runtimeObjectRoot;
    }

    void ClearRuntimeObjects()
    {
        _lastObstacleWall = null;
        _hasLastObstacleWallAimPoint = false;

        if (_runtimeObjectRoot == null)
        {
            return;
        }

        for (int i = _runtimeObjectRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _runtimeObjectRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    Vector3 Grounded(Vector3 position)
    {
        return new Vector3(position.x, _tankGroundY, position.z);
    }

    void LookAt(Transform target, Vector3 worldPoint, float lerp)
    {
        Vector3 direction = worldPoint - target.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion targetRotation = target == _yellowTank || target == _greenTank
            ? lookRotation * Quaternion.Euler(_tankModelRotationOffset)
            : lookRotation;

        target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Mathf.Clamp01(lerp));
    }

    Transform FindChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.ToLowerInvariant().Contains(childName.ToLowerInvariant()))
            {
                return child;
            }

            Transform match = FindChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    void EnsureBoxCollider(GameObject target, Vector3 size)
    {
        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = target.AddComponent<BoxCollider>();
        }

        box.size = size;
    }

    void DisableColliders(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    void AlignBottomToGround(GameObject target, float groundY)
    {
        if (TryGetRendererBounds(target, out Bounds bounds) == false)
        {
            return;
        }

        float lift = groundY - bounds.min.y;
        target.transform.position += Vector3.up * lift;
    }

    bool TryGetRendererBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (hasBounds == false)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    GameObject ResolveDamageTankPrefab()
    {
        return _damageTankPrefab != null
            ? _damageTankPrefab
            : LoadEditorPrefab(DamageTankPrefabEditorPath);
    }

    GameObject ResolveDamageWallPrefab()
    {
        return _obstacleWallDamagePrefab != null
            ? _obstacleWallDamagePrefab
            : LoadEditorPrefab(DamageWallPrefabEditorPath);
    }

    GameObject LoadEditorPrefab(string assetPath)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
        return null;
#endif
    }

    float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    struct CameraAnchor
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        public CameraAnchor(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }

    void OnGUI()
    {
        if (_showCaptions == false || string.IsNullOrEmpty(_currentBeatName))
        {
            return;
        }

        EnsureGuiStyles();

        float width = Mathf.Min(Screen.width - 32f, 680f);
        Rect backgroundRect = new Rect(16f, 16f, width, 92f);
        GUI.DrawTexture(backgroundRect, _captionBackground);
        GUI.Label(new Rect(32f, 26f, width - 32f, 28f), _currentBeatName, _captionTitleStyle);
        GUI.Label(new Rect(32f, 54f, width - 32f, 48f), _currentCaption, _captionBodyStyle);
    }

    void EnsureGuiStyles()
    {
        if (_captionTitleStyle != null)
        {
            return;
        }

        _captionBackground = new Texture2D(1, 1);
        _captionBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.68f));
        _captionBackground.Apply();

        _captionTitleStyle = new GUIStyle(GUI.skin.label);
        _captionTitleStyle.fontSize = 20;
        _captionTitleStyle.fontStyle = FontStyle.Bold;
        _captionTitleStyle.normal.textColor = Color.white;

        _captionBodyStyle = new GUIStyle(GUI.skin.label);
        _captionBodyStyle.fontSize = 15;
        _captionBodyStyle.wordWrap = true;
        _captionBodyStyle.normal.textColor = new Color(0.88f, 0.94f, 1f, 1f);
    }
}
