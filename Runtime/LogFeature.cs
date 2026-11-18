using System;
using UnityEngine;

namespace AppFlow
{
    /// <summary>Built-in feature: logs a message on Enter and Exit.</summary>
    [Serializable]
    public sealed class LogFeature : AppStateFeature
    {
        public string Message = "";

        public override async Awaitable OnEnter(AppTransitionContext ctx) => Debug.Log($"[AppFlow] {State.name} Enter: {Message}");
        public override async Awaitable OnExit(AppTransitionContext ctx) => Debug.Log($"[AppFlow] {State.name} Exit: {Message}");
    }
}
