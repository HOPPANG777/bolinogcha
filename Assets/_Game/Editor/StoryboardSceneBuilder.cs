using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StoryboardSceneBuilder
{
    const string VzScenePath = "Assets/_Game/Scenes/VZScene.unity";

    [MenuItem("Tools/Bolinogcha/Prepare VZ Storyboard Scene")]
    public static void PrepareVzStoryboardScene()
    {
        EnsureFolders();

        Scene scene = EditorSceneManager.OpenScene(VzScenePath, OpenSceneMode.Single);
        StoryboardSceneBootstrapper bootstrapper = Object.FindFirstObjectByType<StoryboardSceneBootstrapper>();

        if (bootstrapper == null)
        {
            bootstrapper = new GameObject("PDF Storyboard Bootstrapper")
                .AddComponent<StoryboardSceneBootstrapper>();
        }

        ConfigureBootstrapper(bootstrapper);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = bootstrapper.gameObject;

        Debug.Log("Prepared VZ storyboard scene: " + VzScenePath);
    }

    static void ConfigureBootstrapper(StoryboardSceneBootstrapper bootstrapper)
    {
        SerializedObject serializedObject = new SerializedObject(bootstrapper);

        SetBool(serializedObject, "_buildOnAwake", true);
        SetBool(serializedObject, "_playOnStart", true);
        SetBool(serializedObject, "_useCustomStoryBeats", false);
        SetArraySize(serializedObject, "_customStoryBeats", 0);
        SetBool(serializedObject, "_useExistingSceneLayout", true);
        SetString(serializedObject, "_existingMapName", "map1");
        SetString(serializedObject, "_yellowTankName", "Tank_Yellow_Prefab");
        SetString(serializedObject, "_greenTankName", "Tank_Green_Prefab");
        SetBool(serializedObject, "_detachExistingProps", true);
        SetBool(serializedObject, "_markScenePropsDestructible", true);
        SetObjectArray(serializedObject, "_mapPrefabs", LoadMapPrefabs());
        SetObject(serializedObject, "_yellowTankPrefab", LoadAsset<GameObject>("Assets/_Game/Model/Tank/Prefab/Tank_Yellow_Prefab.prefab"));
        SetObject(serializedObject, "_greenTankPrefab", LoadAsset<GameObject>("Assets/_Game/Model/Tank/Prefab/Tank_Green_Prefab.prefab"));
        SetObject(serializedObject, "_damageTankPrefab", LoadAsset<GameObject>("Assets/_Game/Model/Tank/Prefab/Tank_Damage_Prefab.prefab"));
        SetObjectArray(serializedObject, "_rockPrefabs", LoadRockPrefabs());
        SetObjectArray(serializedObject, "_propPrefabs", LoadPropPrefabs());
        SetObject(serializedObject, "_chargedProjectilePrefab", LoadAsset<GameObject>("Assets/_Game/Model/Missile/충전탄/충전탄_Prefab.prefab"));
        SetObject(serializedObject, "_gravityProjectilePrefab", LoadAsset<GameObject>("Assets/_Game/Model/Missile/중력탄/중력탄_Prefab.prefab"));
        SetObject(serializedObject, "_obstacleWallPrefab", LoadAsset<GameObject>("Assets/_Game/Model/Wall/유틸벽_Prefab.prefab"));
        SetObject(serializedObject, "_obstacleWallDamagePrefab", LoadAsset<GameObject>("Assets/_Game/Model/Wall/유틸벽_Damage_Prefab.prefab"));
        SetObject(serializedObject, "_explosionPrefab", LoadAsset<GameObject>("Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/BigExplosion.prefab"));
        SetObject(serializedObject, "_dustStormPrefab", LoadAsset<GameObject>("Assets/_Game/Model/DustStorm Variant.prefab"));
        SetVector3(serializedObject, "_mainCameraOffset", new Vector3(7.2f, 4.2f, 0f));
        SetVector3(serializedObject, "_mainCameraRotation", new Vector3(20f, -90f, 0f));
        SetRect(serializedObject, "_sectionCameraRect", new Rect(0.64f, 0.02f, 0.34f, 0.3f));

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject[] LoadMapPrefabs()
    {
        string[] paths =
        {
            "Assets/_Game/Model/Map/Prefab/map1.prefab",
            "Assets/_Game/Model/Map/Prefab/map2.prefab",
            "Assets/_Game/Model/Map/Prefab/map3,4.prefab",
            "Assets/_Game/Model/Map/Prefab/map5.prefab",
            "Assets/_Game/Model/Map/Prefab/map6.prefab",
            "Assets/_Game/Model/Map/Prefab/map7.prefab",
            "Assets/_Game/Model/Map/Prefab/map8.prefab"
        };

        return LoadAssets(paths);
    }

    static GameObject[] LoadRockPrefabs()
    {
        string[] paths =
        {
            "Assets/_Game/Model/Prop/Rock1 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock2 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock3 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock4 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock5 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock6 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock7 Variant.prefab",
            "Assets/_Game/Model/Prop/Rock8 Variant.prefab"
        };

        return LoadAssets(paths);
    }

    static GameObject[] LoadPropPrefabs()
    {
        string[] paths =
        {
            "Assets/_Game/Model/Prop/Cactus_1_Prefab.prefab",
            "Assets/_Game/Model/Prop/Cactus_2_Prefab.prefab",
            "Assets/_Game/Model/Prop/DryTree_Prefab.prefab",
            "Assets/_Game/Model/Prop/DeadTreeLowPrefab.prefab"
        };

        return LoadAssets(paths);
    }

    static GameObject[] LoadAssets(string[] paths)
    {
        GameObject[] assets = new GameObject[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            assets[i] = LoadAsset<GameObject>(paths[i]);
        }

        return assets;
    }

    static T LoadAsset<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning("Missing storyboard asset: " + path);
        }

        return asset;
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/_Game", "Assets", "_Game");
        EnsureFolder("Assets/_Game/Scenes", "Assets/_Game", "Scenes");
        EnsureFolder("Assets/_Game/Storyboards", "Assets/_Game", "Storyboards");
        EnsureFolder("Assets/_Game/Editor", "Assets/_Game", "Editor");
    }

    static void EnsureFolder(string path, string parent, string name)
    {
        if (AssetDatabase.IsValidFolder(path) == false)
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    static void SetRect(SerializedObject serializedObject, string propertyName, Rect value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.rectValue = value;
        }
    }

    static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    static void SetObjectArray(SerializedObject serializedObject, string propertyName, Object[] values)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    static void SetArraySize(SerializedObject serializedObject, string propertyName, int size)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.arraySize = Mathf.Max(0, size);
        }
    }
}
