using Godot;
using System.Collections.Generic;

/// <summary>Input-aware field manual with separate control and build pages.</summary>
public partial class GameInfoPanel : GameMenuPanel
{
    private bool _systems;
    private bool _controller;
    public bool RefugeContext { get; set; }
    protected override string PanelTitle => RefugeContext ? "REFUGE GUIDE" : "FIELD MANUAL";
    protected override string PanelSubtitle => "RUNNER SUPPORT  /  QUICK REFERENCE";

    protected override void Rebuild()
    {
        ClearContent();
        var tabs = new HBoxContainer();
        Content.AddChild(tabs);
        AddTab(tabs, "CONTROLS", !_systems, () => SelectPage(false));
        AddTab(tabs, "RUN SYSTEMS", _systems, () => SelectPage(true));
        if (_systems) { BuildSystems(); return; }
        var devices = new HBoxContainer();
        Content.AddChild(devices);
        AddTab(devices, "KEYBOARD", !_controller, () => SelectDevice(false));
        AddTab(devices, "CONTROLLER", _controller, () => SelectDevice(true));
        if (RefugeContext) { BuildRefugeControls(); return; }
        Content.AddChild(BuffUi.Label("Keep moving. Land on enemies. Carry your momentum into the next jump.", 14, BuffUi.Muted));
        AddMovement();
        AddAction("jump", "JUMP / DOUBLE JUMP", "Press again in the air to extend your arc.");
        AddAction("dash", "RIFT / NITRO", _controller ? "RB / R1 triggers a rift. Use RT / R2 for reactor thrust." : "Tap for a short rift. Hold to engage the nitro reactor.");
        if (_controller) AddAction("nitro_throttle", "REACTOR THRUST", "Control acceleration with the trigger.");
        AddAction("module_skill", "EQUIPPED DEVICE SKILL", "Install a device, choose EQUIP SKILL in BUILD, then cast. Failed casts spend no nitro. The button shows readiness and cooldown.");
        AddAction("pause", "PAUSE", "Open settings, inspect your build or return to the refuge.");
        Content.AddChild(BuffUi.Label("TOUCH   On-screen stick to steer · Jump to leap · Rift / Nitro buttons for movement skills.", 12, BuffUi.Muted));
    }

    private void BuildRefugeControls()
    {
        Content.AddChild(BuffUi.Label("Set your appearance, explore your build and choose when to begin.", 14, BuffUi.Muted));
        AddControlRow(_controller ? "LS / D-PAD" : "← / →", "CHANGE STATION", "Browse Wardrobe, Refuge, Loadout and Descent.");
        AddControlRow(_controller ? "LB / RB" : "↑ / ↓", "PREVIEW STYLE", "Preview an appearance or room style before applying it.");
        AddControlRow(_controller ? "A / ×" : "ENTER", "CONFIRM", "Apply a selection, open the module archive or start a run.");
        AddControlRow(_controller ? "RIGHT STICK" : "DRAG", "ROTATE CHARACTER", "Inspect the character while the Wardrobe station is selected.");
        AddControlRow(_controller ? "Y / △" : "H", "HIDE / SHOW INTERFACE", "Inspect the room without overlays. Press again to restore, or use Escape / B.");
        AddControlRow(_controller ? "START" : "ESC", "SETTINGS", "Open settings while the interface is visible. Escape also restores a hidden interface.");
        AddControlRow(_controller ? "X / □" : "F1", "FIELD GUIDE", "Open this guide. Back / Escape closes it and returns to the same station.");
        Content.AddChild(BuffUi.Label("TOUCH   Tap a station arrow to navigate. Drag the character to rotate it. The eye icon always remains available to restore the interface.", 12, BuffUi.Muted));
    }

    private void SelectPage(bool systems)
    {
        _systems = systems; RefreshContent();
        Content.GetChild<HBoxContainer>(0).GetChild<Button>(systems ? 1 : 0).GrabFocus();
    }
    private void SelectDevice(bool controller)
    {
        _controller = controller; RefreshContent();
        Content.GetChild<HBoxContainer>(1).GetChild<Button>(controller ? 1 : 0).GrabFocus();
    }

