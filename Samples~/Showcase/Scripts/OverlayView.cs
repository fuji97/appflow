using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AppFlow.Sample
{
    /// <summary>Boot-scene overlay: transition curtain with loading bar, and a live HUD of the machine (state, phase, last edge, phase log).</summary>
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public class OverlayView : MonoBehaviour
    {
        const int LogRows = 8;

        [SerializeField] UIDocument m_Document;

        // Set in Awake, cleared in OnDestroy.
        public static OverlayView Instance { get; private set; }

        VisualElement m_Curtain, m_Fill, m_Hud, m_Log;
        Label m_CurtainLabel, m_State, m_Phase, m_Edge;
        AppFlowMachine m_Machine;
        string m_PhaseClass;
        string m_LastEdge = "";

        void Awake() => Instance = this;

        void Start()
        {
            var root = m_Document.rootVisualElement;
            m_Curtain = root.Q("curtain");
            m_CurtainLabel = root.Q<Label>("curtain-label");
            m_Fill = root.Q("curtain-fill");
            m_Hud = root.Q("hud");
            m_State = root.Q<Label>("hud-state");
            m_Phase = root.Q<Label>("hud-phase");
            m_Edge = root.Q<Label>("hud-edge");
            m_Log = root.Q("hud-log");

            // The runner's Awake (same scene) created the machine.
            m_Machine = AppFlowRunner.Active.Machine;
            AppFlowMachine.PhaseChanged += OnPhase;
            m_Machine.TransitionCompleted += OnCompleted;
            SampleSettings.Changed += ApplyHud;
            ApplyHud();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            AppFlowMachine.PhaseChanged -= OnPhase;
            SampleSettings.Changed -= ApplyHud;
            if (m_Machine != null) m_Machine.TransitionCompleted -= OnCompleted;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) SampleSettings.Hud = !SampleSettings.Hud;
            var active = m_Machine.ActiveTransition;
            m_Edge.text = active != null
                ? $"{active.From ?? "start"} > {active.To} | {Kind(active)} | {Time.realtimeSinceStartupAsDouble - active.StartedAt:0.00} s"
                : m_LastEdge;
        }

        static string Kind(AppTransitionRecord r) => r.Custom ? "custom" : r.ParallelPre || r.ParallelMain || r.ParallelPost ? "parallel" : "default flow";

        void ApplyHud() => m_Hud.style.display = SampleSettings.Hud ? DisplayStyle.Flex : DisplayStyle.None;

        void OnPhase(AppFlowMachine machine, AppStateRef state, AppPhase phase)
        {
            if (machine != m_Machine || phase == AppPhase.None) return;
            m_State.text = state.name;
            m_Phase.text = phase.ToString();
            if (m_PhaseClass != null) m_Phase.RemoveFromClassList(m_PhaseClass);
            m_PhaseClass = $"phase--{phase}";
            m_Phase.AddToClassList(m_PhaseClass);

            var row = new Label($"{state.name}.{phase}");
            row.AddToClassList("hud-log-row");
            row.pickingMode = PickingMode.Ignore;
            row.AddToClassList("stagger--off");
            m_Log.Insert(0, row);
            if (m_Log.childCount > LogRows) m_Log.RemoveAt(LogRows);
            row.schedule.Execute(() => row.RemoveFromClassList("stagger--off")).ExecuteLater(16);
        }

        void OnCompleted(AppTransitionRecord r) => m_LastEdge = $"{r.From ?? "start"} > {r.To} | {Kind(r)} | {r.Duration:0.00} s";

        public async Awaitable Close(string label)
        {
            m_CurtainLabel.text = label;
            m_Fill.style.width = Length.Percent(0);
            await UiAnim.Play(m_Curtain, () => m_Curtain.RemoveFromClassList("curtain--up"), 0.5f);
        }

        public async Awaitable FillBar(float seconds)
        {
            var scaled = seconds * SampleSettings.DurationScale;
            for (var t = 0f; t < scaled; t += Time.deltaTime)
            {
                m_Fill.style.width = Length.Percent(100 * t / scaled);
                await Awaitable.NextFrameAsync();
            }
            m_Fill.style.width = Length.Percent(100);
        }

        public Awaitable Open() => UiAnim.Play(m_Curtain, () => m_Curtain.AddToClassList("curtain--up"), 0.4f);
    }
}
