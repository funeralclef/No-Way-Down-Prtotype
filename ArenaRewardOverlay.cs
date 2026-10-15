using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// A mandatory, two-step post-arena choice: first a run upgrade, then the
/// direction of the next ascent. The world remains visible behind the
/// interface so the choice feels like part of the arena instead of a detached
/// inventory screen.
/// </summary>
public partial class ArenaRewardOverlay : Control
{
    [Signal]
    public delegate void RewardChosenEventHandler(int index);

    [Signal]
    public delegate void RouteChosenEventHandler(int index);

    [Signal]
    public delegate void RerollRequestedEventHandler();

    [Signal]
    public delegate void RunContinuedEventHandler();

    [Signal]
    public delegate void RunFinishedEventHandler();

    private sealed class CardView
    {
        public Button Button;
        public Label Tier;
        public Label Title;
        public Label Description;
        public Label Tags;
        public Label Synergy;
        public TextureRect Icon;
    }

    private enum ChoiceStage
    {
        Finale,
        Reward,
        Route
    }

    private static readonly Color Ink =
        new(0.035f, 0.075f, 0.09f);
    private static readonly Color Paper =
        new(0.92f, 0.97f, 0.97f);
    private static readonly Color Muted =
        new(0.59f, 0.71f, 0.74f);
    private static readonly Color Cyan =
        new(0.12f, 0.76f, 0.9f);
    private static readonly Color Orange =
        new(1.0f, 0.43f, 0.12f);
    private static readonly Color Lime =
        new(0.65f, 0.88f, 0.24f);

    private RunProgression _director;
    private UiAccessibility _accessibility;
    private ColorRect _focusVeil;
    private MarginContainer _safeFrame;
    private VBoxContainer _content;
    private HBoxContainer _metaRow;
    private Label _arenaLabel;
    private Label _tierLabel;
    private Label _heading;
    private Label _subheading;
    private GridContainer _cardsGrid;
    private HBoxContainer _footer;
    private Label _inputHint;
    private Button _rerollButton;
    private Label _stepLabel;
    private readonly List<CardView> _cards = new();
    private ChoiceStage _stage = ChoiceStage.Reward;
    private int _activeCardCount;
    private int _selectedIndex;
    private bool _built;
    private bool _transitioning;
    private bool _rerollUsed;
    private bool _pauseOwned;
    private bool _portrait;
    private Tween _stageTween;

    public bool IsFinaleStageVisible =>
        Visible &&
        _stage == ChoiceStage.Finale;

    public void Initialize(
        RunProgression director)
    {
        _director = director;
        if (IsInsideTree())
        {
            EnsureBuilt();
        }
    }

    public override void _Ready()
    {
        ProcessMode =
            ProcessModeEnum.Always;
        ZIndex = 225;
        MouseFilter =
            MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(
            LayoutPreset.FullRect);
        GamepadInput.EnsureUiMappings();
        EnsureBuilt();
        Hide();
    }

