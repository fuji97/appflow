namespace AppFlow
{
    public enum AppPhaseMode { Inherit, Sequential, Parallel }

    /// <summary>Per Phase group override set on an Edge; Inherit follows the states' Parallel flags.</summary>
    [System.Serializable]
    public struct AppPhaseModes
    {
        public AppPhaseMode Pre, Main, Post;
        public bool IsInherit => Pre == AppPhaseMode.Inherit && Main == AppPhaseMode.Inherit && Post == AppPhaseMode.Inherit;
    }
}
