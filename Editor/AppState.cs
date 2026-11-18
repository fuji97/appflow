using System;
using Unity.GraphToolkit.Editor;

namespace AppFlow.Editor
{
    [Serializable]
    [State("App Flow", AppFlowIcons.State, "App State")]
    public sealed class AppState : State
    {
        public const string OptSettings = "Settings";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<AppStateSettings>(OptSettings).WithTooltip("State settings").Build();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            // State.Title set from code is not persisted by Graph Toolkit, so the name lives in the Settings option.
            var name = Settings.Name;
            if (!string.IsNullOrEmpty(name)) Title = name;
        }

        /// <summary>The Settings name, else the title typed in the graph.</summary>
        public string StateName
        {
            get
            {
                var name = Settings.Name;
                return string.IsNullOrEmpty(name) ? Title : name;
            }
        }

        public static string NameOf(IState state) => state is AppState a ? a.StateName : state.Title;

        /// <summary>Stored settings, or a fresh default when the option was never set. Mutate a Clone() and pass it to SetSettings.</summary>
        public AppStateSettings Settings
        {
            get { AppStateSettings v = null; GetOptionByName(OptSettings)?.TryGetValue(out v); return v ?? new AppStateSettings(); }
        }

        public void SetSettings(AppStateSettings value) => GetOptionByName(OptSettings).TrySetValue(value);
    }
}
