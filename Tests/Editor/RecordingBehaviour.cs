#pragma warning disable CS1998
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppFlow.Tests
{
    /// <summary>Appends "State.Phase" to a shared static log. Statics because the machine instantiates copies of the asset.</summary>
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class RecordingBehaviour : AppStateBehaviour
    {
        public static List<string> Log = new();
        public static AwaitableCompletionSource ExitGate;   // holds Exit of state "A" until released
        public static string ThrowInEnterOf;

        public override async Awaitable OnPreEnter(AppTransitionContext ctx) => Log.Add($"{State.name}.PreEnter");

        public override async Awaitable OnEnter(AppTransitionContext ctx)
        {
            Log.Add($"{State.name}.Enter");
            if (ThrowInEnterOf == State.name) throw new InvalidOperationException("boom");
        }

        public override async Awaitable OnPostEnter(AppTransitionContext ctx) => Log.Add($"{State.name}.PostEnter");
        public override async Awaitable OnPreExit(AppTransitionContext ctx) => Log.Add($"{State.name}.PreExit");

        public override async Awaitable OnExit(AppTransitionContext ctx)
        {
            Log.Add($"{State.name}.Exit");
            if (State.name == "A" && ExitGate != null) await ExitGate.Awaitable;
        }

        public override async Awaitable OnPostExit(AppTransitionContext ctx) => Log.Add($"{State.name}.PostExit");
        public override void OnRunning() => Log.Add($"{State.name}.Running");
    }

    /// <summary>Logs "State.F.Phase" into <see cref="RecordingBehaviour.Log"/>.</summary>
    [Serializable]
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class RecordingFeature : AppStateFeature
    {
        public static int Updates;

        public override async Awaitable OnPreEnter(AppTransitionContext ctx) => RecordingBehaviour.Log.Add($"{State.name}.F.PreEnter");
        public override async Awaitable OnEnter(AppTransitionContext ctx) => RecordingBehaviour.Log.Add($"{State.name}.F.Enter");
        public override async Awaitable OnPostEnter(AppTransitionContext ctx) => RecordingBehaviour.Log.Add($"{State.name}.F.PostEnter");
        public override async Awaitable OnPreExit(AppTransitionContext ctx) => RecordingBehaviour.Log.Add($"{State.name}.F.PreExit");
        public override async Awaitable OnExit(AppTransitionContext ctx) => RecordingBehaviour.Log.Add($"{State.name}.F.Exit");
        public override async Awaitable OnPostExit(AppTransitionContext ctx) => RecordingBehaviour.Log.Add($"{State.name}.F.PostExit");
        public override void OnRunning() => RecordingBehaviour.Log.Add($"{State.name}.F.Running");
        public override void OnRunningUpdate() => Updates++;
    }

    /// <summary>Custom transition that only enters the target.</summary>
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class RecordingTransition : AppTransitionBehaviour
    {
        public static AppStateBehaviour LastFrom, LastTo;

        public override async Awaitable Run(AppTransitionContext ctx)
        {
            LastFrom = ctx.From;
            LastTo = ctx.To;
            RecordingBehaviour.Log.Add($"custom {ctx.FromRef.name}->{ctx.ToRef.name}");
            await ctx.Enter();
        }
    }
}
