using System;
using System.Collections.Generic;

namespace AppFlow
{
    [Serializable]
    public sealed class AppTransitionRecord
    {
        public string From;
        public string To;
        public bool Custom;
        public bool ParallelPre, ParallelMain, ParallelPost;
        public double StartedAt;
        public double Duration;
        public int Exceptions;
        public List<AppPhaseTiming> Phases = new();
    }

    [Serializable]
    public struct AppPhaseTiming
    {
        public string State;
        public AppPhase Phase;
        public double Seconds;
    }
}
