using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace AppFlow.Editor
{
    [ScriptedImporter(5, AppFlowStateMachine.AssetExtension)]
    public sealed class AppFlowImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var sm = StateMachineDatabase.LoadStateMachineForImporter<AppFlowStateMachine>(ctx.assetPath);
            if (sm == null)
            {
                Debug.LogError($"AppFlow: failed to load {ctx.assetPath}");
                return;
            }

            var asset = ScriptableObject.CreateInstance<AppFlowAsset>();
            asset.name = sm.Name;
            asset.GraphId = sm.ID;

            var states = sm.GetStates().OfType<AppState>().ToList();
            foreach (var state in states)
            {
                var stateRef = ScriptableObject.CreateInstance<AppStateRef>();
                stateRef.Init(state.ID, state.StateName);
                // Keyed by state id so references in scenes/prefabs survive renames and reimports.
                ctx.AddObjectToAsset("state-" + state.ID, stateRef);

                var settings = state.Settings;
                var features = settings.Features?.Where(f => f != null).ToList() ?? new();
                // The GUID survives scene moves/renames; the path the runtime loads is resolved here.
                foreach (var sf in features.OfType<SceneFeature>())
                    sf.Scene.Path = string.IsNullOrEmpty(sf.Scene.Guid) ? "" : AssetDatabase.GUIDToAssetPath(sf.Scene.Guid);
                asset.States.Add(new AppStateData
                {
                    Id = state.ID,
                    Name = state.StateName,
                    Ref = stateRef,
                    Behaviour = settings.Behaviour,
                    ParallelPre = settings.ParallelPre,
                    ParallelEnter = settings.ParallelEnter,
                    ParallelExit = settings.ParallelExit,
                    ParallelPost = settings.ParallelPost,
                    Features = features,
                });
                if (asset.EntryStateId == default && settings.Entry)
                    asset.EntryStateId = state.ID;
            }

            foreach (var state in states)
                foreach (var t in state.GetOutgoingTransitions())
                {
                    var conditions = AppFlowValidation.ConditionsOf(t).ToList();
                    var custom = conditions.OfType<CustomTransitionCondition>().FirstOrDefault();
                    var modes = conditions.OfType<PhaseModeCondition>().FirstOrDefault()?.Value ?? default;
                    if (custom == null && modes.IsInherit) continue;
                    asset.Transitions.Add(new AppTransitionData
                    {
                        Id = t.ID,
                        From = t.FromState.ID,
                        To = t.ToState.ID,
                        Behaviour = custom?.Value,
                        Modes = modes,
                    });
                }

            ctx.AddObjectToAsset("main", asset, AssetDatabase.LoadAssetAtPath<Texture2D>(AppFlowIcons.Graph));
            ctx.SetMainObject(asset);
        }
    }
}
