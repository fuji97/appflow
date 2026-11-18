using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppFlow
{
    /// <summary>Scene picked in the inspector. The editor stores the GUID; the importer refreshes <see cref="Path"/> from it.</summary>
    [Serializable]
    public struct SceneReference
    {
        public string Guid;
        public string Path;
    }

    /// <summary>Built-in feature: loads a scene in Enter (before the behaviour) and unloads it in Exit (after the behaviour).</summary>
    [Serializable]
    public sealed class SceneFeature : AppStateFeature
    {
        public SceneReference Scene;
        public LoadSceneMode LoadMode = LoadSceneMode.Additive;
        [Tooltip("Unload the scene when the state exits (Additive only)")]
        public bool UnloadOnExit = true;

        public override async Awaitable OnEnter(AppTransitionContext ctx)
        {
            if (!string.IsNullOrEmpty(Scene.Path))
                await SceneManager.LoadSceneAsync(Scene.Path, LoadMode);
        }

        public override async Awaitable OnExit(AppTransitionContext ctx)
        {
            if (UnloadOnExit && LoadMode == LoadSceneMode.Additive && !string.IsNullOrEmpty(Scene.Path)
                && SceneManager.GetSceneByPath(Scene.Path).isLoaded)
                await SceneManager.UnloadSceneAsync(Scene.Path);
        }
    }
}
