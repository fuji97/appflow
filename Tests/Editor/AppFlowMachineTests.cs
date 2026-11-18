using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AppFlow.Tests
{
    public class AppFlowMachineTests
    {
        static readonly Hash128 IdA = Hash128.Compute("A");
        static readonly Hash128 IdB = Hash128.Compute("B");

        readonly List<Object> m_Created = new();
        AppFlowMachine m_Machine;

        [SetUp]
        public void SetUp()
        {
            RecordingBehaviour.Log = new List<string>();
            RecordingFeature.Updates = 0;
            RecordingBehaviour.ExitGate = null;
            RecordingBehaviour.ThrowInEnterOf = null;
            RecordingTransition.LastFrom = RecordingTransition.LastTo = null;
        }

        [TearDown]
        public void TearDown()
        {
            m_Machine?.Dispose();
            foreach (var o in m_Created) Object.DestroyImmediate(o);
            m_Created.Clear();
        }

        AppFlowAsset MakeAsset(AppTransitionData edge = null, params string[] extraNames)
        {
            var asset = ScriptableObject.CreateInstance<AppFlowAsset>();
            m_Created.Add(asset);
            asset.name = "TestFlow";
            asset.EntryStateId = IdA;
            AddState(asset, IdA, "A");
            AddState(asset, IdB, "B");
            foreach (var n in extraNames) AddState(asset, Hash128.Compute(n + asset.States.Count), n);
            if (edge != null) asset.Transitions.Add(edge);
            return asset;
        }

        void AddState(AppFlowAsset asset, Hash128 id, string name)
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();
            m_Created.Add(behaviour);
            asset.States.Add(new AppStateData { Id = id, Name = name, Behaviour = behaviour });
        }

        AppFlowMachine Machine(AppFlowAsset asset) => m_Machine = new AppFlowMachine(asset);

        [Test]
        public async Task InitialEntry_RunsEnterPhasesThenRunning()
        {
            var m = Machine(MakeAsset());
            Assert.IsTrue(await m.Start());
            CollectionAssert.AreEqual(new[] { "A.PreEnter", "A.Enter", "A.PostEnter", "A.Running" }, RecordingBehaviour.Log);
        }

        [Test]
        public async Task Features_WrapBehaviour_DisabledSkipped()
        {
            var asset = MakeAsset();
            asset.States[0].Features.Add(new RecordingFeature());
            asset.States[0].Features.Add(null); // renamed/deleted class deserializes as null and must be skipped
            asset.States[0].Features.Add(new RecordingFeature { Enabled = false });
            var m = Machine(asset);
            Assert.IsTrue(await m.Start());
            CollectionAssert.AreEqual(
                new[] { "A.F.PreEnter", "A.PreEnter", "A.F.Enter", "A.Enter", "A.F.PostEnter", "A.PostEnter", "A.F.Running", "A.Running" },
                RecordingBehaviour.Log);
            m.Tick();
            Assert.AreEqual(1, RecordingFeature.Updates);

            RecordingBehaviour.Log.Clear();
            Assert.IsTrue(await m.TransitTo(IdB));
            CollectionAssert.IsSubsetOf(new[] { "A.PreExit", "A.F.PreExit", "A.Exit", "A.F.Exit", "A.PostExit", "A.F.PostExit" }, RecordingBehaviour.Log);
            Assert.Less(RecordingBehaviour.Log.IndexOf("A.PreExit"), RecordingBehaviour.Log.IndexOf("A.F.PreExit"));
            Assert.Less(RecordingBehaviour.Log.IndexOf("A.Exit"), RecordingBehaviour.Log.IndexOf("A.F.Exit"));
            Assert.Less(RecordingBehaviour.Log.IndexOf("A.PostExit"), RecordingBehaviour.Log.IndexOf("A.F.PostExit"));
        }

        [Test]
        public async Task DefaultFlow_IsInterleavedSequential()
        {
            var m = Machine(MakeAsset());
            await m.Start();
            RecordingBehaviour.Log.Clear();
            Assert.IsTrue(await m.TransitTo(IdB));
            CollectionAssert.AreEqual(
                new[] { "A.PreExit", "B.PreEnter", "A.Exit", "B.Enter", "A.PostExit", "B.PostEnter", "B.Running" },
                RecordingBehaviour.Log);
        }

        [Test]
        public async Task PhaseModeEdge_ParallelMain_StartsExitAndEnterTogether()
        {
            var m = Machine(MakeAsset(new AppTransitionData { From = IdA, To = IdB, Modes = new AppPhaseModes { Main = AppPhaseMode.Parallel } }));
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            Assert.Contains("B.Enter", RecordingBehaviour.Log, "B.Enter must start while A.Exit is still pending");
            Assert.IsTrue(m.IsTransitioning);

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            Assert.AreEqual("B.Running", RecordingBehaviour.Log[^1]);
        }

        [Test]
        public async Task BothStatesParallel_RunsGroupsTogether()
        {
            var asset = MakeAsset();
            SetParallel(asset.States[0], true, true, true);
            SetParallel(asset.States[1], true, true, true);
            var m = Machine(asset);
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            Assert.Contains("B.Enter", RecordingBehaviour.Log, "B.Enter must start while A.Exit is still pending");

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            var r = m.History[^1];
            Assert.IsTrue(r.ParallelPre && r.ParallelMain && r.ParallelPost);
        }

        [Test]
        public async Task OneStateParallel_StaysSequential()
        {
            var asset = MakeAsset();
            SetParallel(asset.States[0], true, true, true);
            var m = Machine(asset);
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            CollectionAssert.DoesNotContain(RecordingBehaviour.Log, "B.Enter");

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            CollectionAssert.AreEqual(
                new[] { "A.PreExit", "B.PreEnter", "A.Exit", "B.Enter", "A.PostExit", "B.PostEnter", "B.Running" },
                RecordingBehaviour.Log);
            var r = m.History[^1];
            Assert.IsFalse(r.ParallelPre || r.ParallelMain || r.ParallelPost);
        }

        [Test]
        public async Task StateFlags_ApplyPerGroupAndNeedBothStates()
        {
            var asset = MakeAsset();
            SetParallel(asset.States[0], true, true, false);
            SetParallel(asset.States[1], false, true, true);
            var m = Machine(asset);
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            Assert.Contains("B.Enter", RecordingBehaviour.Log, "Main is flagged on both states");

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            var r = m.History[^1];
            Assert.IsFalse(r.ParallelPre);
            Assert.IsTrue(r.ParallelMain);
            Assert.IsFalse(r.ParallelPost);
        }

        [Test]
        public async Task MainGroup_NeedsLeavingExitAndEnteringEnter()
        {
            var asset = MakeAsset();
            asset.States[0].ParallelExit = true;
            asset.States[1].ParallelEnter = true;
            var m = Machine(asset);
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            Assert.Contains("B.Enter", RecordingBehaviour.Log, "A has Exit parallel and B has Enter parallel");

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            var r = m.History[^1];
            Assert.IsTrue(r.ParallelMain);
            Assert.IsFalse(r.ParallelPre || r.ParallelPost);
        }

        [Test]
        public async Task MainGroup_WrongSidesStaysSequential()
        {
            var asset = MakeAsset();
            asset.States[0].ParallelEnter = true;
            asset.States[1].ParallelExit = true;
            var m = Machine(asset);
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            CollectionAssert.DoesNotContain(RecordingBehaviour.Log, "B.Enter");

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            Assert.IsFalse(m.History[^1].ParallelMain);
        }

        static void SetParallel(AppStateData s, bool pre, bool main, bool post)
        {
            s.ParallelPre = pre;
            s.ParallelEnter = main;
            s.ParallelExit = main;
            s.ParallelPost = post;
        }

        [Test]
        public async Task PhaseModeEdge_SequentialOverridesParallelStates()
        {
            var asset = MakeAsset(new AppTransitionData { From = IdA, To = IdB, Modes = new AppPhaseModes { Main = AppPhaseMode.Sequential } });
            SetParallel(asset.States[0], true, true, true);
            SetParallel(asset.States[1], true, true, true);
            var m = Machine(asset);
            await m.Start();
            RecordingBehaviour.Log.Clear();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();

            var transition = m.TransitTo(IdB);
            CollectionAssert.DoesNotContain(RecordingBehaviour.Log, "B.Enter");

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await transition);
            var r = m.History[^1];
            Assert.IsTrue(r.ParallelPre && r.ParallelPost);
            Assert.IsFalse(r.ParallelMain);
        }

        [Test]
        public async Task CustomTransition_ReplacesDefaultFlow()
        {
            var custom = ScriptableObject.CreateInstance<RecordingTransition>();
            m_Created.Add(custom);
            var m = Machine(MakeAsset(new AppTransitionData { From = IdA, To = IdB, Behaviour = custom }));
            await m.Start();
            RecordingBehaviour.Log.Clear();

            Assert.IsTrue(await m.TransitTo(IdB));

            CollectionAssert.AreEqual(new[] { "custom A->B", "B.Enter", "B.Running" }, RecordingBehaviour.Log);
            Assert.AreEqual("A", RecordingTransition.LastFrom.State.name);
            Assert.AreEqual("B", RecordingTransition.LastTo.State.name);
            Assert.AreSame(RecordingTransition.LastTo, m.CurrentBehaviour);
            Assert.IsTrue(m.History[^1].Custom);
        }

        [Test]
        public async Task TransitWhileBusy_ReturnsFalse()
        {
            var m = Machine(MakeAsset());
            await m.Start();
            RecordingBehaviour.ExitGate = new AwaitableCompletionSource();
            var first = m.TransitTo(IdB);

            LogAssert.Expect(LogType.Warning, new Regex("rejected"));
            Assert.IsFalse(await m.TransitTo(IdA));

            RecordingBehaviour.ExitGate.SetResult();
            Assert.IsTrue(await first);
        }

        [Test]
        public async Task TransitToCurrent_ReEnters()
        {
            var m = Machine(MakeAsset());
            await m.Start();
            RecordingBehaviour.Log.Clear();

            Assert.IsTrue(await m.TransitTo(IdA));

            CollectionAssert.AreEqual(
                new[] { "A.PreExit", "A.PreEnter", "A.Exit", "A.Enter", "A.PostExit", "A.PostEnter", "A.Running" },
                RecordingBehaviour.Log);
        }

        [Test]
        public async Task HookException_ContinuesToTarget()
        {
            var m = Machine(MakeAsset());
            await m.Start();
            RecordingBehaviour.ThrowInEnterOf = "B";
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            Assert.IsTrue(await m.TransitTo(IdB));

            Assert.AreEqual("B", m.CurrentState.name);
            Assert.AreEqual(AppPhase.Running, m.CurrentPhase);
            Assert.AreEqual(1, m.History[^1].Exceptions);
        }

        [Test]
        public void TryFindState_ByNameIdAndDuplicate()
        {
            var asset = MakeAsset(null, "dup", "DUP");
            var m = Machine(asset);

            Assert.IsTrue(m.TryFindState("b", out var s, out _));
            Assert.AreEqual(IdB, s.Id);

            Assert.IsTrue(m.TryFindState(IdA.ToString(), out s, out _));
            Assert.AreEqual("A", s.Name);

            Assert.IsFalse(m.TryFindState("dup", out s, out var error));
            Assert.IsNull(s);
            StringAssert.Contains(asset.States[2].Id.ToString(), error);
            StringAssert.Contains(asset.States[3].Id.ToString(), error);

            Assert.IsFalse(m.TryFindState("Nope", out _, out error));
            StringAssert.Contains("Nope", error);
        }
    }
}
