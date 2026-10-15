using Godot;

public partial class ScoreLabel : PanelContainer
{
    private const string SavePath = "user://score.cfg";
    private const string SaveSection = "score";
    private const string BestScoreKey = "best";
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public bool IsNewRecord { get; private set; }

    private Label _scoreValue;
    private Label _scoreTitle;
    private Label _bestValue;
    private Tween _scoreTween;
    private Color _targetColor = Colors.White;
    private Color _displayColor = Colors.White;
    private float _targetMomentumWeight;
    private float _momentumWeight;
    private bool _bestScoreSavePending;

    public override void _Ready()
    {
        _scoreValue = GetNode<Label>("Margin/Values/Score/Value");
        _scoreTitle = GetNode<Label>(
            "Margin/Values/Score/Title");
        _bestValue = GetNode<Label>("Margin/Values/Best/Value");
        GetNode<Control>("Margin/Values/Best").Hide();
        GetNode<Control>("Margin/Values/Divider").Hide();

        LoadBestScore();
        UpdateLabels();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        float weight =
            1.0f -
            Mathf.Exp(
                -8.0f * (float)delta);
        _displayColor =
            _displayColor.Lerp(
                _targetColor,
                weight);
        _momentumWeight =
            Mathf.Lerp(
                _momentumWeight,
                _targetMomentumWeight,
                weight);
        _scoreValue.Modulate =
            Colors.White.Lerp(
                _displayColor,
                _momentumWeight * 0.88f);
        _scoreTitle.Modulate =
            new Color(
                0.64f,
                0.69f,
                0.76f,
                0.72f)
                .Lerp(
                    _displayColor,
            _momentumWeight * 0.74f);
    }

    public override void _ExitTree()
    {
        FlushPendingBestScore();
    }

    public override void _Notification(int what)
    {
        if (what ==
                NotificationWMCloseRequest ||
            what ==
                NotificationApplicationPaused)
        {
            FlushPendingBestScore();
        }
    }

    public void SetMomentumVisual(
        Color color,
        float weight)
    {
        _targetColor = color;
        _targetMomentumWeight =
            Mathf.Clamp(weight, 0.0f, 1.0f);
    }

    public void OnMobSquashed(int rewardMultiplier)
    {
        AddScore(
            Mathf.Max(
                1,
                rewardMultiplier));
    }

    public void AddScore(int amount)
    {
        Score += Mathf.Max(0, amount);

        if (Score > BestScore)
        {
            BestScore = Score;
            IsNewRecord = true;
            _bestScoreSavePending = true;
        }

        UpdateLabels();
        AnimateScoreChange();
    }

    private void LoadBestScore()
    {
        ConfigFile saveFile = new();
        Error loadResult = saveFile.Load(SavePath);

        if (loadResult == Error.Ok)
        {
            BestScore = Mathf.Max(0, saveFile.GetValue(SaveSection, BestScoreKey, 0).AsInt32());
        }
        else if (loadResult != Error.FileNotFound)
        {
            GD.PushWarning($"Could not load the high score: {loadResult}");
        }
    }

    public void FlushPendingBestScore()
    {
        if (!_bestScoreSavePending)
        {
            return;
        }

        ConfigFile saveFile = new();
        saveFile.SetValue(SaveSection, BestScoreKey, BestScore);
        Error saveResult = saveFile.Save(SavePath);

        if (saveResult != Error.Ok)
        {
            GD.PushWarning($"Could not save the high score: {saveResult}");
            return;
        }
        _bestScoreSavePending = false;
    }

    private void UpdateLabels()
    {
        _scoreValue.Text = Score.ToString();
        _bestValue.Text = BestScore.ToString();
    }

    private void AnimateScoreChange()
    {
        _scoreTween?.Kill();
        _scoreValue.PivotOffset = _scoreValue.Size * 0.5f;
        _scoreValue.Scale = Vector2.One * 1.25f;

        _scoreTween = CreateTween().SetParallel(true);
        _scoreTween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _scoreTween.TweenProperty(_scoreValue, "scale", Vector2.One, 0.22f);
    }
}
