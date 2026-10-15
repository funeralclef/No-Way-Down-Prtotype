using Godot;

/// <summary>
/// Owns the player's authored visual hierarchy and translates gameplay state
/// into pose, skin and effects. It never mutates movement or combat state.
/// </summary>
[GlobalClass]
public partial class PlayerPresentation : Node3D
{
    public int EffectsQuality { get; private set; } = 3;

    private AnimationPlayer _animationPlayer;
    private GpuParticles3D _momentumTrail;
    private OmniLight3D _rushGlow;
    private ModelVisualRoot _characterVisual;
    private Node3D _skinVisualRoot;
    private MeshInstance3D _coreShell;
    private MeshInstance3D _forwardVisor;
    private MeshInstance3D _technicalMark;
    private StandardMaterial3D _coreShellMaterial;
    private StandardMaterial3D _forwardVisorMaterial;
    private StandardMaterial3D _technicalMarkMaterial;
    private bool _bindingsReady;
    private bool _bindingFailed;

    public override void _Ready()
    {
        EnsureBindings();
        if (!_bindingsReady)
        {
            _bindingFailed = true;
            GD.PushError(
                "PlayerPresentation requires its authored visual nodes " +
                "and a valid ModelVisualRoot at 'Character'.");
        }

        SetEffectsQuality(EffectsQuality);
    }

    public void SetAnimationSpeed(
        Vector3 inputDirection,
        float inputMagnitude,
        float movementMultiplier)
    {
        EnsureBindings();
        if (!IsInstanceValid(_animationPlayer))
        {
            return;
        }

        _animationPlayer.SpeedScale = inputDirection.IsZeroApprox()
            ? 1.0f
            : Mathf.Lerp(
                1.0f,
                1.4f,
                inputMagnitude) *
                movementMultiplier;
    }

    public void UpdateMotion(
        Vector3 velocity,
        Vector3 inputDirection,
        float jumpImpulse,
        Player.MomentumTier momentumTier,
        float speedBoostVisualIntensity,
        float nitroSpool,
        int airStompChain,
        Player.ComboFlowTier comboFlowTier,
        float deltaSeconds)
    {
        Vector3 horizontalHeading = new(
            velocity.X,
            0.0f,
            velocity.Z);
        float targetTurnLean = 0.0f;
        if (!horizontalHeading.IsZeroApprox())
        {
            horizontalHeading = horizontalHeading.Normalized();
            if (!inputDirection.IsZeroApprox())
            {
                float turnAmount =
                    horizontalHeading.X * inputDirection.Z -
                    horizontalHeading.Z * inputDirection.X;
                targetTurnLean = Mathf.Clamp(
                    turnAmount,
                    -1.0f,
                    1.0f) *
                    Mathf.DegToRad(9.0f);
            }

            Basis = Basis.LookingAt(horizontalHeading);
        }

        Rotation = new Vector3(
            Mathf.Pi / 6.0f * velocity.Y / jumpImpulse,
            Rotation.Y,
            targetTurnLean);
        UpdateMomentumEffects(
            velocity,
            momentumTier,
            speedBoostVisualIntensity,
            nitroSpool,
            airStompChain,
            comboFlowTier,
            deltaSeconds);
    }

    public void SetDamageVisible(bool visible)
    {
        Visible = visible;
    }

    public void ApplyBodyColor(Color color)
    {
        EnsureBindings();
        _characterVisual?.ApplyTint(color);
    }

    public void ApplySkin(
        int skinIndex,
        Color primary,
        Color secondary,
        Color accent)
    {
        // Primary remains part of Player's stable customization contract. The
        // current minimal silhouette intentionally uses secondary + accent.
        _ = primary;
        EnsureBindings();
        ApplyBodyColor(secondary);
        if (!IsInstanceValid(_skinVisualRoot) ||
            !IsInstanceValid(_coreShell) ||
            !IsInstanceValid(_forwardVisor) ||
            !IsInstanceValid(_technicalMark))
        {
            return;
        }

        _skinVisualRoot.Show();
        SetMaterialColor(_coreShellMaterial, secondary);
        SetMaterialColor(_forwardVisorMaterial, accent);
        SetMaterialColor(_technicalMarkMaterial, accent);
        Vector3 markPosition = _technicalMark.Position;
        markPosition.X = Mathf.Clamp(skinIndex, 0, 3) % 2 == 0
            ? -0.18f
            : 0.18f;
        _technicalMark.Position = markPosition;
    }

    public void SetEffectsQuality(int quality)
    {
        EffectsQuality = Mathf.Clamp(quality, 0, 3);
        EnsureBindings();
        if (IsInstanceValid(_momentumTrail))
        {
            _momentumTrail.Amount = EffectsQuality switch
            {
                0 => 1,
                1 => 10,
                2 => 18,
                _ => 28
            };
            _momentumTrail.Visible = EffectsQuality > 0;
            if (EffectsQuality == 0)
            {
                _momentumTrail.Emitting = false;
            }
        }

        if (IsInstanceValid(_rushGlow))
        {
            _rushGlow.Visible = EffectsQuality >= 2;
            if (EffectsQuality < 2)
            {
                _rushGlow.LightEnergy = 0.0f;
            }
        }
    }

