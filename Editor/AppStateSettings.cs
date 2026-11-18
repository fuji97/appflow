using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppFlow.Editor
{
    /// <summary>Graph option value holding all settings of an App State (one type so one PropertyDrawer owns the inspector).</summary>
    [Serializable]
    public sealed class AppStateSettings
    {
        [Tooltip("State name used by code and CLI; shown as the state title")] public string Name = "";
        [Tooltip("Initial state entered on Start")] public bool Entry;
        [Tooltip("Lifecycle hooks of this state")] public AppStateBehaviour Behaviour;
        public bool ParallelPre, ParallelEnter, ParallelExit, ParallelPost;
        [SerializeReference] public List<AppStateFeature> Features = new();

        /// <summary>Copy for TrySetValue: options only register a change when given a new instance.</summary>
        public AppStateSettings Clone()
        {
            var c = (AppStateSettings)MemberwiseClone();
            c.Features = new List<AppStateFeature>(Features ?? new List<AppStateFeature>());
            return c;
        }
    }
}
