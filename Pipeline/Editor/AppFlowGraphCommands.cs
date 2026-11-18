using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AppFlow.Editor
{
    /// <summary>`unity command appflow.graph ...`: inspect and author .appflow graphs in edit mode.</summary>
    static class AppFlowGraphCommands
    {
        const string Group = "appflow.graph";
        const string Saves = " Saves the asset.";

        // ---- helpers ----

        static ExitCode Open(CommandContext cmd, string asset, out AppFlowStateMachine sm)
        {
            sm = null;
            var path = ResolveAssetPath(cmd, asset, out var code);
            if (path == null) return code;
            sm = StateMachineDatabase.LoadStateMachine<AppFlowStateMachine>(path);
            if (sm == null)
            {
                cmd.Log.Error($"No app flow graph at '{path}'.");
                return ExitCode.NotFound;
            }
            return ExitCode.Success;
        }

        static string ResolveAssetPath(CommandContext cmd, string asset, out ExitCode code)
        {
            code = ExitCode.Success;
            var path = ProjectPaths.Resolve(asset, out var error);
            if (path == null)
            {
                cmd.Log.Error(error);
                code = ExitCode.InvalidUsage;
                return null;
            }
            var ext = "." + AppFlowStateMachine.AssetExtension;
            return path.EndsWith(ext) ? path : path + ext;
        }

        static ExitCode FindState(CommandContext cmd, AppFlowStateMachine sm, string nameOrId, out AppState state)
        {
            state = null;
            var states = sm.GetStates().OfType<AppState>().ToList();
            var byName = states.Where(s => string.Equals(s.StateName, nameOrId, System.StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1) { state = byName[0]; return ExitCode.Success; }
            if (byName.Count > 1)
            {
                cmd.Log.Error($"Name '{nameOrId}' is ambiguous: " + string.Join(", ", byName.Select(s => $"{s.StateName} ({s.ID})")));
                return ExitCode.InvalidUsage;
            }
            var id = Hash128.Parse(nameOrId ?? "");
            if (id.isValid) state = states.FirstOrDefault(s => s.ID == id);
            if (state != null) return ExitCode.Success;
            cmd.Log.Error($"No state named or with id '{nameOrId}'");
            return ExitCode.NotFound;
        }

        // Empty path -> null object (clears). Missing or wrongly typed asset -> NotFound.
        static ExitCode LoadObject<T>(CommandContext cmd, string assetPath, out T obj) where T : Object
        {
            obj = null;
            if (string.IsNullOrEmpty(assetPath)) return ExitCode.Success;
            var path = ProjectPaths.Resolve(assetPath, out var error);
            if (path == null)
            {
                cmd.Log.Error(error);
                return ExitCode.InvalidUsage;
            }
            obj = AssetDatabase.LoadAssetAtPath<T>(path);
            if (obj != null) return ExitCode.Success;
            cmd.Log.Error($"No {typeof(T).Name} at '{path}'.");
            return ExitCode.NotFound;
        }

        static string PathOf(Object o) => o != null ? AssetDatabase.GetAssetPath(o) : null;

        // ---- commands ----

        [CliGroupCommand(Group, "List the states, transitions and validation issues of an app flow graph.", Effect = CommandEffect.ReadOnly, MainThreadRequired = true)]
        internal static ExitCode Describe(CommandContext cmd, [CliArg("asset", "Path of the .appflow asset", Required = true)] string asset)
        {
            var code = Open(cmd, asset, out var sm);
            if (code != ExitCode.Success) return code;

            foreach (var s in sm.GetStates().OfType<AppState>())
            {
                var st = s.Settings;
                var features = st.Features ?? new List<AppStateFeature>();
                var scene = features.OfType<SceneFeature>().FirstOrDefault();
                cmd.Result.WriteRecord(new
                {
                    kind = "state",
                    name = s.StateName,
                    id = s.ID.ToString(),
                    entry = st.Entry,
                    parallelPre = st.ParallelPre,
                    parallelEnter = st.ParallelEnter,
                    parallelExit = st.ParallelExit,
                    parallelPost = st.ParallelPost,
                    behaviour = PathOf(st.Behaviour),
                    features = features.Select(f => f == null ? "(missing)" : f.GetType().Name + (f.Enabled ? "" : " (disabled)")).ToArray(),
                    scene = scene != null && !string.IsNullOrEmpty(scene.Scene.Guid) ? AssetDatabase.GUIDToAssetPath(scene.Scene.Guid) : null,
                    loadMode = scene?.LoadMode.ToString(),
                    unloadOnExit = scene?.UnloadOnExit,
                });
            }

            foreach (var s in sm.GetStates().OfType<AppState>())
                foreach (var t in s.GetOutgoingTransitions())
                {
                    var conditions = AppFlowValidation.ConditionsOf(t).ToList();
                    var pm = conditions.OfType<PhaseModeCondition>().FirstOrDefault()?.Value ?? default;
                    cmd.Result.WriteRecord(new
                    {
                        kind = "transition",
                        id = t.ID.ToString(),
                        from = AppState.NameOf(t.FromState),
                        to = AppState.NameOf(t.ToState),
                        customTransition = PathOf(conditions.OfType<CustomTransitionCondition>().FirstOrDefault()?.Value),
                        pre = pm.Pre.ToString(),
                        main = pm.Main.ToString(),
                        post = pm.Post.ToString(),
                    });
                }

            foreach (var issue in AppFlowValidation.Validate(sm))
                cmd.Result.WriteRecord(new
                {
                    kind = "issue",
                    severity = issue.IsError ? "error" : "warning",
                    message = issue.Message,
                    context = issue.Context switch
                    {
                        IState st => AppState.NameOf(st),
                        ITransition tr => $"{AppState.NameOf(tr.FromState)} -> {AppState.NameOf(tr.ToState)}",
                        _ => null,
                    },
                });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Create an empty app flow graph." + Saves, Effect = CommandEffect.Additive, MainThreadRequired = true)]
        internal static ExitCode Create(CommandContext cmd, [CliArg("asset", "Path of the .appflow asset to create", Required = true)] string asset)
        {
            var path = ResolveAssetPath(cmd, asset, out var code);
            if (path == null) return code;
            if (File.Exists(path))
            {
                cmd.Log.Error($"Asset already exists: '{path}'");
                return ExitCode.InvalidUsage;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sm = StateMachineDatabase.CreateStateMachine<AppFlowStateMachine>(path);
            if (sm == null)
            {
                cmd.Log.Error($"Could not create '{path}'.");
                return ExitCode.Failed;
            }
            cmd.Result.Write(new { asset = path });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Add a state to an app flow graph." + Saves, Effect = CommandEffect.Additive, MainThreadRequired = true)]
        internal static ExitCode AddState(
            CommandContext cmd,
            [CliArg("asset", "Path of the .appflow asset", Required = true)] string asset,
            [CliArg("name", "Unique state name", Required = true)] string name,
            [CliArg("x", "Graph x position")] float x = 0,
            [CliArg("y", "Graph y position")] float y = 0,
            [CliArg("entry", "Make this the Entry state")] bool entry = false,
            [CliArg("parallel-pre", "PreExit/PreEnter may run Parallel with another state")] bool parallelPre = false,
            [CliArg("parallel-enter", "Enter may run Parallel with the previous state's Exit (when that state has Parallel Exit)")] bool parallelEnter = false,
            [CliArg("parallel-exit", "Exit may run Parallel with the next state's Enter (when that state has Parallel Enter)")] bool parallelExit = false,
            [CliArg("parallel-post", "PostExit/PostEnter may run Parallel with another state")] bool parallelPost = false)
        {
            var code = Open(cmd, asset, out var sm);
            if (code != ExitCode.Success) return code;
            if (sm.GetStates().Any(s => string.Equals(AppState.NameOf(s), name, System.StringComparison.OrdinalIgnoreCase)))
            {
                cmd.Log.Error($"A state named '{name}' already exists.");
                return ExitCode.InvalidUsage;
            }

            var state = new AppState { Position = new Vector2(x, y) };
            sm.UndoBeginRecordStateMachine("add_state");
            try
            {
                sm.AddState(state);
                state.SetSettings(new AppStateSettings { Name = name, Entry = entry, ParallelPre = parallelPre, ParallelEnter = parallelEnter, ParallelExit = parallelExit, ParallelPost = parallelPost });
                state.Title = name; // shows immediately in an open window; the option is what persists
                StateMachineDatabase.SaveStateMachine(sm);
            }
            finally { sm.UndoEndRecordStateMachine(); }
            cmd.Result.Write(new { name, id = state.ID.ToString(), parallelPre, parallelEnter, parallelExit, parallelPost });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Connect two states and set what the edge does (custom transition and/or phase mode)." + Saves, Effect = CommandEffect.Idempotent, MainThreadRequired = true)]
        internal static ExitCode Connect(
            CommandContext cmd,
            [CliArg("asset", "Path of the .appflow asset", Required = true)] string asset,
            [CliArg("from", "Source state name or id", Required = true)] string from,
            [CliArg("to", "Target state name or id", Required = true)] string to,
            [CliArg("transition", "Asset path of an AppTransitionBehaviour; empty for none")] string transition = "",
            [CliArg("pre", "Pre group: Inherit, Sequential or Parallel")] AppPhaseMode pre = AppPhaseMode.Inherit,
            [CliArg("main", "Main group: Inherit, Sequential or Parallel")] AppPhaseMode main = AppPhaseMode.Inherit,
            [CliArg("post", "Post group: Inherit, Sequential or Parallel")] AppPhaseMode post = AppPhaseMode.Inherit)
        {
            var code = Open(cmd, asset, out var sm);
            if (code != ExitCode.Success) return code;
            if ((code = FindState(cmd, sm, from, out var a)) != ExitCode.Success) return code;
            if ((code = FindState(cmd, sm, to, out var b)) != ExitCode.Success) return code;
            if ((code = LoadObject<AppTransitionBehaviour>(cmd, transition, out var behaviour)) != ExitCode.Success) return code;

            var existing = sm.GetTransitions(a, b).FirstOrDefault();
            var oldConditions = existing != null ? AppFlowValidation.ConditionsOf(existing).ToArray() : new Condition[0];
            sm.UndoBeginRecordStateMachine("connect", oldConditions);
            ITransition t;
            try
            {
                t = existing ?? sm.Connect(a, b);
                var root = t.GetRule(0).RootCondition;
                foreach (var c in root.Get().Where(c => c is CustomTransitionCondition or PhaseModeCondition).ToList())
                    root.Remove(c);
                if (behaviour != null) root.Add(new CustomTransitionCondition { Value = behaviour });
                var modes = new AppPhaseModes { Pre = pre, Main = main, Post = post };
                if (!modes.IsInherit) root.Add(new PhaseModeCondition { Value = modes });
                StateMachineDatabase.SaveStateMachine(sm);
            }
            finally { sm.UndoEndRecordStateMachine(); }
            cmd.Result.Write(new { id = t.ID.ToString(), from = a.StateName, to = b.StateName, customTransition = PathOf(behaviour), pre = pre.ToString(), main = main.ToString(), post = post.ToString() });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Set the AppStateBehaviour of a state (empty clears)." + Saves, Effect = CommandEffect.Idempotent, MainThreadRequired = true)]
        internal static ExitCode SetBehaviour(
            CommandContext cmd,
            [CliArg("asset", "Path of the .appflow asset", Required = true)] string asset,
            [CliArg("state", "State name or id", Required = true)] string state,
            [CliArg("behaviour", "Asset path of an AppStateBehaviour; empty clears")] string behaviour = "")
        {
            var code = Open(cmd, asset, out var sm);
            if (code != ExitCode.Success) return code;
            if ((code = FindState(cmd, sm, state, out var s)) != ExitCode.Success) return code;
            if ((code = LoadObject<AppStateBehaviour>(cmd, behaviour, out var obj)) != ExitCode.Success) return code;

            sm.UndoBeginRecordStateMachine("set_behaviour");
            try
            {
                var st = s.Settings.Clone();
                st.Behaviour = obj;
                s.SetSettings(st);
                StateMachineDatabase.SaveStateMachine(sm);
            }
            finally { sm.UndoEndRecordStateMachine(); }
            cmd.Result.Write(new { state = s.StateName, behaviour = PathOf(obj) });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Set which phases of a state may run Parallel with the other state's. Pre/Post: both states must flag the group. Exit/Enter group: the leaving state's Exit and the entering state's Enter. Flags not passed become false." + Saves, Effect = CommandEffect.Idempotent, MainThreadRequired = true)]
        internal static ExitCode SetParallel(
            CommandContext cmd,
            [CliArg("asset", "Path of the .appflow asset", Required = true)] string asset,
            [CliArg("state", "State name or id", Required = true)] string state,
            [CliArg("pre", "PreExit/PreEnter group")] bool pre = false,
            [CliArg("enter", "Enter side of the Exit/Enter group")] bool enter = false,
            [CliArg("exit", "Exit side of the Exit/Enter group")] bool exit = false,
            [CliArg("post", "PostExit/PostEnter group")] bool post = false)
        {
            var code = Open(cmd, asset, out var sm);
            if (code != ExitCode.Success) return code;
            if ((code = FindState(cmd, sm, state, out var s)) != ExitCode.Success) return code;

            sm.UndoBeginRecordStateMachine("set_parallel");
            try
            {
                var st = s.Settings.Clone();
                st.ParallelPre = pre;
                st.ParallelEnter = enter;
                st.ParallelExit = exit;
                st.ParallelPost = post;
                s.SetSettings(st);
                StateMachineDatabase.SaveStateMachine(sm);
            }
            finally { sm.UndoEndRecordStateMachine(); }
            cmd.Result.Write(new { state = s.StateName, pre, enter, exit, post });
            return ExitCode.Success;
        }

        [CliGroupCommand(Group, "Set the scene a state loads through its Scene feature (empty removes the feature)." + Saves, Effect = CommandEffect.Idempotent, MainThreadRequired = true)]
        internal static ExitCode SetScene(
            CommandContext cmd,
            [CliArg("asset", "Path of the .appflow asset", Required = true)] string asset,
            [CliArg("state", "State name or id", Required = true)] string state,
            [CliArg("scene", "Asset path of a scene; empty removes the Scene feature")] string scene = "",
            [CliArg("load-mode", "Single or Additive")] LoadSceneMode loadMode = LoadSceneMode.Additive,
            [CliArg("unload-on-exit", "Unload an additive scene when the state exits")] bool unloadOnExit = true)
        {
            var code = Open(cmd, asset, out var sm);
            if (code != ExitCode.Success) return code;
            if ((code = FindState(cmd, sm, state, out var s)) != ExitCode.Success) return code;
            if ((code = LoadObject<SceneAsset>(cmd, scene, out var obj)) != ExitCode.Success) return code;

            sm.UndoBeginRecordStateMachine("set_scene");
            try
            {
                var st = s.Settings.Clone();
                var items = st.Features;
                var index = items.FindIndex(f => f is SceneFeature);
                if (obj == null)
                {
                    if (index >= 0) items.RemoveAt(index);
                }
                else
                {
                    var path = PathOf(obj);
                    var feature = new SceneFeature
                    {
                        Scene = new SceneReference { Guid = AssetDatabase.AssetPathToGUID(path), Path = path },
                        LoadMode = loadMode,
                        UnloadOnExit = unloadOnExit,
                    };
                    if (index >= 0) items[index] = feature;
                    else items.Insert(0, feature);
                }
                s.SetSettings(st);
                StateMachineDatabase.SaveStateMachine(sm);
            }
            finally { sm.UndoEndRecordStateMachine(); }
            cmd.Result.Write(new { state = s.StateName, scene = PathOf(obj), loadMode = loadMode.ToString(), unloadOnExit });
            return ExitCode.Success;
        }
    }
}
