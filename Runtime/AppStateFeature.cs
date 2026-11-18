#pragma warning disable CS1998 // virtual hooks are async no-ops so overrides can await
using System;
using UnityEngine;

namespace AppFlow
{
    /// <summary>
    /// Reusable lifecycle logic attached to a state in the inspector. Every non-abstract subclass shows up in "+ Add Feature".
    /// Features wrap the state's <see cref="AppStateBehaviour"/>: they run before it while entering/running and after it while exiting.
    /// </summary>
    [Serializable]
    public abstract class AppStateFeature
    {
        /// <summary>Header checkbox in the inspector; disabled features are skipped at runtime.</summary>
        public bool Enabled = true;

        public AppFlowMachine Machine { get; internal set; }
        public AppStateRef State { get; internal set; }

        public virtual async Awaitable OnPreEnter(AppTransitionContext ctx) { }
        public virtual async Awaitable OnEnter(AppTransitionContext ctx) { }
        public virtual async Awaitable OnPostEnter(AppTransitionContext ctx) { }
        public virtual async Awaitable OnPreExit(AppTransitionContext ctx) { }
        public virtual async Awaitable OnExit(AppTransitionContext ctx) { }
        public virtual async Awaitable OnPostExit(AppTransitionContext ctx) { }

        /// <summary>Called once when the state becomes Running.</summary>
        public virtual void OnRunning() { }

        /// <summary>Called every frame while Running.</summary>
        public virtual void OnRunningUpdate() { }
    }
}
