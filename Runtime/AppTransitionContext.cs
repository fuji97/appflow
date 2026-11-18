#pragma warning disable CS1998 // helpers are async so a null From is a cheap no-op
using System;
using UnityEngine;

namespace AppFlow
{
    /// <summary>Passed to every phase hook and custom transition. Step helpers run one phase of From or To.</summary>
    public sealed class AppTransitionContext
    {
        readonly AppFlowMachine.StateEntry m_From;
        readonly AppFlowMachine.StateEntry m_To;

        public AppStateBehaviour From => m_From?.Behaviour;
        public AppStateBehaviour To => m_To.Behaviour;
        public AppStateRef FromRef => m_From?.Ref;
        public AppStateRef ToRef => m_To.Ref;
        /// <summary>Resolved for this transition (edge Phase Mode, else both states Parallel); custom transitions may read it.</summary>
        public bool ParallelPre => Record.ParallelPre;
        /// <summary>Resolved for this transition (edge Phase Mode, else both states Parallel); custom transitions may read it.</summary>
        public bool ParallelMain => Record.ParallelMain;
        /// <summary>Resolved for this transition (edge Phase Mode, else both states Parallel); custom transitions may read it.</summary>
        public bool ParallelPost => Record.ParallelPost;
        public AppFlowMachine Machine { get; }

        internal AppTransitionRecord Record { get; }

        internal AppTransitionContext(AppFlowMachine machine, AppFlowMachine.StateEntry from, AppFlowMachine.StateEntry to, AppTransitionRecord record)
        {
            Machine = machine;
            m_From = from;
            m_To = to;
            Record = record;
        }

        public async Awaitable PreExit() { if (m_From != null) await Machine.RunPhase(m_From, AppPhase.PreExit, this); }
        public async Awaitable Exit() { if (m_From != null) await Machine.RunPhase(m_From, AppPhase.Exit, this); }
        public async Awaitable PostExit() { if (m_From != null) await Machine.RunPhase(m_From, AppPhase.PostExit, this); }
        public async Awaitable PreEnter() { await Machine.RunPhase(m_To, AppPhase.PreEnter, this); }
        public async Awaitable Enter() { await Machine.RunPhase(m_To, AppPhase.Enter, this); }
        public async Awaitable PostEnter() { await Machine.RunPhase(m_To, AppPhase.PostEnter, this); }

        /// <summary>Default flow: Pre group (A.PreExit, B.PreEnter), Main (A.Exit, B.Enter), Post (A.PostExit, B.PostEnter). A Parallel group starts both phases together.</summary>
        public async Awaitable RunDefault()
        {
            await Group(PreExit, PreEnter, ParallelPre);
            await Group(Exit, Enter, ParallelMain);
            await Group(PostExit, PostEnter, ParallelPost);
        }

        static async Awaitable Group(Func<Awaitable> exit, Func<Awaitable> enter, bool parallel)
        {
            if (!parallel) { await exit(); await enter(); return; }
            var a = exit();
            var b = enter();
            await a;
            await b;
        }
    }
}
