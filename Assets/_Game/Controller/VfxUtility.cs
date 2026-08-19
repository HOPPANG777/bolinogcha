using UnityEngine;

public static class VfxUtility
{
    // ParticlePack prefabs are authored as looping effects; force a single burst.
    public static void MakeOneShot(GameObject vfxInstance, float lifetime)
    {
        if (vfxInstance == null)
        {
            return;
        }

        ParticleSystem[] systems = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.loop = false;
        }

        if (lifetime > 0f)
        {
            EditorSelectionClearOnDestroy.Attach(vfxInstance);
            Object.Destroy(vfxInstance, lifetime);
        }
    }
}
