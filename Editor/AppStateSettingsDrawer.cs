using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AppFlow.Editor
{
    /// <summary>Sectioned inspector for an App State: header (name, entry, live phase), Behaviour, Parallel phases and a Volume-style Features list.</summary>
    [CustomPropertyDrawer(typeof(AppStateSettings))]
    public sealed class AppStateSettingsDrawer : PropertyDrawer
    {
        // Translucent so both editor skins read correctly.
        static Color Separator => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.1f) : new Color(0f, 0f, 0f, 0.2f);
        static Color HeaderBg => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.05f) : new Color(0f, 0f, 0f, 0.06f);
        static readonly Color EntryBlue = new(0.23f, 0.47f, 0.85f);

        static string DisplayName(Type t) => ObjectNames.NicifyVariableName(t.Name).Replace(" Feature", "");

        static VisualElement Section(VisualElement header)
        {
            header.style.backgroundColor = HeaderBg;
            header.style.borderTopWidth = 1;
            header.style.borderTopColor = Separator;
            header.style.marginLeft = 0;
            header.style.marginRight = 0;
            header.style.marginTop = 0;
            header.style.marginBottom = 0;
            header.style.paddingLeft = 2;
            header.style.minHeight = 22;
            header.style.alignItems = Align.Center;
            return header;
        }

        static Foldout SectionFoldout(string title)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.contentContainer.style.paddingTop = 2;
            foldout.contentContainer.style.paddingBottom = 4;
            foldout.contentContainer.style.paddingRight = 4;
            var header = Section(foldout.Q<Toggle>());
            header.Q<Label>().style.unityFontStyleAndWeight = FontStyle.Bold;
            return foldout;
        }

        static Button MenuButton(Action<Rect> open)
        {
            Button button = null;
            button = new Button(() => open(button.worldBound))
            {
                // Same icon as the component header menu in the Inspector.
                iconImage = Background.FromTexture2D((Texture2D)EditorGUIUtility.IconContent("_Menu").image),
                tooltip = "Feature options",
                style =
                {
                    marginLeft = StyleKeyword.Auto, marginRight = 2, width = 18, height = 18,
                    paddingLeft = 1, paddingRight = 1, paddingTop = 1, paddingBottom = 1,
                    backgroundColor = Color.clear, borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                },
            };
            return button;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var items = property.FindPropertyRelative(nameof(AppStateSettings.Features));
            if (items == null)
            {
                // The option value came back null: seed it so the fields have something to bind to.
                property.boxedValue = new AppStateSettings();
                property.serializedObject.ApplyModifiedProperties();
                items = property.FindPropertyRelative(nameof(AppStateSettings.Features));
            }

            var nameProp = property.FindPropertyRelative(nameof(AppStateSettings.Name));
            var entryProp = property.FindPropertyRelative(nameof(AppStateSettings.Entry));
            var behaviourProp = property.FindPropertyRelative(nameof(AppStateSettings.Behaviour));

            var root = new VisualElement();
            // Graph Toolkit lays the option label and value out in a row; stack them so headers span the inspector like the Volume editor,
            // and drop its "Settings" label. Done on layout: the field's classes and parent are not final yet when the drawer attaches.
            root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                for (var e = root.parent; e != null; e = e.parent)
                    if (e.ClassListContains("ge-model-property-field"))
                    {
                        e.style.flexDirection = FlexDirection.Column;
                        e.style.alignItems = Align.Stretch;
                        var l = e.Q<Label>();
                        if (l != null && !root.Contains(l)) l.style.display = DisplayStyle.None;
                        break;
                    }
            });

            // ---- header: name, live phase, entry ----
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            header.Add(new TextField
            {
                bindingPath = nameProp.propertyPath,
                isDelayed = true,
                tooltip = "State name used by code and CLI; shown as the state title",
                style = { flexGrow = 1, fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 0, marginRight = 0 },
            });
            var phasePill = new Label { name = "appflow-phase", pickingMode = PickingMode.Ignore };
            var ps = phasePill.style;
            ps.unityFontStyleAndWeight = FontStyle.Bold;
            ps.color = Color.white;
            ps.backgroundColor = (Color)new Color32(0x2E, 0x7D, 0x32, 0xFF);
            ps.borderTopLeftRadius = ps.borderTopRightRadius = ps.borderBottomLeftRadius = ps.borderBottomRightRadius = 4;
            ps.paddingLeft = ps.paddingRight = 6;
            ps.marginLeft = 4;
            ps.display = DisplayStyle.None;
            header.Add(phasePill);
            var entry = new Toggle { text = "Entry", bindingPath = entryProp.propertyPath, tooltip = "Initial state entered on Start" };
            var es = entry.style;
            es.marginLeft = 4;
            es.marginRight = 0;
            es.borderTopLeftRadius = es.borderTopRightRadius = es.borderBottomLeftRadius = es.borderBottomRightRadius = 10;
            es.borderTopWidth = es.borderBottomWidth = es.borderLeftWidth = es.borderRightWidth = 1;
            es.paddingTop = es.paddingBottom = 2;
            es.paddingLeft = es.paddingRight = 8;
            header.Add(entry);
            root.Add(header);

            void RefreshEntry()
            {
                var on = entryProp.boolValue;
                entry.style.backgroundColor = on ? new Color(EntryBlue.r, EntryBlue.g, EntryBlue.b, 0.35f) : Color.clear;
                entry.style.borderTopColor = entry.style.borderBottomColor = entry.style.borderLeftColor = entry.style.borderRightColor = on ? EntryBlue : Separator;
            }
            RefreshEntry();
            root.TrackPropertyValue(property.FindPropertyRelative(nameof(AppStateSettings.Entry)), _ => RefreshEntry());

            // ---- behaviour ----
            var targetPath = AssetDatabase.GetAssetPath(property.serializedObject.targetObject);
            var createDir = targetPath == "" ? "Assets" : System.IO.Path.GetDirectoryName(targetPath).Replace('\\', '/');
            var behaviour = SectionFoldout("Behaviour");
            var behaviourRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            behaviourRow.Add(new PropertyField(behaviourProp, "") { style = { flexGrow = 1 } });
            var editScript = new Button(() =>
            {
                if (behaviourProp.objectReferenceValue is ScriptableObject so && so != null)
                    AssetDatabase.OpenAsset(MonoScript.FromScriptableObject(so));
            }) { text = "Edit Script", style = { marginRight = 0 } };
            behaviourRow.Add(editScript);
            var createButton = new Button { text = "Create", tooltip = "Create a behaviour asset or a new behaviour script", style = { marginRight = 0 } };
            behaviourRow.Add(createButton);
            behaviour.Add(behaviourRow);
            root.Add(behaviour);

            void CreateAsset(Type t)
            {
                var path = EditorUtility.SaveFilePanelInProject("Create App State Behaviour", $"{nameProp.stringValue}{t.Name}", "asset", "Choose where to save the behaviour asset", createDir);
                if (path == "") return;
                var so = ScriptableObject.CreateInstance(t);
                AssetDatabase.CreateAsset(so, path);
                AssetDatabase.SaveAssets();
                behaviourProp.objectReferenceValue = so;
                property.serializedObject.ApplyModifiedProperties();
            }

            void CreateScript()
            {
                var path = EditorUtility.SaveFilePanelInProject("New App State Behaviour Script", $"{nameProp.stringValue}StateBehaviour", "cs", "Choose where to save the script", createDir);
                if (path == "") return;
                var className = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!System.Text.RegularExpressions.Regex.IsMatch(className, "^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    EditorUtility.DisplayDialog("Invalid class name", $"'{className}' is not a valid C# class name.", "OK");
                    return;
                }
                // No namespace: compiles into whichever assembly owns the chosen folder.
                System.IO.File.WriteAllText(path,
                    "#pragma warning disable CS1998 // hooks are async; await what you need\n" +
                    "using AppFlow;\n" +
                    "using UnityEngine;\n\n" +
                    $"public class {className} : AppStateBehaviour\n" +
                    "{\n" +
                    "    public override async Awaitable OnEnter(AppTransitionContext ctx) { }\n\n" +
                    "    public override async Awaitable OnExit(AppTransitionContext ctx) { }\n" +
                    "}\n");
                AssetDatabase.ImportAsset(path);
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(path));
            }

            createButton.clicked += () =>
            {
                var types = TypeCache.GetTypesDerivedFrom<AppStateBehaviour>()
                    .Where(t => !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.Name).ToList();
                var menu = new GenericMenu();
                if (types.Count == 0) menu.AddDisabledItem(new GUIContent("No AppStateBehaviour types found"));
                foreach (var t in types)
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(t.Name)), false, () => CreateAsset(t));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("New Script…"), false, CreateScript);
                menu.DropDown(createButton.worldBound);
            };

            void RefreshBehaviourButtons()
            {
                var has = behaviourProp.objectReferenceValue != null;
                editScript.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                createButton.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            }
            RefreshBehaviourButtons();
            root.TrackPropertyValue(property.FindPropertyRelative(nameof(AppStateSettings.Behaviour)), _ => RefreshBehaviourButtons());

            // ---- parallel phases ----
            var parallel = SectionFoldout("Parallel phases");
            var labels = new[] { "Pre", "Enter", "Exit", "Post" };
            var fields = new[] { nameof(AppStateSettings.ParallelPre), nameof(AppStateSettings.ParallelEnter), nameof(AppStateSettings.ParallelExit), nameof(AppStateSettings.ParallelPost) };
            var tips = new[]
            {
                "PreExit/PreEnter run together when both states have Pre Parallel",
                "Enter runs together with the previous state's Exit when that state has Exit Parallel",
                "Exit runs together with the next state's Enter when that state has Enter Parallel",
                "PostExit/PostEnter run together when both states have Post Parallel",
            };
            var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            for (var row = 0; row < labels.Length; row++)
            {
                var index = row;
                var cell = new VisualElement { style = { width = Length.Percent(50), flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingRight = 6, marginBottom = 2 } };
                cell.Add(new Label(labels[index]) { tooltip = tips[index], style = { width = 44, unityTextAlign = TextAnchor.MiddleLeft } });
                var group = new ToggleButtonGroup { isMultipleSelection = false, allowEmptySelection = false, tooltip = tips[index], style = { flexGrow = 1, marginLeft = 0, marginRight = 0 } };
                group.Add(new Button { text = "Sequential", style = { flexGrow = 1 } });
                group.Add(new Button { text = "Parallel", style = { flexGrow = 1 } });
                cell.Add(group);
                grid.Add(cell);

                void Refresh()
                {
                    var state = new ToggleButtonGroupState(0, 2);
                    state[property.FindPropertyRelative(fields[index]).boolValue ? 1 : 0] = true;
                    group.SetValueWithoutNotify(state);
                }
                Refresh();
                group.RegisterValueChangedCallback(e =>
                {
                    property.FindPropertyRelative(fields[index]).boolValue = e.newValue[1];
                    property.serializedObject.ApplyModifiedProperties();
                    Refresh(); // an empty selection counts as Sequential
                });
                root.TrackPropertyValue(property.FindPropertyRelative(fields[index]), _ => Refresh());
            }
            parallel.Add(grid);
            root.Add(parallel);

            // ---- features ----
            var features = SectionFoldout("Features");
            var list = new VisualElement { style = { marginTop = 2, borderBottomWidth = 1, borderBottomColor = Separator } };
            features.Add(list);
            var addButton = new Button { text = "Add Feature", style = { height = 22, marginLeft = 0, marginRight = 0, marginTop = 6 } };
            features.Add(addButton);
            root.Add(features);

            void Commit()
            {
                property.serializedObject.ApplyModifiedProperties();
                Rebuild();
            }

            void ShowMenu(int index, Rect at)
            {
                var el = items.GetArrayElementAtIndex(index);
                var type = el.managedReferenceValue?.GetType();
                var menu = new GenericMenu();
                if (type != null)
                    menu.AddItem(new GUIContent("Reset"), false, () => { el.managedReferenceValue = Activator.CreateInstance(type); Commit(); });
                else
                    menu.AddDisabledItem(new GUIContent("Reset"));
                menu.AddItem(new GUIContent("Remove"), false, () => { items.DeleteArrayElementAtIndex(index); Commit(); });
                menu.AddSeparator("");
                if (index > 0) menu.AddItem(new GUIContent("Move Up"), false, () => { items.MoveArrayElement(index, index - 1); Commit(); });
                else menu.AddDisabledItem(new GUIContent("Move Up"));
                if (index < items.arraySize - 1) menu.AddItem(new GUIContent("Move Down"), false, () => { items.MoveArrayElement(index, index + 1); Commit(); });
                else menu.AddDisabledItem(new GUIContent("Move Down"));
                menu.DropDown(at);
            }

            void Rebuild()
            {
                list.Clear();
                for (var i = 0; i < items.arraySize; i++)
                {
                    var index = i;
                    var el = items.GetArrayElementAtIndex(i);
                    var menuButton = MenuButton(at => ShowMenu(index, at));
                    if (el.managedReferenceValue == null)
                    {
                        var row = Section(new VisualElement { style = { flexDirection = FlexDirection.Row } });
                        row.Add(new Label("Missing feature (class renamed or deleted)")
                        {
                            style = { paddingLeft = 16, unityFontStyleAndWeight = FontStyle.Italic, color = new Color(1f, 0.75f, 0.2f) },
                        });
                        row.Add(menuButton);
                        list.Add(row);
                        continue;
                    }

                    var foldout = SectionFoldout(DisplayName(el.managedReferenceValue.GetType()));
                    var featureHeader = foldout.Q<Toggle>();
                    var title = featureHeader.Q<Label>();
                    // Volume-style header: arrow, enable checkbox, title, options menu.
                    var enabled = new Toggle { bindingPath = el.FindPropertyRelative(nameof(AppStateFeature.Enabled)).propertyPath, tooltip = "Enabled", style = { marginLeft = 0, marginRight = 4 } };
                    enabled.RegisterValueChangedCallback(e =>
                    {
                        foldout.contentContainer.SetEnabled(e.newValue);
                        title.SetEnabled(e.newValue);
                    });
                    title.parent.Insert(title.parent.IndexOf(title), enabled);
                    featureHeader.Add(menuButton);

                    var end = el.GetEndProperty();
                    var child = el.Copy();
                    var depth = el.depth + 1;
                    for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
                    {
                        if (child.depth != depth || child.name == nameof(AppStateFeature.Enabled)) continue;
                        foldout.Add(new PropertyField(child.Copy()));
                    }
                    list.Add(foldout);
                }
                list.style.display = items.arraySize > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                list.Bind(property.serializedObject);
            }

            addButton.clicked += () =>
            {
                var used = Enumerable.Range(0, items.arraySize)
                    .Select(i => items.GetArrayElementAtIndex(i).managedReferenceValue?.GetType())
                    .Where(t => t != null).ToHashSet();
                var types = TypeCache.GetTypesDerivedFrom<AppStateFeature>()
                    .Where(t => !t.IsAbstract && !t.IsGenericType).OrderBy(t => t.Name).ToList();
                var menu = new GenericMenu();
                if (types.Count == 0) menu.AddDisabledItem(new GUIContent("No AppStateFeature types found"));
                foreach (var t in types)
                {
                    var label = new GUIContent(DisplayName(t));
                    if (used.Contains(t)) { menu.AddDisabledItem(label); continue; }
                    menu.AddItem(label, false, () =>
                    {
                        items.arraySize++;
                        items.GetArrayElementAtIndex(items.arraySize - 1).managedReferenceValue = Activator.CreateInstance(t);
                        Commit();
                    });
                }
                menu.DropDown(addButton.worldBound);
            };

            Rebuild();
            root.Bind(property.serializedObject);

            // ---- live phase (Play mode), mirrors AppStateView ----
            bool Matches(AppFlowMachine machine, AppStateRef stateRef)
            {
                if (stateRef == null || stateRef.name != nameProp.stringValue) return false;
                // shortcut: name-only match when the inspector target is not an asset; two open graphs with equal state names both light up.
                return targetPath == "" || AssetDatabase.GetAssetPath(machine.Asset) == targetPath;
            }

            void Show(AppPhase phase)
            {
                phasePill.text = phase.ToString().ToUpperInvariant();
                phasePill.style.display = DisplayStyle.Flex;
            }

            void Hide() => phasePill.style.display = DisplayStyle.None;

            void OnPhase(AppFlowMachine machine, AppStateRef stateRef, AppPhase phase)
            {
                if (Matches(machine, stateRef))
                {
                    if (phase == AppPhase.None) Hide(); else Show(phase);
                }
                else if (phase == AppPhase.Running)
                {
                    Hide(); // another state took over
                }
            }

            void OnPlayMode(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.ExitingPlayMode) Hide();
            }

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                AppFlowMachine.PhaseChanged += OnPhase;
                EditorApplication.playModeStateChanged += OnPlayMode;
                var machine = AppFlowRunner.Active != null ? AppFlowRunner.Active.Machine : null;
                if (machine != null && Matches(machine, machine.CurrentState) && machine.CurrentPhase != AppPhase.None)
                    Show(machine.CurrentPhase);
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                AppFlowMachine.PhaseChanged -= OnPhase;
                EditorApplication.playModeStateChanged -= OnPlayMode;
            });
            return root;
        }
    }
}
