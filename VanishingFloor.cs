using Godot;

public partial class VanishingFloor : StaticBody3D
{
    [Export(PropertyHint.Range, "0.1,1.5,0.05")]
    public float WarningTime { get; set; } = 0.42f;

    [Export(PropertyHint.Range, "0.2,1.5,0.05")]
    public float FallTime { get; set; } = 0.55f;

    private Area3D _trigger;
    private bool _triggered;

    public override void _Ready()
    {
        _trigger = GetNode<Area3D>("Trigger");
        _trigger.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_triggered ||
            body is not Player player)
        {
            return;
        }

        _triggered = true;
        player.ApplyTraversalSpeedBoost(
            1.4f,
            0.72f);
        _trigger.SetDeferred(Area3D.PropertyName.Monitoring, false);

        Tween warningTween = CreateTween();
        warningTween.SetTrans(Tween.TransitionType.Sine);
        warningTween.SetEase(Tween.EaseType.InOut);
        warningTween.TweenProperty(
            this,
            "scale",
            new Vector3(1.035f, 0.82f, 1.035f),
            WarningTime * 0.5f);
        warningTween.TweenProperty(
            this,
            "scale",
            Vector3.One,
            WarningTime * 0.5f);
        warningTween.TweenCallback(Callable.From(Collapse));
    }

    private void Collapse()
    {
        CollisionLayer = 0;
        CollisionMask = 0;
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape3D collision)
            {
                collision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
            }
        }

        Tween fallTween = CreateTween().SetParallel(true);
        fallTween.SetTrans(Tween.TransitionType.Quad);
        fallTween.SetEase(Tween.EaseType.In);
        fallTween.TweenProperty(
            this,
            "position:y",
            Position.Y - 5.0f,
            FallTime);
        fallTween.TweenProperty(
            this,
            "rotation:x",
            0.22f,
            FallTime);
        fallTween.TweenProperty(
            this,
            "scale",
            new Vector3(0.92f, 0.12f, 0.92f),
            FallTime);
        fallTween.SetParallel(false);
        fallTween.TweenCallback(Callable.From(QueueFree));
    }
}
