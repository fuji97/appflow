using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppFlow.Sample
{
    /// <summary>Game keeps its scene loaded while paused (its Scene feature has UnloadOnExit off) and unloads it itself otherwise.</summary>
    [CreateAssetMenu(menuName = "App Flow/Sample/Game State")]
    public class GameStateBehaviour : ScreenStateBehaviour
    {
        public override async Awaitable OnExit(AppTransitionContext ctx)
        {
            if (ctx.ToRef == Back)
            {
                if (View is GameView game) game.SetFrozen(true);
                return;
            }
            await base.OnExit(ctx);
            await UnloadSceneOf(Machine, State);
        }

        public override void OnRunning()
        {
            base.OnRunning();
            if (View is GameView game) game.SetFrozen(false);
        }

        internal static async Awaitable UnloadSceneOf(AppFlowMachine machine, AppStateRef state)
        {
            var path = machine.Asset.FindState(state.StateId).Features.OfType<SceneFeature>().FirstOrDefault()?.Scene.Path;
            if (!string.IsNullOrEmpty(path) && SceneManager.GetSceneByPath(path).isLoaded) await SceneManager.UnloadSceneAsync(path);
        }
    }
}
