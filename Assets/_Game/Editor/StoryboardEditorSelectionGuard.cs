using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class StoryboardEditorSelectionGuard
{
    static StoryboardEditorSelectionGuard()
    {
        EditorApplication.update += RemoveMissingSelections;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += RemoveMissingSelections;
        Selection.selectionChanged += RemoveMissingSelections;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            Selection.objects = new Object[0];
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }

    static void RemoveMissingSelections()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        List<Object> keptObjects = new List<Object>(selectedObjects.Length);
        bool changed = false;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            Object selectedObject = selectedObjects[i];
            if (selectedObject == null)
            {
                changed = true;
                continue;
            }

            keptObjects.Add(selectedObject);
        }

        if (changed)
        {
            Selection.objects = keptObjects.ToArray();
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}
