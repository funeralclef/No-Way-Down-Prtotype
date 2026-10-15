using Godot;

public partial class RushBreakable : StaticBody3D
{
    [Export(PropertyHint.Range, "0.0,5.0,0.25")]
    public float SlowAmount { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "0.25,4.0,0.05")]
    public float SlowDuration { get; set; } = 0.7f;

    private bool _isBreaking;

    public bool Break(bool poweredImpact)
    {
        if (_isBreaking)
        {
            return false;
        }

        _isBreaking = true;
        CollisionLayer = 0;
        CollisionMask = 0;

        float breakDuration = poweredImpact ? 0.12f : 0.24f;
        float spinDirection = GetInstanceId() % 2UL == 0UL ? 1.0f : -1.0f;
        Tween breakTween = CreateTween().SetParallel(true);
        breakTween.SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.In);
        breakTween.TweenProperty(
            this,
            "scale",
            poweredImpact
                ? new Vector3(1.55f, 0.04f, 1.55f)
                : new Vector3(1.18f, 0.08f, 1.3f),
            breakDuration);
        breakTween.TweenProperty(
            this,
            "rotation:z",
            spinDirection * (poweredImpact ? 0.45f : 0.22f),
            breakDuration);
        breakTween.TweenProperty(
            this,
            "position:y",
            Position.Y - 0.8f,
            breakDuration);
        breakTween.SetParallel(false);
        breakTween.TweenCallback(Callable.From(QueueFree));
        return true;
    }
}
