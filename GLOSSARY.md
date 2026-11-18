# AppFlow

App-level state machine authored as a Graph Toolkit graph (`.appflow`): each state has a behaviour and a list of Features (e.g. its scene), edges customize how a pair of states transitions.

## Language

**App State**:
A screen-level mode of the app, with a behaviour and Features.
_Avoid_: screen, page

**Feature**:
Reusable lifecycle logic added to an App State from the inspector (an `AppStateFeature` subclass, e.g. Scene, Log). Features wrap the behaviour: they run before it while entering/running and after it while exiting.
_Avoid_: component, override, module

**Entry state**:
The App State the flow starts in; exactly one per graph.

**Transition**:
Moving the app from one App State to another, as Phases ordered by the Default flow or a Custom transition; each Phase group runs Sequential or Parallel. A Custom transition may skip Phases (e.g. resuming a paused state without entering it again).

**Phase**:
One step of entering or leaving a state: PreEnter, Enter, PostEnter, PreExit, Exit, PostExit, plus Running.

**Edge**:
A graph link A→B that can change how that pair transitions. An Edge with no Phase Mode and no Custom transition uses the Default flow.
_Avoid_: connection

**Default flow**:
The phase order used when no Custom transition applies: the Pre, Main and Post Phase groups in order.

**Phase group**:
The matching Phases of the source and target that run as one step: Pre (PreExit, PreEnter), Main (Exit, Enter), Post (PostExit, PostEnter). Sequential runs the source's Phase first; Parallel starts both together.
_Avoid_: crossfade

**Parallel state**:
An App State that flags individual Phase groups (Pre, Main, Post) as Parallel. A Phase group of a Transition runs Parallel when both states flag it, unless the Edge's Phase Mode says otherwise.
_Avoid_: concurrent state

**Phase mode**:
Edge setting that forces each Phase group Sequential or Parallel, or Inherit to follow the states' per-group Parallel flags.

**Custom transition**:
Edge kind whose code orders the phases itself.
_Avoid_: transition script

**Running**:
The phase in which a state is settled and ticked every frame.
