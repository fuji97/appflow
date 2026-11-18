# AppFlow

App-level state machine (Startup, MainMenu, Gameplay, ...) authored as a Graph Toolkit state machine (`.appflow`). Unity package `com.fuji97.appflow`.

## Install

Package Manager > `+` > "Install package from git URL" > `https://github.com/fuji97/appflow.git#v0.1.0`. Requires Unity 6000.7+.

## Use

1. `Assets > Create > App Flow > App Flow Graph`. Add `App State` nodes. Per state, the Graph Inspector shows sections (all stored in one `Settings` option):
   - **Header**: **Name** (unique, used by code and CLI; Graph Toolkit does not persist `State.Title` set from code, so the name is stored in the settings and drives the title), an **Entry** pill (exactly one state) and, in Play mode, the live **Phase** pill of the running state.
   - **Behaviour**: an `AppStateBehaviour` asset, with an "Edit Script" button.
   - **Parallel phases**: four Sequential / Parallel rows (Pre, Enter, Exit, Post). Pre and Post run their two phases together when both states are Parallel there; the Exit/Enter group runs together when the leaving state's **Exit** and the entering state's **Enter** are both Parallel.
   - **Features**: post-processing-style list ("Add Feature" lists every non-abstract `AppStateFeature` subclass in the project; header checkbox enables, `⋮` resets/removes/reorders). Built-in: **Scene** (Scene / LoadMode / UnloadOnExit: loaded in `Enter`, unloaded in `Exit`) and **Log**. Features wrap the behaviour: they run before it in PreEnter/Enter/PostEnter/Running and after it in PreExit/Exit/PostExit, in list order.
2. An edge A→B can customize how that pair transitions. Add a `Custom Transition` condition (an `AppTransitionBehaviour` asset) and/or `Phase Mode` (Pre / Main / Post: Inherit, Sequential or Parallel). An edge with neither uses the default flow.
3. Put an `AppFlowRunner` in the boot scene and assign the `.appflow` asset (its main object is an `AppFlowAsset`).
4. Move by reference: `[SerializeField] AppStateRef next;` (sub-asset of the `.appflow`), then `await AppFlowRunner.Active.Machine.TransitTo(next)`.

## Phases

`AppPhase`: PreEnter, Enter, PostEnter, Running, PreExit, Exit, PostExit. Default flow A→B: `A.PreExit, B.PreEnter, A.Exit, B.Enter, A.PostExit, B.PostEnter`, then `B.Running`. Phase groups: Pre (`A.PreExit`, `B.PreEnter`), Main (`A.Exit`, `B.Enter`), Post (`A.PostExit`, `B.PostEnter`). A group runs Parallel (both started together) when A's Exit and B's Enter are Parallel (Main), or when both states are Parallel for Pre/Post, unless the edge's Phase Mode overrides it; `ctx.ParallelPre/Main/Post` expose the result and `ctx.RunDefault()` honours it. A custom transition receives `AppTransitionContext` (both states) and calls the step helpers (`ctx.PreExit()`, `ctx.Enter()`, `ctx.RunDefault()` ...) in any order. A second `TransitTo` while one runs is rejected (returns false, warning). Hook exceptions are logged and the transition continues.

## Features

A feature is a `[Serializable]` subclass of `AppStateFeature`; it appears in "Add Feature" automatically (one instance per type per state). Its serialized fields are drawn in its foldout; `Machine` and `State` are set at runtime. Each machine gets its own copy, so runtime changes never touch the asset.

```csharp
[Serializable]
public sealed class MusicFeature : AppStateFeature
{
    public AudioClip Clip;
    public override async Awaitable OnEnter(AppTransitionContext ctx) => MusicPlayer.Play(Clip);
}
```

Hooks match `AppStateBehaviour` (`OnPreEnter` … `OnPostExit`, `OnRunning`, `OnRunningUpdate`). Exceptions are logged per feature and do not stop the others. Renaming a feature class loses its data (the inspector shows "Missing feature") unless you add `[UnityEngine.Scripting.APIUpdating.MovedFrom]`.

## CLI (optional)

Requires `com.unity.pipeline`; the `AppFlow.Pipeline*` assemblies compile only when it is installed.

- Play mode / development Player: `unity command appflow status | list_states | transit --state X [--wait false] | history`.
- Edit mode: `unity command appflow.graph describe | create | add_state | connect | set_behaviour | set_scene` (all take `--asset`).

## Play mode graph view

Open the `.appflow` window before or during Play: the active state animates and shows a phase badge; right-click a state > `App Flow/Force enter`.

## UniTask

`AppFlow.UniTask` compiles only when `com.cysharp.unitask` is installed (`TransitToUniTask`). Hooks stay `Awaitable`.

## Samples

Package Manager > AppFlow > Samples > Import "Showcase". Add the imported `Scenes/*.unity` to the Build Profile scene list, open `Scenes/Boot.unity`, press Play. UI Toolkit showcase (Fredoka font, playful style). Flow: Splash > Main menu > Settings / Credits / Game > Pause > back to the menu. A top-right HUD shows the current state, phase and the last transition.

| Edge | Kind | Visible effect |
|---|---|---|
| Splash > MainMenu, MainMenu > Credits, Game > Pause, ... (no edge) | default flow | sequential: the old screen pops out, then the new one pops in |
| MainMenu <> Settings | phase mode Main=Parallel | both screens slide sideways at the same time |
| MainMenu > Game, Pause > MainMenu | custom (`CurtainTransition`) | curtain drops, loading bar fills, curtain lifts |
| Pause > Game | custom (`ResumeTransition`) | only Pause's exit phases run: Game is not re-entered, its scene is not reloaded |

Keys: `Esc` goes back (Game opens Pause, Pause resumes), `F1` toggles the HUD, any key or click skips the splash. The Game score only advances in the `Running` phase, so it freezes by itself while Pause is active. Settings: animation speed (scales every transition), HUD, fullscreen.

## Releasing

Bump `package.json` version, add a CHANGELOG entry, commit, `git tag vX.Y.Z`, `git push origin main vX.Y.Z`, then `gh release create vX.Y.Z --notes-from-tag` (or paste the CHANGELOG section). The tag must equal `v` + the `package.json` version.
