using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class StoryboardSceneBootstrapper : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] bool _buildOnAwake = true;
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _useCustomStoryBeats;
    [SerializeField] StoryboardDirector.StoryBeat[] _customStoryBeats = new StoryboardDirector.StoryBeat[0];

    [Header("Existing VZ Scene")]
    [SerializeField] bool _useExistingSceneLayout = true;
    [SerializeField] string _existingMapName = "map1";
    [SerializeField] string _yellowTankName = "Tank_Yellow_Prefab";
    [SerializeField] string _greenTankName = "Tank_Green_Prefab";
    [SerializeField] bool _detachExistingProps = true;
    [SerializeField] bool _markScenePropsDestructible = true;

    [Header("Map Prefabs")]
    [SerializeField] GameObject[] _mapPrefabs = new GameObject[0];

    [Header("Tank Prefabs")]
    [SerializeField] GameObject _yellowTankPrefab;
    [SerializeField] GameObject _greenTankPrefab;
    [SerializeField] GameObject _damageTankPrefab;

    [Header("Prop Prefabs")]
    [SerializeField] GameObject[] _rockPrefabs = new GameObject[0];
    [SerializeField] GameObject[] _propPrefabs = new GameObject[0];

    [Header("Combat Prefabs")]
    [SerializeField] GameObject _chargedProjectilePrefab;
    [SerializeField] GameObject _gravityProjectilePrefab;
    [SerializeField] GameObject _obstacleWallPrefab;
    [SerializeField] GameObject _obstacleWallDamagePrefab;
    [SerializeField] GameObject _explosionPrefab;
    [SerializeField] GameObject _dustStormPrefab;

    [Header("Camera")]
    [SerializeField] Vector3 _mainCameraOffset = new Vector3(7.2f, 4.2f, 0f);
    [SerializeField] Vector3 _mainCameraRotation = new Vector3(20f, -90f, 0f);
    [SerializeField] Rect _sectionCameraRect = new Rect(0.64f, 0.02f, 0.34f, 0.3f);

    const float TankY = 1.45f;
    const float GroundPlaneY = 1.28f;
    const float MarkerY = 1.58f;
    const float WallY = 2.05f;
    const string YellowFrontRockName = "Rock1 Variant";
    const string YellowFrontRockAimTargetName = "rock_01_low";
    const string DustStormLayerName = "DustStorm";

    static readonly int[] StoryboardMapOrder = { 0, 1, 2, 2, 3, 4, 5, 6 };

    static readonly Vector3 YellowStart = new Vector3(18.1f, TankY, 0f);
    static readonly Vector3 GreenStart = new Vector3(-17.7f, TankY, 2f);
    static readonly Vector3 LowerGravityTarget = new Vector3(-8.5f, TankY, 1.2f);
    static readonly Vector3 GreenFrontGroundImpactTarget = new Vector3(-14.2f, TankY, 1.8f);
    static readonly Vector3 WallPosition = new Vector3(11.8f, TankY, 1.2f);
    static readonly Vector3 GreenSideLane = new Vector3(-8.5f, TankY, -7.3f);
    static readonly Vector3 YellowAdvance = new Vector3(8.4f, TankY, 0.8f);
    static readonly Vector3 UpperGravityTarget = new Vector3(10.2f, TankY, 2.6f);
    static readonly Vector3 GreenFinishTarget = new Vector3(-10.8f, TankY, -6.4f);

    bool _built;

    void Awake()
    {
        if (_buildOnAwake)
        {
            Build();
        }
    }

    [ContextMenu("Build Storyboard Scene")]
    public void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        RemoveLegacyImpactMarkers();

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            groundLayer = 0;
        }

        int dustStormLayer = LayerMask.NameToLayer(DustStormLayerName);
        if (dustStormLayer < 0)
        {
            dustStormLayer = LayerMask.NameToLayer("TransparentFX");
        }

        Transform sceneRoot = new GameObject("PDF Storyboard Runtime Scene").transform;
        EditorSelectionClearOnDestroy.Attach(sceneRoot.gameObject);

        Transform mapRoot = CreateChild(sceneRoot, "Storyboard Map Sets");
        Transform propRoot = CreateChild(sceneRoot, "VZScene Reused Props");

        GameObject existingMap = _useExistingSceneLayout ? GameObject.Find(_existingMapName) : null;
        List<Transform> sceneProps = new List<Transform>();

        if (existingMap != null && _detachExistingProps)
        {
            DetachSceneProps(existingMap.transform, propRoot, sceneProps);
        }

        StoryboardDirector.MapSet[] mapSets = CreateMapSets(mapRoot, existingMap, groundLayer);

        if (sceneProps.Count == 0)
        {
            CreateFallbackProps(propRoot);
        }

        PrepareSceneProps(propRoot);

        Camera mainCamera;
        QuarterViewCameraController quarterCamera;
        ProjectileKillCamController killCam;
        Transform cameraTarget = CreateChild(sceneRoot, "Storyboard Camera Target");
        cameraTarget.position = YellowStart;

        CreateCamera(sceneRoot, cameraTarget, out mainCamera, out quarterCamera, out killCam);
        IncludeLayerInCamera(mainCamera, dustStormLayer);
        CreateSectionCamera(sceneRoot, dustStormLayer);
        EnsureLights(sceneRoot);

        GameObject yellowTank = FindOrCreateTank(_yellowTankName, _yellowTankPrefab, YellowStart, sceneRoot);
        GameObject greenTank = FindOrCreateTank(_greenTankName, _greenTankPrefab, GreenStart, sceneRoot);
        ConfigureTank(yellowTank, mainCamera, killCam, groundLayer);
        ConfigureTank(greenTank, mainCamera, killCam, groundLayer);

        SandStormController sandStorm = CreateDustStorm(sceneRoot, dustStormLayer);
        Transform primaryDestructible = FindPrimaryDestructible(propRoot, greenTank.transform, yellowTank.transform);
        Transform yellowSideDestructible = FindNamedRock(propRoot, YellowFrontRockName);
        if (yellowSideDestructible == null)
        {
            yellowSideDestructible = FindYellowSideDestructible(propRoot, yellowTank.transform, greenTank.transform, primaryDestructible);
        }

        EnsureStoryboardPropActive(yellowSideDestructible);
        Transform yellowSideAimTarget = FindDescendantByName(yellowSideDestructible, YellowFrontRockAimTargetName);
        EnsureStoryboardPropActive(yellowSideAimTarget);

        StoryboardDirector director = new GameObject("PDF Storyboard Director").AddComponent<StoryboardDirector>();
        director.transform.SetParent(sceneRoot);

        LayerMask groundMask = 1 << groundLayer;
        director.Configure(
            mapSets,
            GetStoryBeats(),
            yellowTank.transform,
            greenTank.transform,
            cameraTarget,
            mainCamera,
            killCam,
            _chargedProjectilePrefab,
            _gravityProjectilePrefab,
            _obstacleWallPrefab,
            _obstacleWallDamagePrefab,
            _damageTankPrefab,
            _explosionPrefab,
            sandStorm,
            groundMask,
            ~0);

        director.SetPrimaryDestructible(primaryDestructible);
        director.SetYellowSideDestructible(yellowSideDestructible, yellowSideAimTarget);
        SetDirectorDefaults(director);
    }

    StoryboardDirector.StoryBeat[] GetStoryBeats()
    {
        if (_useCustomStoryBeats && _customStoryBeats != null && _customStoryBeats.Length > 0)
        {
            return _customStoryBeats;
        }

        return CreateStoryBeats();
    }

    StoryboardDirector.MapSet[] CreateMapSets(Transform parent, GameObject existingMap, int groundLayer)
    {
        string[] names =
        {
            "01 Initial",
            "02 Ground Break Map",
            "03 Reserved",
            "04 Reserved",
            "05 Reserved",
            "06 Reserved",
            "07 Reserved",
            "08 Reserved"
        };

        StoryboardDirector.MapSet[] mapSets = new StoryboardDirector.MapSet[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            GameObject setRoot = new GameObject("MapSet_" + names[i]);
            setRoot.transform.SetParent(parent);
            setRoot.SetActive(i == 0);

            GameObject map = null;
            if (i == 0 && existingMap != null)
            {
                map = existingMap;
                map.transform.SetParent(setRoot.transform, true);
            }
            else
            {
                map = InstantiatePrefab(GetMapPrefabForBeat(i), setRoot.transform, "Map " + names[i]);
                map.transform.localPosition = Vector3.zero;
                map.transform.localRotation = Quaternion.identity;
                map.transform.localScale = Vector3.one;
            }

            SetLayerRecursively(map, groundLayer);
            AddGroundCollider(setRoot, groundLayer);

            mapSets[i] = new StoryboardDirector.MapSet
            {
                name = names[i],
                root = setRoot
            };
        }

        return mapSets;
    }

    GameObject GetMapPrefabForBeat(int beatIndex)
    {
        if (_mapPrefabs == null || _mapPrefabs.Length == 0)
        {
            return null;
        }

        int orderedIndex = StoryboardMapOrder[Mathf.Clamp(beatIndex, 0, StoryboardMapOrder.Length - 1)];
        return _mapPrefabs[Mathf.Clamp(orderedIndex, 0, _mapPrefabs.Length - 1)];
    }

    void DetachSceneProps(Transform map, Transform propRoot, List<Transform> sceneProps)
    {
        List<Transform> props = new List<Transform>();
        for (int i = 0; i < map.childCount; i++)
        {
            Transform child = map.GetChild(i);
            if (IsReusableProp(child.name))
            {
                props.Add(child);
            }
        }

        for (int i = 0; i < props.Count; i++)
        {
            props[i].SetParent(propRoot, true);
            sceneProps.Add(props[i]);
        }
    }

    bool IsReusableProp(string objectName)
    {
        return objectName.Contains("Cactus") ||
            objectName.Contains("Rock") ||
            objectName.Contains("Tree") ||
            objectName.Contains("DeadTree") ||
            objectName.Contains("DryTree");
    }

    void PrepareSceneProps(Transform propRoot)
    {
        for (int i = 0; i < propRoot.childCount; i++)
        {
            GameObject prop = propRoot.GetChild(i).gameObject;
            EnsureCollider(prop, Vector3.one * 1.5f);

            if (_markScenePropsDestructible && prop.GetComponent<StoryboardDestructible>() == null)
            {
                prop.AddComponent<StoryboardDestructible>();
            }
        }
    }

    void CreateFallbackProps(Transform propRoot)
    {
        Vector3[] rockPositions =
        {
            new Vector3(-16.15f, TankY, 2.16f),
            new Vector3(7.94f, TankY, -10.44f),
            new Vector3(8.22f, TankY, 10.06f)
        };

        for (int i = 0; i < rockPositions.Length; i++)
        {
            GameObject rock = InstantiatePrefab(GetArrayPrefab(_rockPrefabs, i), propRoot, "Storyboard Rock " + (i + 1));
            rock.transform.position = rockPositions[i];
            rock.transform.localScale *= i == 0 ? 1.2f : 1f;
            AlignBottomToGround(rock, GroundPlaneY);
        }

        PlaceFallbackProp(propRoot, 0, new Vector3(-12.27f, TankY, 8.64f), 0f);
        PlaceFallbackProp(propRoot, 1, new Vector3(26.21f, TankY, 8.48f), 0f);
        PlaceFallbackProp(propRoot, 2, new Vector3(-26.77f, TankY, 8.09f), 0f);
        PlaceFallbackProp(propRoot, 3, new Vector3(-8.68f, TankY, -7.7f), 0f);
    }

    void PlaceFallbackProp(Transform parent, int index, Vector3 position, float yaw)
    {
        GameObject prop = InstantiatePrefab(GetArrayPrefab(_propPrefabs, index), parent, "Storyboard Prop");
        prop.transform.position = position;
        prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        AlignBottomToGround(prop, GroundPlaneY);
    }

    StoryboardDirector.StoryBeat[] CreateStoryBeats()
    {
        StoryboardDirector.StoryBeat[] beats =
        {
            Beat("01. Battle Start", "VZScene props stay in place while the first map is active.", 0, StoryboardDirector.StoryboardActor.Yellow, StoryboardDirector.StoryboardAction.Hold, YellowStart, GreenStart, Vector3.zero, Vector3.zero, Vector3.zero, StoryboardDirector.StoryboardTarget.ManualPosition, 1.1f, 0.65f, 30f, 2.5f, 1f, false, false),
            Beat("02. Yellow Breaks Green Rock", "Yellow fires first and breaks the rock in front of Green on the base map.", 0, StoryboardDirector.StoryboardActor.Yellow, StoryboardDirector.StoryboardAction.FireCharged, YellowStart, GreenStart, Vector3.zero, GreenStart, Vector3.zero, StoryboardDirector.StoryboardTarget.GreenSideDestructible, 1.0f, 0.95f, 28f, 2.0f, 1.2f, false, true),
            Beat("03. Green Counterattacks Yellow", "Green immediately counterattacks Yellow instead of moving.", 0, StoryboardDirector.StoryboardActor.Green, StoryboardDirector.StoryboardAction.FireCharged, YellowStart, GreenStart, Vector3.zero, YellowStart, Vector3.zero, StoryboardDirector.StoryboardTarget.YellowTank, 1.0f, 0.95f, 14f, 1.6f, 1f, false, false),
            Beat("04. Yellow Digs In Front Of Green", "Yellow fires into the ground directly in front of Green so map2 appears after impact.", 1, StoryboardDirector.StoryboardActor.Yellow, StoryboardDirector.StoryboardAction.FireGravity, YellowStart, GreenStart, Vector3.zero, GreenFrontGroundImpactTarget, Vector3.zero, StoryboardDirector.StoryboardTarget.ManualPosition, 1.0f, 0.95f, 18f, 1.4f, 1f, false, false),
            Beat("05. Green Moves To Tree Side", "Green uses one turn to move toward the tree side.", 1, StoryboardDirector.StoryboardActor.Green, StoryboardDirector.StoryboardAction.Move, YellowStart, GreenStart, Vector3.zero, GreenSideLane, Vector3.zero, StoryboardDirector.StoryboardTarget.ManualPosition, 2.1f, 0.95f, 30f, 2.5f, 1f, false, false),
            Beat("06. Green Breaks Yellow Front Rock", "After moving, Green fires and breaks the rock in front of Yellow.", 1, StoryboardDirector.StoryboardActor.Green, StoryboardDirector.StoryboardAction.FireCharged, YellowStart, GreenSideLane, Vector3.zero, YellowStart, Vector3.zero, StoryboardDirector.StoryboardTarget.YellowSideDestructible, 1.0f, 0.95f, 26f, 2.0f, 1.15f, false, true),
            Beat("07. Yellow Utility Wall", "Yellow spends one turn placing the defensive wall after the rock breaks.", 1, StoryboardDirector.StoryboardActor.Yellow, StoryboardDirector.StoryboardAction.SpawnObstacle, YellowStart, GreenSideLane, Vector3.zero, Vector3.zero, WallPosition, StoryboardDirector.StoryboardTarget.ManualPosition, 1.0f, 0.9f, 30f, 2.5f, 1f, false, false),
            Beat("08. Green Breaks Wall", "Green attacks and breaks Yellow's utility wall.", 1, StoryboardDirector.StoryboardActor.Green, StoryboardDirector.StoryboardAction.FireCharged, YellowStart, GreenSideLane, Vector3.zero, UpperGravityTarget, Vector3.zero, StoryboardDirector.StoryboardTarget.UtilityWall, 1.0f, 0.95f, 24f, 2.2f, 1.2f, false, false),
            Beat("09. Yellow Advances And Finishes", "Yellow passes the broken wall, then immediately lands a direct hit on Green.", 1, StoryboardDirector.StoryboardActor.Yellow, StoryboardDirector.StoryboardAction.MoveThenFireCharged, YellowStart, GreenSideLane, YellowAdvance, GreenSideLane, Vector3.zero, StoryboardDirector.StoryboardTarget.GreenTank, 1.7f, 1.15f, 50f, 2.6f, 1.2f, false, true, true)
        };

        ApplyWind(beats, 0, new Vector3(1f, 0f, 0.25f), 4.8f);
        ApplyWind(beats, 1, new Vector3(0.25f, 0f, -1f), 6.4f);
        ApplyWind(beats, 2, new Vector3(-1f, 0f, -0.15f), 7.2f);
        ApplyWind(beats, 3, new Vector3(-0.55f, 0f, 1f), 5.8f);
        ApplyWind(beats, 4, new Vector3(1f, 0f, 0.65f), 6.6f);
        ApplyWind(beats, 5, new Vector3(0.15f, 0f, 1f), 4.6f);
        ApplyWind(beats, 6, new Vector3(-1f, 0f, 0.35f), 7.6f);
        ApplyWind(beats, 7, new Vector3(0.6f, 0f, -1f), 8.2f);
        ApplyWind(beats, 8, new Vector3(-0.35f, 0f, -1f), 8.4f);

        return beats;
    }

    StoryboardDirector.StoryBeat Beat(
        string name,
        string caption,
        int mapSetIndex,
        StoryboardDirector.StoryboardActor actor,
        StoryboardDirector.StoryboardAction action,
        Vector3 yellowPosition,
        Vector3 greenPosition,
        Vector3 movePosition,
        Vector3 targetPosition,
        Vector3 obstaclePosition,
        StoryboardDirector.StoryboardTarget targetOverride,
        float duration,
        float postDelay,
        float damage,
        float splashRadius,
        float powerMultiplier,
        bool usePrimaryDestructibleAsTarget,
        bool destroyDestructiblesOnImpact,
        bool swapGreenTankWithDamageOnImpact = false)
    {
        return new StoryboardDirector.StoryBeat
        {
            name = name,
            caption = caption,
            mapSetIndex = mapSetIndex,
            actor = actor,
            action = action,
            yellowPosition = yellowPosition,
            greenPosition = greenPosition,
            movePosition = movePosition,
            targetPosition = targetPosition,
            obstaclePosition = obstaclePosition,
            targetOverride = targetOverride,
            duration = duration,
            postDelay = postDelay,
            launchAngle = 55f,
            damage = damage,
            splashRadius = splashRadius,
            powerMultiplier = powerMultiplier,
            usePrimaryDestructibleAsTarget = usePrimaryDestructibleAsTarget,
            destroyDestructiblesOnImpact = destroyDestructiblesOnImpact,
            swapGreenTankWithDamageOnImpact = swapGreenTankWithDamageOnImpact,
            destructibleImpactRadius = Mathf.Max(1.2f, splashRadius)
        };
    }

    void ApplyWind(StoryboardDirector.StoryBeat[] beats, int index, Vector3 direction, float strength)
    {
        if (beats == null || index < 0 || index >= beats.Length || beats[index] == null)
        {
            return;
        }

        beats[index].windDirection = direction;
        beats[index].windStrength = strength;
    }

    GameObject FindOrCreateTank(string objectName, GameObject prefab, Vector3 position, Transform sceneRoot)
    {
        GameObject tank = _useExistingSceneLayout ? GameObject.Find(objectName) : null;
        if (tank == null)
        {
            tank = InstantiatePrefab(prefab, sceneRoot, objectName);
            tank.transform.position = position;
            tank.transform.rotation = Quaternion.Euler(-90f, 90f, 0f);
        }
        else
        {
            tank.transform.SetParent(sceneRoot, true);
        }

        return tank;
    }

    void ConfigureTank(GameObject tank, Camera mainCamera, ProjectileKillCamController killCam, int groundLayer)
    {
        CharacterController characterController = tank.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = tank.AddComponent<CharacterController>();
        }

        characterController.center = new Vector3(0f, 0.8f, 0f);
        characterController.height = 1.6f;
        characterController.radius = 0.7f;

        CapsuleCollider hitCollider = tank.GetComponent<CapsuleCollider>();
        if (hitCollider == null)
        {
            hitCollider = tank.AddComponent<CapsuleCollider>();
        }

        hitCollider.center = new Vector3(0f, 0.65f, 0f);
        hitCollider.height = 1.4f;
        hitCollider.radius = 0.72f;

        TankHealth health = tank.GetComponent<TankHealth>();
        if (health == null)
        {
            health = tank.AddComponent<TankHealth>();
        }

        MoveController move = tank.GetComponent<MoveController>();
        if (move == null)
        {
            move = tank.AddComponent<MoveController>();
        }

        TankAimController aim = tank.GetComponent<TankAimController>();
        if (aim == null)
        {
            aim = tank.AddComponent<TankAimController>();
        }

        TankProjectileLauncher launcher = tank.GetComponent<TankProjectileLauncher>();
        if (launcher == null)
        {
            launcher = tank.AddComponent<TankProjectileLauncher>();
        }

        LayerMask groundMask = 1 << groundLayer;
        move.Configure(characterController, mainCamera, groundMask);
        move.SetMouseInputEnabled(false);
        move.enabled = false;

        SetField(aim, "_mainCamera", mainCamera);
        SetField(aim, "_turretName", "Cannon");
        SetField(aim, "_muzzleName", "gun");
        SetField(aim, "_turretRotationSpeed", 30f);
        SetField(aim, "_showAimTrajectory", true);
        SetField(aim, "_trajectoryDotCount", 34);
        SetField(aim, "_trajectoryVisiblePortion", 0.95f);
        SetField(launcher, "_aimController", aim);
        SetField(launcher, "_projectilePrefab", _chargedProjectilePrefab);
        SetField(launcher, "_mainCamera", mainCamera);
        SetField(launcher, "_killCamController", killCam);
        SetField(launcher, "_explosionPrefab", _explosionPrefab);
        SetField(launcher, "_groundLayer", groundMask);
        SetField(launcher, "_damageMask", (LayerMask)(~0));

        aim.enabled = false;
        launcher.enabled = false;
    }

    void CreateCamera(
        Transform parent,
        Transform target,
        out Camera mainCamera,
        out QuarterViewCameraController quarterCamera,
        out ProjectileKillCamController killCam)
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            cameraObject.AddComponent<AudioListener>();
            mainCamera = cameraObject.AddComponent<Camera>();
        }
        else
        {
            mainCamera.transform.SetParent(parent, true);
        }

        mainCamera.fieldOfView = 62f;
        mainCamera.nearClipPlane = 0.1f;
        mainCamera.farClipPlane = 600f;

        quarterCamera = mainCamera.GetComponent<QuarterViewCameraController>();
        if (quarterCamera == null)
        {
            quarterCamera = mainCamera.gameObject.AddComponent<QuarterViewCameraController>();
        }

        SetField(quarterCamera, "_target", target);
        SetField(quarterCamera, "_offset", GetFollowCameraOffset());
        SetField(quarterCamera, "_rotateOffsetWithTargetYaw", true);
        SetField(quarterCamera, "_rotation", _mainCameraRotation);
        SetField(quarterCamera, "_lookAtTarget", true);
        SetField(quarterCamera, "_lookOffset", new Vector3(0f, 1.1f, 6.2f));
        SetField(quarterCamera, "_followSpeed", 12f);
        SetField(quarterCamera, "_rotationSpeed", 16f);
        SetField(quarterCamera, "_snapOnEnable", true);

        killCam = mainCamera.GetComponent<ProjectileKillCamController>();
        if (killCam == null)
        {
            killCam = mainCamera.gameObject.AddComponent<ProjectileKillCamController>();
        }

        SetField(killCam, "_camera", mainCamera);
        SetField(killCam, "_defaultCameraController", quarterCamera);
        SetField(killCam, "_impactHoldTime", 2.75f);
    }

    Vector3 GetFollowCameraOffset()
    {
        if (Mathf.Abs(_mainCameraOffset.z) <= 0.01f && Mathf.Abs(_mainCameraOffset.x) > 0.01f)
        {
            return new Vector3(0f, _mainCameraOffset.y, -Mathf.Abs(_mainCameraOffset.x));
        }

        return _mainCameraOffset;
    }

    void CreateSectionCamera(Transform parent, int hiddenLayer)
    {
        GameObject cameraObject = GameObject.Find("Section View Camera");
        if (cameraObject == null)
        {
            cameraObject = new GameObject("Section View Camera");
            cameraObject.AddComponent<Camera>();
        }

        cameraObject.transform.SetParent(parent, true);
        cameraObject.transform.position = new Vector3(0f, 5f, 30f);
        cameraObject.transform.rotation = Quaternion.Euler(0f, -180f, 0f);

        Camera sectionCamera = cameraObject.GetComponent<Camera>();
        sectionCamera.orthographic = true;
        sectionCamera.orthographicSize = 11.5f;
        sectionCamera.nearClipPlane = 0.1f;
        sectionCamera.farClipPlane = 600f;
        sectionCamera.depth = 10f;
        sectionCamera.rect = _sectionCameraRect;
        sectionCamera.clearFlags = CameraClearFlags.SolidColor;
        sectionCamera.backgroundColor = new Color(0.07f, 0.08f, 0.1f, 1f);
        ExcludeLayerFromCamera(sectionCamera, hiddenLayer);
    }

    SandStormController CreateDustStorm(Transform parent, int stormLayer)
    {
        SandStormController existing = FindFirstObjectByType<SandStormController>();
        if (existing != null)
        {
            existing.transform.SetParent(parent, true);
            SetLayerRecursively(existing.gameObject, stormLayer);
            return existing;
        }

        GameObject storm = InstantiatePrefab(_dustStormPrefab, parent, "Storyboard Dust Storm");
        storm.transform.position = new Vector3(0f, 5f, 0f);
        storm.transform.localScale = Vector3.one * 3.2f;
        SetLayerRecursively(storm, stormLayer);
        EditorSelectionClearOnDestroy.Attach(storm);

        ParticleSystem[] particleSystems = storm.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ConfigureDustStormParticleSystem(particleSystems[i]);
        }

        SandStormController controller = storm.GetComponent<SandStormController>();
        if (controller == null)
        {
            controller = storm.GetComponentInChildren<SandStormController>();
        }

        if (controller == null)
        {
            controller = storm.AddComponent<SandStormController>();
        }

        if (particleSystems.Length > 0)
        {
            controller.Configure(particleSystems[0]);
        }

        return controller;
    }

    void IncludeLayerInCamera(Camera camera, int layer)
    {
        if (camera == null || layer < 0 || layer >= 32)
        {
            return;
        }

        camera.cullingMask |= 1 << layer;
    }

    void ExcludeLayerFromCamera(Camera camera, int layer)
    {
        if (camera == null || layer < 0 || layer >= 32)
        {
            return;
        }

        camera.cullingMask &= ~(1 << layer);
    }

    void ConfigureDustStormParticleSystem(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 5.5f;
        main.startSpeed = 0.4f;
        main.startSize = 2.2f;
        main.maxParticles = 3500;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 420f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(60f, 8f, 24f);

        if (particleSystem.isPlaying == false)
        {
            particleSystem.Play(true);
        }
    }

    void EnsureLights(Transform parent)
    {
        if (FindFirstObjectByType<Light>() != null)
        {
            return;
        }

        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.SetParent(parent);
        lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.94f, 0.82f, 1f);
    }

    void SetDirectorDefaults(StoryboardDirector director)
    {
        SetField(director, "_playOnStart", _playOnStart);
        SetField(director, "_showCaptions", false);
        SetField(director, "_lockCameraToArenaCenter", false);
        SetField(director, "_cameraFollowsYellowTank", true);
        SetField(director, "_cameraLookAheadWeight", 0.05f);
        SetField(director, "_cameraTargetSmooth", 9f);
        SetField(director, "_cameraTargetHeight", 0.65f);
        SetField(director, "_tankGroundY", TankY);
        SetField(director, "_projectileGroundY", GroundPlaneY);
        SetField(director, "_tankTurnSpeed", 10f);
        SetField(director, "_projectileVisualScale", 0.45f);
        SetField(director, "_scriptedProjectileHitRadius", 0.035f);
        SetField(director, "_obstacleWallScale", 1.45f);
        SetField(director, "_placeObstacleInFrontOfActor", true);
        SetField(director, "_obstacleWallDistanceFromTank", 2.1f);
        SetField(director, "_obstacleWallYawOffset", 0f);
        SetField(director, "_damagedWallLayDownEuler", new Vector3(90f, 0f, 0f));
        SetField(director, "_damagedWallGroundOffset", 0.03f);
        SetField(director, "_scriptedAimPreviewDuration", 1.9f);
        SetField(director, "_preActionPause", 0.45f);
        SetField(director, "_betweenMoveAndFirePause", 0.7f);
        SetField(director, "_postAimPause", 0.5f);
        SetField(director, "_postImpactPause", 2.2f);
        SetField(director, "_killCamPostImpactPause", 2.85f);
        SetField(director, "_switchMapAfterBeatAction", true);
        SetField(director, "_useProjectileKillCam", false);
        SetField(director, "_useProjectileKillCamForPlayerShots", true);
        SetField(director, "_useProjectileKillCamForEnemyShots", true);
        SetField(director, "_useMoveControllerForScriptedMoves", true);
        SetField(director, "_useTankAimControllerForScriptedAim", true);
        SetField(director, "_useTankProjectileLauncherForScriptedShots", true);
    }

    void RemoveLegacyImpactMarkers()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform item = transforms[i];
            if (item == null ||
                (item.name != "Gravity Rift" &&
                item.name != "Gravity Impact Debug Marker" &&
                item.name != "Purple Gravity Zone" &&
                item.name != "Purple Gravity Zone Top"))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(item.gameObject);
            }
            else
            {
                DestroyImmediate(item.gameObject);
            }
        }
    }

    Transform FindPrimaryDestructible(Transform propRoot, Transform greenTank, Transform yellowTank)
    {
        Transform best = null;
        float bestScore = float.PositiveInfinity;
        Vector3 forward = yellowTank != null
            ? yellowTank.position - greenTank.position
            : Vector3.right;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.right;
        }

        forward.Normalize();

        for (int i = 0; i < propRoot.childCount; i++)
        {
            Transform prop = propRoot.GetChild(i);
            if (IsActiveRock(prop) == false)
            {
                continue;
            }

            Vector3 toProp = prop.position - greenTank.position;
            toProp.y = 0f;

            float forwardDistance = Vector3.Dot(toProp, forward);
            if (forwardDistance <= 0f)
            {
                continue;
            }

            Vector3 closestPointOnForward = greenTank.position + forward * forwardDistance;
            closestPointOnForward.y = prop.position.y;
            float sideDistance = Vector3.Distance(prop.position, closestPointOnForward);
            float score = sideDistance * 3.5f + forwardDistance;

            if (score < bestScore)
            {
                best = prop;
                bestScore = score;
            }
        }

        return best;
    }

    Transform FindYellowSideDestructible(Transform propRoot, Transform yellowTank, Transform greenTank, Transform excluded)
    {
        Transform best = null;
        float bestScore = float.PositiveInfinity;
        Vector3 forward = greenTank.position - yellowTank.position;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.left;
        }

        forward.Normalize();

        for (int i = 0; i < propRoot.childCount; i++)
        {
            Transform prop = propRoot.GetChild(i);
            if (prop == excluded)
            {
                continue;
            }

            if (IsActiveRock(prop) == false)
            {
                continue;
            }

            Vector3 toProp = prop.position - yellowTank.position;
            toProp.y = 0f;

            float forwardDistance = Vector3.Dot(toProp, forward);
            if (forwardDistance <= 0f)
            {
                continue;
            }

            Vector3 closestPointOnForward = yellowTank.position + forward * forwardDistance;
            closestPointOnForward.y = prop.position.y;
            float sideDistance = Vector3.Distance(prop.position, closestPointOnForward);

            float score = sideDistance * 4f + forwardDistance * 0.35f;
            if (score < bestScore)
            {
                best = prop;
                bestScore = score;
            }
        }

        return best;
    }

    Transform FindNamedRock(Transform propRoot, string rockName)
    {
        if (propRoot == null || string.IsNullOrEmpty(rockName))
        {
            return null;
        }

        for (int i = 0; i < propRoot.childCount; i++)
        {
            Transform prop = propRoot.GetChild(i);
            if (prop == null || prop.name.Contains("Rock") == false)
            {
                continue;
            }

            if (prop.name.Equals(rockName, System.StringComparison.OrdinalIgnoreCase) ||
                prop.name.Contains(rockName))
            {
                return prop;
            }
        }

        return null;
    }

    Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        if (root.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase) ||
            root.name.Contains(targetName))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindDescendantByName(root.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    void EnsureStoryboardPropActive(Transform prop)
    {
        if (prop != null && prop.gameObject.activeSelf == false)
        {
            prop.gameObject.SetActive(true);
        }
    }

    bool IsActiveRock(Transform prop)
    {
        return prop != null &&
            prop.gameObject.activeSelf &&
            prop.name.Contains("Rock");
    }

    void AddGroundCollider(GameObject root, int groundLayer)
    {
        GameObject colliderObject = new GameObject("Ground Collision Plane");
        colliderObject.layer = groundLayer;
        colliderObject.transform.SetParent(root.transform);
        colliderObject.transform.localPosition = new Vector3(0f, GroundPlaneY, 0f);
        BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(64f, 0.2f, 26f);
    }

    GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
    {
        GameObject instance = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        instance.name = name;

        if (parent != null)
        {
            instance.transform.SetParent(parent);
        }

        return instance;
    }

    GameObject GetArrayPrefab(GameObject[] prefabs, int index)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        return prefabs[Mathf.Clamp(index, 0, prefabs.Length - 1)];
    }

    Transform CreateChild(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent);
        return child;
    }

    void EnsureCollider(GameObject target, Vector3 fallbackSize)
    {
        if (target.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        BoxCollider box = target.AddComponent<BoxCollider>();
        box.size = fallbackSize;
    }

    void AlignBottomToGround(GameObject target, float groundY)
    {
        if (TryGetRendererBounds(target, out Bounds bounds) == false)
        {
            return;
        }

        target.transform.position += Vector3.up * (groundY - bounds.min.y);
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

    void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }

    void SetField(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