    private void EnsureBindings()
    {
        if (_bindingsReady || _bindingFailed)
        {
            return;
        }

        _animationPlayer ??=
            GetNodeOrNull<AnimationPlayer>("../AnimationPlayer");
        _momentumTrail ??=
            GetNodeOrNull<GpuParticles3D>("../MomentumTrail");
        _rushGlow ??=
            GetNodeOrNull<OmniLight3D>("../RushGlow");
        _characterVisual ??=
            GetNodeOrNull<ModelVisualRoot>("Character");
        _skinVisualRoot ??=
            GetNodeOrNull<Node3D>("SkinSilhouette");
        _coreShell ??=
            GetNodeOrNull<MeshInstance3D>(
                "SkinSilhouette/CoreShell");
        _forwardVisor ??=
            GetNodeOrNull<MeshInstance3D>(
                "SkinSilhouette/ForwardVisor");
        _technicalMark ??=
            GetNodeOrNull<MeshInstance3D>(
                "SkinSilhouette/RunnerTechnicalMark");
        _coreShellMaterial ??=
            _coreShell?.MaterialOverride as StandardMaterial3D;
        _forwardVisorMaterial ??=
            _forwardVisor?.MaterialOverride as StandardMaterial3D;
        _technicalMarkMaterial ??=
            _technicalMark?.MaterialOverride as StandardMaterial3D;

        _bindingsReady =
            IsInstanceValid(_animationPlayer) &&
            IsInstanceValid(_momentumTrail) &&
            IsInstanceValid(_rushGlow) &&
            IsInstanceValid(_characterVisual) &&
            _characterVisual.ValidateContract() &&
            IsInstanceValid(_skinVisualRoot) &&
            IsInstanceValid(_coreShell) &&
            IsInstanceValid(_forwardVisor) &&
            IsInstanceValid(_technicalMark) &&
            IsInstanceValid(_coreShellMaterial) &&
            IsInstanceValid(_forwardVisorMaterial) &&
            IsInstanceValid(_technicalMarkMaterial);
    }

    private void UpdateMomentumEffects(
        Vector3 velocity,
        Player.MomentumTier momentumTier,
        float speedBoostVisualIntensity,
        float nitroSpool,
        int airStompChain,
        Player.ComboFlowTier comboFlowTier,
        float deltaSeconds)
    {
        EnsureBindings();
        if (!IsInstanceValid(_momentumTrail) ||
            !IsInstanceValid(_rushGlow))
        {
            return;
        }

        Vector2 horizontalVelocity = new(
            velocity.X,
            velocity.Z);
        bool isMoving = horizontalVelocity.LengthSquared() > 4.0f;
        float boostVisual = Mathf.Max(
            speedBoostVisualIntensity,
            nitroSpool);
        float comboVisual = airStompChain >= 3
            ? (int)comboFlowTier /
                (float)(int)Player.ComboFlowTier.Legendary
            : 0.0f;
        _momentumTrail.Emitting =
            EffectsQuality > 0 &&
            isMoving &&
            (momentumTier != Player.MomentumTier.Low ||
                boostVisual > 0.01f ||
                comboVisual > 0.0f);
        float momentumTrailAmount = momentumTier switch
        {
            Player.MomentumTier.Rush => 1.0f,
            Player.MomentumTier.Medium => 0.42f,
            _ => 0.0f
        };
        _momentumTrail.AmountRatio = Mathf.Max(
            Mathf.Max(
                momentumTrailAmount,
                comboVisual * 0.86f),
            Mathf.Lerp(0.32f, 1.0f, boostVisual));
        _momentumTrail.SpeedScale = Mathf.Max(
            momentumTier == Player.MomentumTier.Rush
                ? 1.35f
                : 1.0f,
            Mathf.Max(
                Mathf.Lerp(1.0f, 2.35f, boostVisual),
                1.0f + comboVisual * 0.85f));

        float glowQualityMultiplier =
            EffectsQuality >= 3 ? 1.0f : 0.55f;
        float momentumGlowEnergy = momentumTier switch
        {
            Player.MomentumTier.Medium =>
                0.65f * glowQualityMultiplier,
            Player.MomentumTier.Rush =>
                1.8f * glowQualityMultiplier,
            _ => 0.0f
        };
        float targetGlowEnergy = EffectsQuality < 2
            ? 0.0f
            : Mathf.Max(
                Mathf.Max(
                    momentumGlowEnergy,
                    comboVisual *
                        1.65f *
                        glowQualityMultiplier),
                boostVisual *
                    2.6f *
                    glowQualityMultiplier);
        float visualWeight = deltaSeconds <= 0.0f
            ? 1.0f
            : 1.0f - Mathf.Exp(-8.0f * deltaSeconds);
        _rushGlow.LightEnergy = Mathf.Lerp(
            _rushGlow.LightEnergy,
            targetGlowEnergy,
            visualWeight);
        Color targetGlowColor = boostVisual > 0.01f
            ? new Color(0.24f, 0.86f, 1.0f)
            : new Color(1.0f, 0.35f, 0.06f);
        if (airStompChain >= 3)
        {
            targetGlowColor = comboFlowTier switch
            {
                Player.ComboFlowTier.Rhythm =>
                    new Color(0.28f, 0.92f, 1.0f),
                Player.ComboFlowTier.Dominant =>
                    new Color(1.0f, 0.7f, 0.12f),
                Player.ComboFlowTier.Legendary =>
                    new Color(1.0f, 0.24f, 0.78f),
                _ => targetGlowColor
            };
        }

        if (_momentumTrail.ProcessMaterial is
            ParticleProcessMaterial trailMaterial)
        {
            trailMaterial.Color = trailMaterial.Color.Lerp(
                targetGlowColor,
                visualWeight);
        }
        _rushGlow.LightColor = _rushGlow.LightColor.Lerp(
            targetGlowColor,
            visualWeight);
    }

    private static void SetMaterialColor(
        StandardMaterial3D material,
        Color color)
    {
        if (material != null)
        {
            material.AlbedoColor = color;
        }
    }
}