    public override void _ExitTree()
    {
        _stageTween?.Kill();
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -=
                UpdateResponsiveLayout;
        }
        if (_accessibility != null)
        {
            _accessibility.ScaleChanged -=
                OnAccessibilityScaleChanged;
        }
        ReleasePause();
    }

    /// <summary>
    /// Opens the reward choice prepared by RunProgression. This can
    /// be called after every arena; the same overlay instance is reused.
    /// </summary>
    public void ShowRewardStage()
    {
        EnsureBuilt();
        if (_director == null)
        {
            GD.PushWarning(
                "ArenaRewardOverlay requires RunProgression.");
            return;
        }

        _stageTween?.Kill();
        _stage = ChoiceStage.Reward;
        _rerollUsed = false;
        _transitioning = false;
        _selectedIndex = 0;
        Modulate = Colors.White;
        Show();
        OwnPause();
        PopulateRewardStage();
        UpdateResponsiveLayout();
        _transitioning = true;
        AnimateStageReveal();
    }

    /// <summary>
    /// The fourth arena is a real ending, not just another draft. Continuing
    /// is deliberately opt-in and feeds back into the normal reward flow.
    /// </summary>
    public void ShowFinaleStage()
    {
        EnsureBuilt();
        _stageTween?.Kill();
        foreach (var card in _cards) { card.Icon.Hide(); card.Button.TooltipText = ""; }
        _stage = ChoiceStage.Finale;
        _transitioning = false;
        _selectedIndex = 0;
        Modulate = Colors.White;
        Show();
        OwnPause();
        PopulateFinaleStage();
        UpdateResponsiveLayout();
        _transitioning = true;
        AnimateStageReveal();
    }

    public override void _UnhandledInput(
        InputEvent inputEvent)
    {
        if (!Visible || _transitioning)
        {
            return;
        }

        GamepadInput.Observe(inputEvent);
        if (IsRerollInput(inputEvent))
        {
            TryReroll();
            GetViewport().SetInputAsHandled();
            return;
        }

        int direction = 0;
        if (inputEvent.IsActionPressed(
                "ui_left") ||
            (_portrait &&
                inputEvent.IsActionPressed(
                    "ui_up")))
        {
            direction = -1;
        }
        else if (inputEvent.IsActionPressed(
                "ui_right") ||
            (_portrait &&
                inputEvent.IsActionPressed(
                    "ui_down")))
        {
            direction = 1;
        }

        if (direction != 0)
        {
            SelectCard(
                Mathf.PosMod(
                    _selectedIndex + direction,
                    Mathf.Max(1, _activeCardCount)));
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed(
                "ui_accept"))
        {
            ActivateSelectedCard();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        _focusVeil =
            GetNode<ColorRect>(
                "ArenaRewardVeil");
        ConfigureFocusVeil(
            _focusVeil);
        _safeFrame =
            GetNode<MarginContainer>(
                "RewardSafeFrame");
        _content =
            _safeFrame.GetNode<VBoxContainer>(
                "RewardContent");

        BindHeader();
        BindCards();
        BindFooter();
        // Keep full descriptions reachable on short windows and large text.
        _safeFrame.RemoveChild(_content);
        var layout = new VBoxContainer();
        _safeFrame.AddChild(layout);
        var scroll = new ScrollContainer { Name = "RewardScroll", FollowFocus = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(scroll);
        scroll.AddChild(_content);
        _content.RemoveChild(_footer);
        layout.AddChild(_footer);
        _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _content.SizeFlagsVertical = SizeFlags.ExpandFill;

        _accessibility =
            GetNodeOrNull<UiAccessibility>(
                "/root/UiAccessibility");
        _accessibility?.ApplyControlTree(
            _safeFrame);
        if (_accessibility != null)
        {
            _accessibility.ScaleChanged +=
                OnAccessibilityScaleChanged;
        }
        GetViewport().SizeChanged +=
            UpdateResponsiveLayout;
        UpdateResponsiveLayout();
    }

    private void BindHeader()
    {
        _metaRow =
            _content.GetNode<HBoxContainer>(
                "Meta");
        _arenaLabel =
            _metaRow.GetNode<Label>(
                "Arena");
        _tierLabel =
            _metaRow.GetNode<Label>(
                "Tier");
        _heading =
            _content.GetNode<Label>(
                "Heading");
        _subheading =
            _content.GetNode<Label>(
                "Subheading");

        StyleLabel(_arenaLabel, 11, Cyan);
        StyleLabel(_tierLabel, 11, Orange);
        StyleLabel(_heading, 31, Paper);
        StyleLabel(_subheading, 13, Muted);
        foreach (Label label in new[] { _heading, _subheading, _tierLabel })
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.ClipText = false;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        }
    }

    private void BindCards()
    {
        _cardsGrid =
            _content.GetNode<GridContainer>(
                "ChoiceCards");

        for (int index = 0;
            index < 3;
            index++)
        {
            int capturedIndex = index;
            Button button =
                _cardsGrid.GetNode<Button>(
                    $"Choice{index + 1}");
            CardView card =
                BindCard(button);
            card.Button.Pressed +=
                () => ActivateCard(
                    capturedIndex);
            card.Button.FocusEntered +=
                () => _selectedIndex =
                    capturedIndex;
            card.Button.MouseEntered +=
                () =>
                {
                    if (!_transitioning &&
                        capturedIndex <
                            _activeCardCount)
                    {
                        _selectedIndex =
                            capturedIndex;
                    }
                };
            _cards.Add(card);
        }
    }

    private void BindFooter()
    {
        _footer =
            _content.GetNode<HBoxContainer>(
                "Footer");
        _stepLabel =
            _footer.GetNode<Label>(
                "Step");
        _inputHint =
            _footer.GetNode<Label>(
                "InputHint");
        _rerollButton =
            _footer.GetNode<Button>(
                "Reroll");

        StyleLabel(_stepLabel, 10, Muted);
        StyleLabel(_inputHint, 10, Muted);
        StyleUtilityButton(
            _rerollButton);
        _rerollButton.Pressed +=
            TryReroll;
    }

    private void PopulateRewardStage()
    {
        _stage = ChoiceStage.Reward;
        _heading.Text =
            "INSTALL A MODULE";
        _subheading.Text =
            "ONE DEVICE + ITS CATALYST. EVOLUTIONS ARE ASSEMBLED IN YOUR BUILD.";
        _arenaLabel.Text =
            $"ARENA {Mathf.Max(1, _director.ArenaNumber):00} CLEARED";
        string currentTier =
            FriendlyName(
                _director.CurrentRewardTier
                    .ToString());
        _tierLabel.Text =
            $"{currentTier} // " +
            (_director.CanReroll &&
                    !_rerollUsed
                ? "REROLL READY"
                : "COMMIT");
        _tierLabel.AddThemeColorOverride(
            "font_color",
            GetTierColor(currentTier));
        _stepLabel.Text =
            "01  REWARD    02  ROUTE";
        _stepLabel.AddThemeColorOverride(
            "font_color",
            Cyan);

        var offers =
            _director.CurrentRewardOffers;
        _activeCardCount =
            Mathf.Min(3, offers?.Count ?? 0);
        for (int index = 0;
            index < _cards.Count;
            index++)
        {
            bool active =
                index < _activeCardCount;
            CardView card = _cards[index];
            card.Button.Visible = active;
            card.Button.FocusMode = active
                ? FocusModeEnum.All
                : FocusModeEnum.None;
            if (!active)
            {
                continue;
            }

            RunUpgradeDefinition definition =
                offers[index];
            string tier =
                definition.Tier.ToString();
            string title =
                definition.Name;
            string description = RunBuffCatalog.Effect(definition.Id,
                Mathf.Min(definition.MaxRank, _director.GetRank(definition.Id) + 1));
            bool synergy = definition.ParentUpgrade.HasValue || HasBuildAffinity(definition);
            string tags =
                string.Join(
                    " · ",
                    definition.Tags);
            int currentRank =
                _director.GetRank(
                    definition.Id);
            string rank = currentRank >= definition.MaxRank ? "SERVICE · MAX RANK" :
                $"RANK {Mathf.Min(
                    definition.MaxRank,
                    currentRank + 1)}/{definition.MaxRank}";
            RunMergeRecipe recipe = RunBuffCatalog.GetRecipe(definition.Id);
            RunMergeRecipe deviceRecipe = null;
            foreach (var candidate in RunBuffCatalog.Recipes)
                if (candidate.Device == definition.Id) deviceRecipe = candidate;
            string relationship = deviceRecipe != null
                ? "+1 " + RunProgression.GetDefinition(deviceRecipe.Catalyst).Name + " CATALYST · MERGE → " + RunProgression.GetDefinition(deviceRecipe.Evolution).Name
                : recipe != null
                ? "ALSO MERGES: " + RunBuffCatalog.RecipeText(recipe)
                : currentRank > 0 ? "RANK UP · " + RunBuffCatalog.RankPips(definition.Id, currentRank) + " → " + RunBuffCatalog.RankPips(definition.Id, currentRank + 1)
                : synergy ? "MATCHES YOUR BUILD" : "NEW BUFF · ACTIVE FOR THIS RUN";
            Color accent = RunBuffCatalog.Accent(definition.Id);
            card.Icon.Texture = UiIconLibrary.Get(RunBuffCatalog.Icon(definition.Id));
            card.Icon.SelfModulate = accent;
            card.Icon.Show();
            card.Button.TooltipText = definition.Description + "\n" + description + "\n" + relationship;
            PopulateCard(
                card,
                synergy
                    ? $"SYNERGY · {FriendlyName(tier)} · {rank}"
                    : $"{FriendlyName(tier)} · {rank}",
                title,
                description,
                tags,
                relationship,
                accent);
        }

        _cardsGrid.Columns =
            _portrait ? 1 : 3;
        UpdateRerollState();
        SelectCard(0);
    }

    private bool HasBuildAffinity(RunUpgradeDefinition candidate)
    {
        foreach (var owned in RunProgression.Catalog)
        {
            if (!_director.HasUpgrade(owned.Id)) continue;
            foreach (string tag in candidate.Tags)
                foreach (string ownTag in owned.Tags)
                    if (tag == ownTag) return true;
        }
        return false;
    }

    private void PopulateFinaleStage()
    {
        foreach (var card in _cards) { card.Icon.Hide(); card.Button.TooltipText = ""; }
        _stage = ChoiceStage.Finale;
        _heading.Text =
            "ASCENT COMPLETE";
        _subheading.Text =
            "THE FINAL GUARDIAN RESTS. END THE RUN HERE, OR PUSH PAST THE SUMMIT.";
        _arenaLabel.Text =
            "FINAL ARENA CLEARED";
        _tierLabel.Text =
            "RUN COMPLETE // CONTINUATION OPTIONAL";
        _tierLabel.AddThemeColorOverride(
            "font_color",
            Orange);
        _stepLabel.Text =
            "FINAL  •  DECISION";
        _stepLabel.AddThemeColorOverride(
            "font_color",
            Orange);

        _activeCardCount = 2;
        for (int index = 0;
            index < _cards.Count;
            index++)
        {
            CardView card = _cards[index];
            bool active =
                index < _activeCardCount;
            card.Button.Visible = active;
            card.Button.FocusMode = active
                ? FocusModeEnum.All
                : FocusModeEnum.None;
            if (!active)
            {
                continue;
            }

            if (index == 0)
            {
                PopulateCard(
                    card,
                    "ASCENT SECURED",
                    "RETURN TO REFUGE",
                    "BANK THIS RUN AND RETURN WITH THE FOURTH ARENA RECORDED AS YOUR FINISH.",
                    "FINISH · SAVE · BREATHE",
                    "THE ASCENT HAS A TRUE END",
                    Cyan);
            }
            else
            {
                PopulateCard(
                    card,
                    "OPTIONAL OVERRUN",
                    "CLIMB BEYOND",
                    "KEEP THE BUILD AND ENTER AN ENDLESS ASCENT THAT TIGHTENS AFTER EVERY ARENA.",
                    "RISING PRESSURE · LONGER WAVES",
                    "DIFFICULTY GROWS, NEVER SPIKES",
                    Orange);
            }
        }

        _cardsGrid.Columns =
            _portrait ? 1 : 2;
        _rerollButton.Hide();
        SelectCard(0);
        UpdateResponsiveLayout();
    }

    private void PopulateRouteStage()
    {
        foreach (var card in _cards) { card.Icon.Hide(); card.Button.TooltipText = ""; }
        _stage = ChoiceStage.Route;
        _heading.Text =
            "SHAPE THE NEXT ASCENT";
        _subheading.Text =
            "THE WAY STILL LEADS UP. YOU CHOOSE ITS RHYTHM.";
        _tierLabel.Text =
            "ROUTE DIRECTIVE // TWO SIGNALS";
        _tierLabel.AddThemeColorOverride(
            "font_color",
            Cyan);
        _stepLabel.Text =
            "01  REWARD    02  ROUTE";
        _stepLabel.AddThemeColorOverride(
            "font_color",
            Orange);

        var routes =
            _director.CurrentRouteOffers;
        _activeCardCount =
            Mathf.Min(2, routes?.Count ?? 0);
        for (int index = 0;
            index < _cards.Count;
            index++)
        {
            bool active =
                index < _activeCardCount;
            CardView card = _cards[index];
            card.Button.Visible = active;
            card.Button.FocusMode = active
                ? FocusModeEnum.All
                : FocusModeEnum.None;
            if (!active)
            {
                continue;
            }

            string routeName =
                FriendlyName(
                    routes[index]
                        .ToString());
            GetRouteCopy(
                routeName,
                out string description,
                out string tags,
                out string affinity,
                out Color accent);
            PopulateCard(
                card,
                "NEXT CHAPTER",
                routeName,
                description,
                tags,
                affinity,
                accent);
        }

        _cardsGrid.Columns =
            _portrait ? 1 : 2;
        _rerollButton.Hide();
        _selectedIndex = 0;
        SelectCard(0);
        UpdateResponsiveLayout();
    }

    private void PopulateCard(
        CardView card,
        string tier,
        string title,
        string description,
        string tags,
        string synergy,
        Color accent)
    {
        card.Tier.Text =
            tier.ToUpperInvariant();
        card.Title.Text =
            title.ToUpperInvariant();
        card.Description.Text =
            description;
        card.Tags.Text =
            tags.ToUpperInvariant();
        card.Synergy.Text =
            synergy.ToUpperInvariant();
        card.Synergy.Visible =
            !string.IsNullOrWhiteSpace(
                synergy);
        card.Tier.AddThemeColorOverride(
            "font_color",
            accent);
        card.Tags.AddThemeColorOverride(
            "font_color",
            accent.Lerp(Paper, 0.22f));
        card.Synergy.AddThemeColorOverride(
            "font_color",
            Lime);
        ApplyCardAccent(
            card.Button,
            accent);
    }

    private void ActivateSelectedCard()
    {
        ActivateCard(_selectedIndex);
    }

    private void ActivateCard(int index)
    {
        if (_transitioning ||
            index < 0 ||
            index >= _activeCardCount)
        {
            return;
        }

        _selectedIndex = index;
        if (_stage == ChoiceStage.Finale)
        {
            if (index == 0)
            {
                FinishFinaleSequence();
            }
            else
            {
                EmitSignal(
                    SignalName.RunContinued);
                TransitionFromFinaleToReward();
            }
            return;
        }

        if (_stage == ChoiceStage.Reward)
        {
            _director.SelectReward(index);
            EmitSignal(
                SignalName.RewardChosen,
                index);
            TransitionToRouteStage();
            return;
        }

        _director.SelectRoute(index);
        EmitSignal(
            SignalName.RouteChosen,
            index);
        FinishSequence();
    }

    private void TransitionFromFinaleToReward()
    {
        _transitioning = true;
        _rerollUsed = false;
        _stageTween?.Kill();
        _stageTween = CreateTween();
        _stageTween.SetPauseMode(
            Tween.TweenPauseMode.Process);
        _stageTween.TweenProperty(
                _content,
                "modulate:a",
                0.0f,
                0.16f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    PopulateRewardStage();
                    _content.Modulate =
                        new Color(
                            1.0f,
                            1.0f,
                            1.0f,
                            0.0f);
                }));
        _stageTween.TweenProperty(
                _content,
                "modulate:a",
                1.0f,
                0.26f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.Out);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    _transitioning = false;
                    FocusSelectedCard();
                }));
    }

    private void FinishFinaleSequence()
    {
        _transitioning = true;
        _stageTween?.Kill();
        _stageTween = CreateTween();
        _stageTween.SetPauseMode(
            Tween.TweenPauseMode.Process);
        _stageTween.SetParallel(true);
        _stageTween.TweenProperty(
                this,
                "modulate:a",
                0.0f,
                0.3f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.TweenProperty(
                _content,
                "scale",
                new Vector2(0.985f, 0.985f),
                0.3f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.SetParallel(false);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    Hide();
                    Modulate = Colors.White;
                    _content.Scale = Vector2.One;
                    _transitioning = false;
                    ReleasePause();
                    EmitSignal(
                        SignalName.RunFinished);
                }));
    }

    private void TransitionToRouteStage()
    {
        _transitioning = true;
        _stageTween?.Kill();
        _stageTween = CreateTween();
        _stageTween.SetPauseMode(
            Tween.TweenPauseMode.Process);
        _stageTween.TweenProperty(
                _content,
                "modulate:a",
                0.0f,
                0.14f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    PopulateRouteStage();
                    _content.Modulate =
                        new Color(
                            1.0f,
                            1.0f,
                            1.0f,
                            0.0f);
                }));
        _stageTween.TweenProperty(
                _content,
                "modulate:a",
                1.0f,
                0.24f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.Out);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    _transitioning = false;
                    FocusSelectedCard();
                }));
    }

    private void FinishSequence()
    {
        _transitioning = true;
        _stageTween?.Kill();
        _stageTween = CreateTween();
        _stageTween.SetPauseMode(
            Tween.TweenPauseMode.Process);
        _stageTween.SetParallel(true);
        _stageTween.TweenProperty(
                this,
                "modulate:a",
                0.0f,
                0.24f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.TweenProperty(
                _content,
                "scale",
                new Vector2(0.985f, 0.985f),
                0.24f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.SetParallel(false);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    Hide();
                    Modulate = Colors.White;
                    _content.Scale = Vector2.One;
                    _transitioning = false;
                    ReleasePause();
                }));
    }

    private void TryReroll()
    {
        if (_transitioning ||
            _stage != ChoiceStage.Reward ||
            _rerollUsed ||
            !_director.CanReroll)
        {
            return;
        }

        EmitSignal(
            SignalName.RerollRequested);
        if (!_director.RerollRewards())
        {
            UpdateRerollState();
            return;
        }

        _rerollUsed = true;
        _transitioning = true;
        _stageTween?.Kill();
        _stageTween = CreateTween();
        _stageTween.SetPauseMode(
            Tween.TweenPauseMode.Process);
        _stageTween.TweenProperty(
                _cardsGrid,
                "modulate:a",
                0.0f,
                0.12f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.In);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    PopulateRewardStage();
                    _cardsGrid.Modulate =
                        new Color(
                            1.0f,
                            1.0f,
                            1.0f,
                            0.0f);
                }));
        _stageTween.TweenProperty(
                _cardsGrid,
                "modulate:a",
                1.0f,
                0.2f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.Out);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    _transitioning = false;
                    FocusSelectedCard();
                }));
    }

    private void SelectCard(int index)
    {
        if (_activeCardCount <= 0)
        {
            return;
        }

        _selectedIndex = Mathf.Clamp(
            index,
            0,
            _activeCardCount - 1);
        FocusSelectedCard();
    }

    private void FocusSelectedCard()
    {
        if (!Visible ||
            _transitioning ||
            _selectedIndex < 0 ||
            _selectedIndex >=
                _activeCardCount)
        {
            return;
        }
        _cards[_selectedIndex]
            .Button
            .GrabFocus();
    }

    private void UpdateRerollState()
    {
        bool canReroll =
            _stage == ChoiceStage.Reward &&
            !_rerollUsed &&
            _director.CanReroll;
        _rerollButton.Show();
        _rerollButton.Disabled =
            !canReroll;
        bool touch =
            _accessibility?.IsTouchLayout ==
                true;
        _rerollButton.Text = canReroll
            ? touch
                ? "REROLL"
                : "REROLL  [R / X]"
            : "REROLL SPENT";
    }

    private void AnimateStageReveal()
    {
        _stageTween?.Kill();
        _focusVeil.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
        _content.Modulate =
            new Color(
                1.0f,
                1.0f,
                1.0f,
                0.0f);
        _content.Scale =
            new Vector2(
                0.985f,
                0.985f);
        _content.PivotOffset =
            _content.Size * 0.5f;
        _stageTween =
            CreateTween();
        _stageTween.SetPauseMode(
            Tween.TweenPauseMode.Process);
        _stageTween.SetParallel(true);
        _stageTween.TweenProperty(
                _focusVeil,
                "modulate:a",
                1.0f,
                0.2f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.Out);
        _stageTween.TweenProperty(
                _content,
                "modulate:a",
                1.0f,
                0.28f)
            .SetDelay(0.06f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.Out);
        _stageTween.TweenProperty(
                _content,
                "scale",
                Vector2.One,
                0.32f)
            .SetDelay(0.04f)
            .SetTrans(
                Tween.TransitionType.Cubic)
            .SetEase(
                Tween.EaseType.Out);
        _stageTween.SetParallel(false);
        _stageTween.TweenCallback(
            Callable.From(
                () =>
                {
                    _transitioning = false;
                    FocusSelectedCard();
                }));
    }

    private void UpdateResponsiveLayout()
    {
        if (!_built ||
            !IsInstanceValid(_safeFrame))
        {
            return;
        }

        Vector2 viewport =
            GetViewport()
                .GetVisibleRect()
                .Size;
        Vector4 safe =
            _accessibility
                ?.GetSafeInsets(viewport) ??
            Vector4.Zero;
        float layoutScale =
            _accessibility?.LayoutScale ??
            1.0f;
        float safeWidth = Mathf.Max(
            240.0f,
            viewport.X - safe.X - safe.Z);
        float safeHeight = Mathf.Max(
            260.0f,
            viewport.Y - safe.Y - safe.W);
        _portrait =
            safeHeight > safeWidth * 1.04f;
        bool compact =
            !_portrait &&
            safeHeight < 520.0f;
        bool compactPortrait =
            _portrait &&
            safeHeight < 700.0f;
        float outer =
            (_portrait ? 14.0f : 22.0f) *
            layoutScale;

        Place(
            _safeFrame,
            safe.X + outer,
            safe.Y + outer,
            Mathf.Max(
                200.0f,
                safeWidth - outer * 2.0f),
            Mathf.Max(
                220.0f,
                safeHeight - outer * 2.0f));

        _content.AddThemeConstantOverride(
            "separation",
            Mathf.RoundToInt(
                (compact ? 6.0f : 10.0f) *
                layoutScale));
        _cardsGrid.AddThemeConstantOverride(
            "h_separation",
            Mathf.RoundToInt(
                12.0f * layoutScale));
        _cardsGrid.AddThemeConstantOverride(
            "v_separation",
            Mathf.RoundToInt(
                8.0f * layoutScale));
        _cardsGrid.Columns = _portrait
            ? 1
            : _stage == ChoiceStage.Reward
                ? 3
                : 2;

        _subheading.Visible =
            !compact &&
            !compactPortrait;
        _inputHint.Text =
            _accessibility?.IsTouchLayout ==
                true
                ? _portrait
                    ? "TAP A CARD TO CHOOSE"
                    : "TAP TO CHOOSE"
                : _portrait
                    ? "↑  ↓  SELECT     A / ENTER  CONFIRM"
                    : "←  →  SELECT     A / ENTER  CONFIRM";
        _stepLabel.Visible =
            !compact &&
            (!_portrait ||
                safeWidth >= 380.0f);

        foreach (CardView card in _cards)
        {
            card.Button.CustomMinimumSize = new Vector2(0, 340 * layoutScale);
            card.Title.MaxLinesVisible = 2;
            card.Description.MaxLinesVisible = -1;
            card.Tags.MaxLinesVisible = -1;
            card.Tags.Visible = true;
            card.Synergy.MaxLinesVisible = -1;
            card.Synergy.Visible = !string.IsNullOrWhiteSpace(card.Synergy.Text);
        }

        _content.PivotOffset =
            _content.Size * 0.5f;
    }

    private void OnAccessibilityScaleChanged(
        float uiScale,
        float textScale)
    {
        _accessibility?.ApplyControlTree(
            _safeFrame);
        UpdateResponsiveLayout();
    }

    private void OwnPause()
    {
        if (GetTree().Paused)
        {
            _pauseOwned = false;
            return;
        }
        GetTree().Paused = true;
        _pauseOwned = true;
    }

    private void ReleasePause()
    {
        if (!_pauseOwned ||
            GetTree() == null)
        {
            return;
        }
        GetTree().Paused = false;
        _pauseOwned = false;
    }

    private static bool IsRerollInput(
        InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey key &&
            key.Pressed &&
            !key.Echo)
        {
            return key.Keycode == Key.R ||
                key.PhysicalKeycode == Key.R;
        }
        return inputEvent is
            InputEventJoypadButton button &&
            button.Pressed &&
            button.ButtonIndex == JoyButton.X;
    }

    private static CardView BindCard(
        Button button)
    {
        VBoxContainer content =
            button.GetNode<VBoxContainer>(
                "Margin/Content");
        TextureRect icon = UiIconLibrary.Create(UiIconLibrary.Loadout, 42, Cyan);
        icon.Name = "BuffIcon";
        content.AddChild(icon);
        content.MoveChild(icon, 0);
        icon.Hide();
        Label tier =
            content.GetNode<Label>(
                "Tier");
        Label title =
            content.GetNode<Label>(
                "Title");
        Label description =
            content.GetNode<Label>(
                "Description");
        Label tags =
            content.GetNode<Label>(
                "Tags");
        Label synergy =
            content.GetNode<Label>(
                "Synergy");

        StyleLabel(tier, 10, Cyan);
        StyleLabel(title, 22, Paper);
        StyleLabel(description, 13, Muted);
        StyleLabel(tags, 10, Cyan);
        StyleLabel(synergy, 10, Lime);
        ApplyCardAccent(button, Cyan);
        return new CardView
        {
            Button = button,
            Tier = tier,
            Title = title,
            Description = description,
            Tags = tags,
            Synergy = synergy,
            Icon = icon
        };
    }

    private static void ApplyCardAccent(
        Button button,
        Color accent)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            CardStyle(
                new Color(
                    0.02f,
                    0.055f,
                    0.068f,
                    0.86f),
                new Color(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.25f),
                1));
        button.AddThemeStyleboxOverride(
            "hover",
            CardStyle(
                new Color(
                    0.035f,
                    0.09f,
                    0.105f,
                    0.94f),
                new Color(
                    accent.R,
                    accent.G,
                    accent.B,
                    0.76f),
                2));
        button.AddThemeStyleboxOverride(
            "pressed",
            CardStyle(
                new Color(
                    accent.R * 0.1f,
                    accent.G * 0.1f,
                    accent.B * 0.1f,
                    0.98f),
                accent,
                3));
        button.AddThemeStyleboxOverride(
            "focus",
            CardStyle(
                new Color(
                    0.035f,
                    0.09f,
                    0.105f,
                    0.96f),
                accent,
                3));
        button.AddThemeStyleboxOverride(
            "disabled",
            CardStyle(
                new Color(
                    0.02f,
                    0.035f,
                    0.04f,
                    0.68f),
                new Color(
                    0.3f,
                    0.38f,
                    0.4f,
                    0.2f),
                1));
    }

    private static StyleBoxFlat CardStyle(
        Color background,
        Color border,
        int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            AntiAliasing = true
        };
    }

    private static void StyleUtilityButton(
        Button button)
    {
        button.AddThemeFontSizeOverride(
            "font_size",
            11);
        button.AddThemeColorOverride(
            "font_color",
            Paper);
        button.AddThemeColorOverride(
            "font_disabled_color",
            new Color(
                Muted.R,
                Muted.G,
                Muted.B,
                0.58f));
        button.AddThemeStyleboxOverride(
            "normal",
            UtilityStyle(
                new Color(
                    0.02f,
                    0.06f,
                    0.07f,
                    0.8f),
                new Color(
                    Cyan.R,
                    Cyan.G,
                    Cyan.B,
                    0.44f)));
        button.AddThemeStyleboxOverride(
            "hover",
            UtilityStyle(
                new Color(
                    0.04f,
                    0.1f,
                    0.12f,
                    0.94f),
                Cyan));
        button.AddThemeStyleboxOverride(
            "pressed",
            UtilityStyle(
                new Color(
                    0.11f,
                    0.065f,
                    0.025f,
                    0.98f),
                Orange));
        button.AddThemeStyleboxOverride(
            "disabled",
            UtilityStyle(
                new Color(
                    0.02f,
                    0.04f,
                    0.045f,
                    0.58f),
                new Color(
                    0.3f,
                    0.38f,
                    0.4f,
                    0.18f)));
    }

    private static StyleBoxFlat UtilityStyle(
        Color background,
        Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 14.0f,
            ContentMarginRight = 14.0f,
            ContentMarginTop = 8.0f,
            ContentMarginBottom = 8.0f
        };
    }

    private static void StyleLabel(
        Label label,
        int fontSize,
        Color color)
    {
        label.AddThemeFontSizeOverride(
            "font_size",
            fontSize);
        label.AddThemeColorOverride(
            "font_color",
            color);
    }

    private static void ConfigureFocusVeil(
        ColorRect veil)
    {
        Shader shader = new()
        {
            Code = @"
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture
    : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec3 scene = textureLod(
        screen_texture,
        SCREEN_UV,
        2.6).rgb;
    float centre = 1.0 - smoothstep(
        0.08,
        0.82,
        length((UV - vec2(0.5)) * vec2(0.78, 1.0)));
    float edge = smoothstep(
        0.18,
        0.98,
        length((UV - vec2(0.5)) * vec2(0.74, 1.0)));
    vec3 filtered = mix(
        scene,
        vec3(0.004, 0.016, 0.021),
        0.78 + edge * 0.1);
    filtered += vec3(0.005, 0.026, 0.033) * centre;
    COLOR = vec4(filtered, 0.92);
}"
        };
        veil.Color = Colors.White;
        veil.Material =
            new ShaderMaterial
            {
                Shader = shader
            };
    }

    private static void GetRouteCopy(
        string routeName,
        out string description,
        out string tags,
        out string affinity,
        out Color accent)
    {
        string normalized =
            routeName.ToUpperInvariant();
        if (normalized.Contains("HUNT"))
        {
            description =
                "DENSER VERTICAL TARGETS AND LONGER COMBO CHAINS. PRESSURE RISES WITH THE PAYOFF.";
            tags =
                "COMBO · ENEMY DENSITY · SCORE";
            affinity =
                "FAVORS ROUTE AND STOMP BUILDS";
            accent = Orange;
            return;
        }
        if (normalized.Contains("ENDURE"))
        {
            description =
                "FEWER TARGETS, SHARPER THREATS AND MORE SPACE FOR BRAVE DODGES AND RECOVERY.";
            tags =
                "SURVIVAL · DODGE · RECOVERY";
            affinity =
                "FAVORS RISK AND HEALTH BUILDS";
            accent = Lime;
            return;
        }

        description =
            "LONGER SPEED LINES, WIDER SPACING AND MORE ROOM TO TURN NITRO INTO MOMENTUM.";
        tags =
            "SPEED · NITRO · TRAVERSAL";
        affinity =
            "FAVORS THRUST AND MOVEMENT BUILDS";
        accent = Cyan;
    }

    private static Color GetTierColor(
        string tier)
    {
        string normalized =
            tier.ToUpperInvariant();
        if (normalized.Contains(
                "EVOLUTION"))
        {
            return new Color("#bd9dff");
        }
        if (normalized.Contains(
                "DEVICE"))
        {
            return Orange;
        }
        return Cyan;
    }

    private static object ReadMember(
        object source,
        params string[] names)
    {
        if (source == null)
        {
            return null;
        }
        Type type = source.GetType();
        foreach (string name in names)
        {
            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.IgnoreCase);
            if (property != null)
            {
                return property.GetValue(source);
            }
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase);
            if (field != null)
            {
                return field.GetValue(source);
            }
        }
        return null;
    }

    private static string ReadString(
        object source,
        string fallback,
        params string[] names)
    {
        object value =
            ReadMember(source, names);
        string text = value?.ToString();
        return string.IsNullOrWhiteSpace(text)
            ? fallback
            : text;
    }

    private static bool ReadBool(
        object source,
        params string[] names)
    {
        object value =
            ReadMember(source, names);
        return value is bool flag && flag;
    }

    private static string FormatTags(
        object value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        if (value is string text)
        {
            return text;
        }
        if (value is not IEnumerable values)
        {
            return FriendlyName(
                value.ToString());
        }

        List<string> tags = new();
        foreach (object tag in values)
        {
            if (tag == null)
            {
                continue;
            }
            string formatted =
                FriendlyName(
                    tag.ToString());
            if (!string.IsNullOrWhiteSpace(
                    formatted))
            {
                tags.Add(formatted);
            }
        }
        return string.Join(" · ", tags);
    }

    private static string FriendlyName(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        List<char> characters = new();
        char previous = '\0';
        foreach (char character in raw.Trim())
        {
            if (character == '_' ||
                character == '-')
            {
                if (characters.Count > 0 &&
                    characters[^1] != ' ')
                {
                    characters.Add(' ');
                }
                previous = character;
                continue;
            }
            if (char.IsUpper(character) &&
                characters.Count > 0 &&
                previous != ' ' &&
                !char.IsUpper(previous))
            {
                characters.Add(' ');
            }
            characters.Add(character);
            previous = character;
        }
        return new string(characters.ToArray())
            .ToUpperInvariant();
    }

    private static void Place(
        Control control,
        float x,
        float y,
        float width,
        float height)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = x;
        control.OffsetTop = y;
        control.OffsetRight = x + width;
        control.OffsetBottom = y + height;
    }
}
