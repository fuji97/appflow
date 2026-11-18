using System.Linq;
using System.Threading.Tasks;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace AppFlow
{
    /// <summary>`unity command appflow ...`: inspect and drive the active <see cref="AppFlowRunner"/> (Play mode or development Players).</summary>
    static class AppFlowCommands
    {
        const string Group = "appflow";

        static AppFlowMachine Machine(CommandContext cmd)
        {
            var machine = AppFlowRunner.Active != null ? AppFlowRunner.Active.Machine : null;
            if (machine == null)
                cmd.Log.Error("No AppFlowRunner is active: enter Play mode in a scene with an AppFlowRunner.");
            return machine;
        }

        [CliGroupCommand(Group, "Show the active state, phase and whether a transition is running.", Effect = CommandEffect.ReadOnly, MainThreadRequired = true, Tags = new[] { "runtime" })]
        internal static ExitCode Status(CommandContext cmd)
        {
            var m = Machine(cmd);
            if (m == null) return ExitCode.Failed;
            cmd.Result.Write(new
            {
                asset = m.Asset.name,
                state = m.CurrentState != null ? m.CurrentState.name : null,
                stateId = m.CurrentState != null ? m.CurrentState.StateId.ToString() : null,
                phase = m.CurrentPhase.ToString(),
                transitioning = m.IsTransitioning,
            });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "List the states of the active app flow.", Effect = CommandEffect.ReadOnly, MainThreadRequired = true, Tags = new[] { "runtime" })]
        internal static ExitCode ListStates(CommandContext cmd)
        {
            var m = Machine(cmd);
            if (m == null) return ExitCode.Failed;
            var currentId = m.CurrentState != null ? m.CurrentState.StateId : default;
            foreach (var s in m.Asset.States)
            {
                var edge = m.CurrentState != null ? m.Asset.FindTransition(currentId, s.Id) : null;
                cmd.Result.WriteRecord(new
                {
                    name = s.Name,
                    id = s.Id.ToString(),
                    active = m.CurrentState != null && s.Id == currentId,
                    entry = s.Id == m.Asset.EntryStateId,
                    parallelPre = s.ParallelPre,
                    parallelEnter = s.ParallelEnter,
                    parallelExit = s.ParallelExit,
                    parallelPost = s.ParallelPost,
                    scene = s.Features.OfType<SceneFeature>().FirstOrDefault(f => f.Enabled)?.Scene.Path,
                    behaviour = s.Behaviour != null ? s.Behaviour.name : null,
                    customTransitionFromActive = edge?.Behaviour != null,
                    phaseModeFromActive = edge != null && !edge.Modes.IsInherit ? $"{edge.Modes.Pre}/{edge.Modes.Main}/{edge.Modes.Post}" : null,
                });
            }
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Transit to a state by name or id.", Effect = CommandEffect.Additive, MainThreadRequired = true, Tags = new[] { "runtime" })]
        internal static async Task<ExitCode> Transit(
            CommandContext cmd,
            [CliArg("state", "State name or id", Required = true)] string state,
            [CliArg("wait", "Wait for the transition to finish")] bool wait = true,
            [CliArg("timeout", "Timeout in seconds")] double timeoutInSeconds = 120)
        {
            var m = Machine(cmd);
            if (m == null) return ExitCode.Failed;
            if (!m.TryFindState(state, out var data, out var error))
            {
                cmd.Log.Error(error);
                return ExitCode.NotFound;
            }
            if (m.IsTransitioning)
            {
                cmd.Log.Error($"Transition in progress: '{m.ActiveTransition.From}' -> '{m.ActiveTransition.To}'");
                return ExitCode.Failed;
            }

            bool? ok = null;
            async Awaitable Go() { ok = await m.TransitTo(data.Id); }
            _ = Go();

            if (!wait)
            {
                cmd.Result.Write(new { started = data.Name });
                return ExitCode.Success;
            }

            var deadline = Time.realtimeSinceStartupAsDouble + timeoutInSeconds;
            while (!ok.HasValue)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline)
                {
                    cmd.Log.Error($"Timed out after {timeoutInSeconds}s waiting for '{data.Name}'; the transition is still running.");
                    return ExitCode.TimedOut;
                }
                await Task.Yield();
                cmd.CancellationToken.ThrowIfCancellationRequested();
            }

            if (!ok.Value)
            {
                cmd.Log.Error($"Transition to '{data.Name}' failed; see the console.");
                return ExitCode.Failed;
            }
            var last = m.History[^1];
            cmd.Result.Write(new { state = m.CurrentState.name, phase = m.CurrentPhase.ToString(), duration = last.Duration, exceptions = last.Exceptions });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Show recent transitions, newest last.", Effect = CommandEffect.ReadOnly, MainThreadRequired = true, Tags = new[] { "runtime" })]
        internal static ExitCode History(CommandContext cmd, [CliArg("count", "Most recent N records")] int count = 10)
        {
            var m = Machine(cmd);
            if (m == null) return ExitCode.Failed;
            for (var i = Mathf.Max(0, m.History.Count - count); i < m.History.Count; i++)
                cmd.Result.WriteRecord(m.History[i]);
            return ExitCode.Success;
        }
    }
}
