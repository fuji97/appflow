using UnityEngine;
using UnityEngine.InputSystem;

namespace AppFlow.Sample
{
    /// <summary>State behaviour of a UI screen: animates the state's ScreenView and handles auto-advance, any-key and Esc.</summary>
    [CreateAssetMenu(menuName = "App Flow/Sample/Screen State")]
    public class ScreenStateBehaviour : AppStateBehaviour
    {
        [SerializeField] AppStateRef m_Back;
        [SerializeField] AppStateRef m_Next;
        [SerializeField] float m_AutoAdvanceSeconds;
        [SerializeField] int m_Depth;

        float m_Elapsed;

        protected AppStateRef Back => m_Back;
        protected ScreenView View => ScreenView.For(State);

        // A parallel Main group slides the screens sideways; deeper screens live on the right.
        string OffClass(AppTransitionContext ctx, AppStateBehaviour other)
        {
            if (!ctx.ParallelMain || other is not ScreenStateBehaviour o) return "off-pop";
            return m_Depth > o.m_Depth ? "off-right" : "off-left";
        }

        public override async Awaitable OnEnter(AppTransitionContext ctx)
        {
            if (View) await View.Show(OffClass(ctx, ctx.From));
        }

        public override async Awaitable OnExit(AppTransitionContext ctx)
        {
            if (View) await View.Hide(OffClass(ctx, ctx.To));
        }

        public override void OnRunning()
        {
            m_Elapsed = 0;
            if (View) View.OnRunning();
        }

        public override void OnRunningUpdate()
        {
            if (Machine.IsTransitioning) return;
            var dt = Time.deltaTime;
            m_Elapsed += dt;
            if (View) View.Tick(dt);

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (m_Next != null
                && ((m_AutoAdvanceSeconds > 0 && m_Elapsed >= m_AutoAdvanceSeconds)
                    || (kb != null && kb.anyKey.wasPressedThisFrame)
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame)))
                _ = Machine.TransitTo(m_Next);
            else if (m_Back != null && kb != null && kb.escapeKey.wasPressedThisFrame)
                _ = Machine.TransitTo(m_Back);
        }
    }
}
