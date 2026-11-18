# Changelog

All notable changes to this package are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/).

## [Unreleased]

### Added

- State **Features**: post-processing-style list on App States; any `AppStateFeature` subclass appears in "Add Feature". Built-in `SceneFeature` and `LogFeature`.
- Per-state **Parallel** flags (Pre, Enter, Exit, Post) and per-edge **Phase Mode** condition (Pre/Main/Post: Inherit, Sequential, Parallel). Pre/Post run Parallel when both states flag them; the Exit/Enter group when the leaving state has Exit and the entering state has Enter Parallel.
- Custom App State inspector: Name/Entry header, Behaviour (Edit Script), Parallel phases (2x2 Sequential/Parallel chip rows), Features, live Phase badge in Play mode.

### Changed

- **Breaking:** the Scene / LoadMode / UnloadOnExit state options moved into `SceneFeature`; `AppStateData.ScenePath/LoadMode/UnloadOnExit` were removed. Graphs saved with 0.1.0 lose their scene settings when opened; re-add them as a Scene feature (or `unity command appflow.graph set_scene`).
- **Breaking:** App State options merged into one `Settings` option (`AppStateSettings`); `AppStateFeatures` removed. Graphs saved before lose their state settings; re-author them.
- **Breaking:** the `Crossfade` edge condition, `AppTransitionData.Crossfade`, `AppTransitionRecord.Crossfade`, `AppTransitionContext.Crossfade` and `RunDefault(bool)` were removed; use Phase Mode with Main = Parallel (`ctx.ParallelMain`, `RunDefault()`). CLI `connect --crossfade` became `--pre/--main/--post`.

## [0.1.0] - 2026-11-13

### Added

- Initial release: AppFlow runtime, Graph Toolkit editor, optional UniTask and Unity Pipeline CLI integrations, Showcase sample.