    private void BuildSystems()
    {
        AddLesson("01", "MOVE WITH INTENT", "Stomp enemies from above to sustain a combo. Use the next target as a stepping stone; nitro carries speed between encounters.");
        AddLesson("02", "CLEAR → INSTALL → CONTINUE", "Each arena grants one device and one matching catalyst. Devices add behavior such as landing pulses, stored dodge energy or a reactor wake. There is no separate stat-boost choice.");
        AddLesson("03", "ASSEMBLE AN EVOLUTION", "Open BUILD. Combine an installed device with two matching catalyst ranks. The catalyst is consumed; the device stays installed. Ready recipes appear first. Evolutions are made through merging.");
        AddLesson("04", "CHOOSE YOUR ROUTE", "HUNT favors enemy chains. FLOW favors traversal. ENDURE gives space for dodges and recovery. Your equipment lasts until the end of the run.");
        AddLesson("05", "THE REFUGE", "Left / right to change stations. Up / down to preview styles. Enter to apply or start. Open the module archive in LOADOUT. F1 / controller X opens this manual. H / controller Y hides the interface; Escape restores it.");
        AddLesson("06", "READ THE WORLD", "Biomes have distinct silhouettes and weather. The playable path stays readable: scenery and weather do not add hidden collisions.");
        AddLesson("07", "FOLLOW THE FLOW", "Teal chevrons mark helpful routes. Wind arches sustain speed, bloom pads turn landings into launches, and gentle bridge ramps lead to an exit charge. Take the side route or stay on the open center lane.");
        AddLesson("09", "DEVICE SKILLS", "E / controller X casts the device equipped in BUILD. Air Step blinks along your steering direction. Fault Dive plunges from the air. Hunt Spark launches a drone. Thread Recall plants an anchor and returns to it. Glass Heart grows a visible guard; Closed Loop makes friendly Echo Steps. Evolutions change these actions, not just their numbers.");
        AddLesson("08", "THE FINAL GUARDIAN", "Guardians reposition and vary attacks with distance and your movement. Bellwarden mixes moving rings with marked slams. Thornmaw alternates rushes and thorn marks; bait a rush into a resonator. Prismweaver changes sides while its crystals orbit. Watch the locked warning, use the bloom pads and stomp the open crown. At half health, patterns gain a second beat.");
    }

    private void AddLesson(string index, string title, string detail)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", BuffUi.Surface(BuffUi.Accent));
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 7);
        panel.AddChild(column); Content.AddChild(panel);
        column.AddChild(BuffUi.Label(index + "  /  " + title, 16, BuffUi.Accent));
        column.AddChild(BuffUi.Label(detail, 14, BuffUi.Muted));
    }

    private static void AddTab(HBoxContainer parent, string title, bool selected, System.Action action)
    {
        var button = BuffUi.Button(title); button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (selected) button.AddThemeStyleboxOverride("normal", BuffUi.Surface(BuffUi.Accent, true));
        button.Pressed += () => action(); parent.AddChild(button);
    }

    private void AddAction(string action, string title, string detail)
    {
        if (!InputMap.HasAction(action)) return;
        var bindings = new List<string>();
        foreach (InputEvent input in InputMap.ActionGetEvents(action))
        {
            bool joy = input is InputEventJoypadButton or InputEventJoypadMotion;
            if (joy != _controller) continue;
            string name = BindingName(input);
            if (!bindings.Contains(name)) bindings.Add(name);
        }
        AddControlRow(bindings.Count > 0 ? string.Join(" / ", bindings) : "—", title, detail);
    }

    private void AddMovement()
    {
        var keys = new List<string>();
        foreach (string action in new[] { "move_forward", "move_left", "move_back", "move_right" })
            foreach (InputEvent input in InputMap.ActionGetEvents(action))
            {
                bool joy = input is InputEventJoypadButton or InputEventJoypadMotion;
                if (joy != _controller) continue;
                string name = input is InputEventJoypadMotion axis && axis.Axis is JoyAxis.LeftX or JoyAxis.LeftY
                    ? "LEFT STICK" : BindingName(input);
                if (!keys.Contains(name)) keys.Add(name);
            }
        AddControlRow(keys.Count > 0 ? string.Join(" · ", keys) : "—", "MOVE / STEER",
            "Guide your landing and line up the next stomp.");
    }

    private void AddControlRow(string keys, string title, string detail)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", BuffUi.Surface(BuffUi.Muted));
        Content.AddChild(panel);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); panel.AddChild(row);
        var cap = new PanelContainer { CustomMinimumSize = new Vector2(86, 44) };
        cap.AddThemeStyleboxOverride("panel", BuffUi.Surface(BuffUi.Accent, true));
        var binding = BuffUi.Label(keys, 13, BuffUi.Accent);
        binding.HorizontalAlignment = HorizontalAlignment.Center; cap.AddChild(binding); row.AddChild(cap);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(column);
        column.AddChild(BuffUi.Label(title, 14)); column.AddChild(BuffUi.Label(detail, 12, BuffUi.Muted));
    }

    private static string BindingName(InputEvent input) => input switch
    {
        InputEventKey key => OS.GetKeycodeString(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode),
        InputEventJoypadMotion axis => axis.Axis switch
        {
            JoyAxis.LeftX => axis.AxisValue < 0 ? "LS ←" : "LS →",
            JoyAxis.LeftY => axis.AxisValue < 0 ? "LS ↑" : "LS ↓",
            JoyAxis.TriggerRight => "RT / R2", JoyAxis.TriggerLeft => "LT / L2", _ => "STICK"
        },
        InputEventJoypadButton button => button.ButtonIndex switch
        {
            JoyButton.A => "A / ×", JoyButton.B => "B / ○", JoyButton.X => "X / □", JoyButton.Y => "Y / △",
            JoyButton.Start => "START", JoyButton.Back => "VIEW", JoyButton.LeftShoulder => "LB / L1", JoyButton.RightShoulder => "RB / R1",
            JoyButton.DpadUp => "↑", JoyButton.DpadDown => "↓", JoyButton.DpadLeft => "←", JoyButton.DpadRight => "→", _ => button.ButtonIndex.ToString()
        },
        InputEventMouseButton mouse => "MOUSE " + mouse.ButtonIndex, _ => input.AsText()
    };
}
