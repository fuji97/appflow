using System;
using UnityEngine;

namespace AppFlow
{
    [Serializable]
    public sealed class AppTransitionData
    {
        public Hash128 Id;
        public Hash128 From;
        public Hash128 To;
        public AppTransitionBehaviour Behaviour;
        public AppPhaseModes Modes;
    }
}
