using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class EditorSelectionClearOnDestroy : MonoBehaviour
{
    [SerializeField] bool _clearChildSelection = true;

    public static void Attach(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (target.GetComponent<EditorSelectionClearOnDestroy>() == null)
        {
            target.AddComponent<EditorSelectionClearOnDestroy>();
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            ClearSelectionIfInside(gameObject, _clearChildSelection);
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        ClearSelectionIfInside(gameObject, _clearChildSelection);
#endif
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        ClearSelectionIfInside(gameObject, _clearChildSelection);
#endif
    }

#if UNITY_EDITOR
    static void ClearSelectionIfInside(GameObject root, bool includeChildren)
    {
        if (root == null)
        {
            return;
        }

        UnityEngine.Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        List<UnityEngine.Object> keptObjects = new List<UnityEngine.Object>(selectedObjects.Length);
        bool changed = false;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            UnityEngine.Object selectedObject = selectedObjects[i];
            if (selectedObject == null)
            {
                changed = true;
                continue;
            }

            GameObject selectedGameObject = GetGameObject(selectedObject);
            if (selectedGameObject != null && IsInsideRoot(selectedGameObject, root, includeChildren))
            {
                changed = true;
                continue;
            }

            keptObjects.Add(selectedObject);
        }

        if (changed)
        {
            Selection.objects = keptObjects.ToArray();
            EditorApplication.delayCall += delegate
            {
                if (Selection.objects == null || Selection.objects.Length == 0)
                {
                    ActiveEditorTracker.sharedTracker.ForceRebuild();
                }
            };
        }
    }

    static bool IsInsideRoot(GameObject selectedGameObject, GameObject root, bool includeChildren)
    {
        if (selectedGameObject == root)
        {
            return true;
        }

        return includeChildren &&
            selectedGameObject.transform != null &&
            root.transform != null &&
            selectedGameObject.transform.IsChildOf(root.transform);
    }

    static GameObject GetGameObject(UnityEngine.Object selectedObject)
    {
        if (selectedObject is GameObject gameObject)
        {
            return gameObject;
        }

        if (selectedObject is Component component)
        {
            return component.gameObject;
        }

        return null;
    }
#endif
}
