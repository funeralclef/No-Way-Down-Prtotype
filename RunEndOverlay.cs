using Godot;

/// <summary>
/// Owns end-of-run presentation, responsive layout and action intent.
/// Gameplay shutdown and scene transitions remain in Main.
/// </summary>
[GlobalClass]
public partial class RunEndOverlay : ColorRect
{
    [Signal]
    public delegate void RetryRequestedEventHandler();

    [Signal]
    public delegate void RefugeRequestedEventHandler();

    private Control _card;
    private Label _subtitle;
    private Label _scoreValue;
    private Label _bestValue;
    private Control _recordBadge;
    private Label _hint;
    private Button _retryButton;
    private Button _refugeButton;
    private MobileControls _mobileControls;
    private UiAccessibility _accessibility;
    private Tween _revealTween;

    public override void _Ready()
    {
        _card = GetNode<Control>("Card");
        _subtitle = GetNode<Label>(
            "Card/Margin/Content/Subtitle");
        _scoreValue = GetNode<Label>(
            "Card/Margin/Content/Results/Score/Value");
        _bestValue = GetNode<Label>(
            "Card/Margin/Content/Results/Best/Value");
        _recordBadge = GetNode<Control>(
            "Card/Margin/Content/RecordBadge");
        _hint = GetNode<Label>(
            "Card/Margin/Content/Hint");
        HBoxContainer actions = GetNode<HBoxContainer>(
            "Card/Margin/Content/Actions");
        _retryButton = actions.GetNode<Button>("RetryRun");
        _refugeButton = actions.GetNode<Button>("ReturnToRefuge");
        _retryButton.Pressed += RequestRetry;
        _refugeButton.Pressed += RequestRefuge;

        _accessibility = GetNodeOrNull<UiAccessibility>(
            "/root/UiAccessibility");
        if (_accessibility != null)
        {
            _accessibility.ApplyControlTree(_card);
            _accessibility.ScaleChanged += OnScaleChanged;
        }
        GetViewport().SizeChanged += OnViewportSizeChanged;
        UpdateInputHint();
        Callable.From(ApplyResponsiveLayout).CallDeferred();
    }

    public override void _ExitTree()
    {
        _revealTween?.Kill();
        if (IsInstanceValid(_accessibility))
        {
            _accessibility.ScaleChanged -= OnScaleChanged;
        }
        if (IsInstanceValid(GetViewport()))
        {
            GetViewport().SizeChanged -= OnViewportSizeChanged;
        }
    }

    public void Initialize(MobileControls mobileControls)
    {
        _mobileControls = mobileControls;
        UpdateInputHint();
    }

    public void ShowResults(
        int score,
        int bestScore,
        bool isNewRecord)
    {
        _subtitle.Text = isNewRecord
            ? "THAT WAS A GREAT RUN"
            : "THE CREEPS GOT YOU";
        _scoreValue.Text = score.ToString();
        _bestValue.Text = bestScore.ToString();
        _recordBadge.Visible = isNewRecord;

        ApplyResponsiveLayout();
        Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _card.PivotOffset = _card.Size * 0.5f;
        _card.Modulate =
            new Color(1.0f, 1.0f, 1.0f, 0.0f);
        _card.Scale = new Vector2(0.975f, 0.955f);
        Show();
        UpdateInputHint();
        if (!IsInstanceValid(_mobileControls) ||
            !_mobileControls.IsTouchMode)
        {
            _retryButton.GrabFocus();
        }

        _revealTween?.Kill();
        _revealTween = CreateTween().SetParallel(true);
        _revealTween.TweenProperty(
                this,
                "modulate:a",
                1.0f,
                0.34f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _revealTween.TweenProperty(
                _card,
                "modulate",
                Colors.White,
                0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _revealTween.TweenProperty(
                _card,
                "scale",
                Vector2.One,
                0.42f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    public bool HandleInput(InputEvent inputEvent)
    {
        UpdateInputHint();
        if (!Visible)
        {
            return false;
        }

        if (inputEvent.IsActionPressed("ui_accept"))
        {
            GetViewport().SetInputAsHandled();
            if (GetViewport().GuiGetFocusOwner() == _refugeButton)
            {
                RequestRefuge();
            }
            else
            {
                RequestRetry();
            }
            return true;
        }

        if (inputEvent.IsActionPressed("ui_cancel") ||
            inputEvent.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            RequestRefuge();
            return true;
        }

        return false;
    }

    private void RequestRetry()
    {
        EmitSignal(SignalName.RetryRequested);
    }

    private void RequestRefuge()
    {
        EmitSignal(SignalName.RefugeRequested);
    }

    private void UpdateInputHint()
    {
        if (!IsInstanceValid(_hint))
        {
            return;
        }

        if (IsInstanceValid(_mobileControls) &&
            _mobileControls.IsTouchMode)
        {
            _hint.Text = "CHOOSE RETRY OR RETURN TO REFUGE";
        }
        else if (GamepadInput.ShouldShowGamepadPrompts)
        {
            _hint.Text =
                "D-PAD: CHOOSE  •  A / CROSS: CONFIRM  •  B / CIRCLE: REFUGE";
        }
        else
        {
            _hint.Text =
                "←/→: CHOOSE  •  ENTER: CONFIRM  •  ESC: REFUGE";
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsInstanceValid(_card))
        {
            return;
        }

        Vector2 viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 1.0f ||
            viewportSize.Y <= 1.0f)
        {
            viewportSize = GetViewport().GetVisibleRect().Size;
        }
        Vector4 safeInsets =
            _accessibility?.GetSafeInsets(viewportSize) ??
            Vector4.Zero;
        float layoutScale =
            _accessibility?.LayoutScale ?? 1.0f;
        float safeWidth = Mathf.Max(
            1.0f,
            viewportSize.X - safeInsets.X - safeInsets.Z);
        float safeHeight = Mathf.Max(
            1.0f,
            viewportSize.Y - safeInsets.Y - safeInsets.W);
        float halfWidth = Mathf.Max(
            96.0f,
            Mathf.Min(
                210.0f * layoutScale,
                safeWidth * 0.5f - 14.0f * layoutScale));
        float halfHeight = Mathf.Max(
            96.0f,
            Mathf.Min(
                162.0f * layoutScale,
                safeHeight * 0.5f - 12.0f * layoutScale));
        float centerOffsetX =
            (safeInsets.X - safeInsets.Z) * 0.5f;
        float centerOffsetY =
            (safeInsets.Y - safeInsets.W) * 0.5f;

        _card.AnchorLeft = 0.5f;
        _card.AnchorTop = 0.5f;
        _card.AnchorRight = 0.5f;
        _card.AnchorBottom = 0.5f;
        _card.OffsetLeft = centerOffsetX - halfWidth;
        _card.OffsetTop = centerOffsetY - halfHeight;
        _card.OffsetRight = centerOffsetX + halfWidth;
        _card.OffsetBottom = centerOffsetY + halfHeight;
        _card.PivotOffset = _card.Size * 0.5f;
    }

    private void OnScaleChanged(
        float uiScale,
        float textScale)
    {
        _accessibility?.ApplyControlTree(_card);
        Callable.From(ApplyResponsiveLayout).CallDeferred();
    }

    private void OnViewportSizeChanged()
    {
        Callable.From(ApplyResponsiveLayout).CallDeferred();
    }
}
