using System;
using Unity.GraphToolkit.Editor;

namespace AppFlow.Editor
{
    [Serializable]
    [Condition("Phase Mode")]
    public sealed class PhaseModeCondition : Condition<AppPhaseModes>
    {
        public override string Tooltip => "Forces each phase group (Pre, Main, Post) Sequential or Parallel; Inherit follows the states' Parallel flags.";
    }
}
