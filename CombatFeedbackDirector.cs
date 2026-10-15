using Godot;
using System;
using System.Collections.Generic;

public partial class CombatFeedbackDirector : Node
{
    private const int ImpactBurstPoolSize = 6;
    private const int MaximumImpactShards = 12;

    private sealed class ImpactBurstSlot
    {
        public Node3D Root;
        public MeshInstance3D Ring;
        public MeshInstance3D[] Shards;
        public Tween ActiveTween;

        public void Hide()
        {
            if (GodotObject.IsInstanceValid(Root))
            {
                Root.Hide();
            }
        }
    }

    private Player _player;
    private Camera3D _camera;
    private Node _worldRoot;
    private Control _overlayRoot;
    private ColorRect _impactFlash;
    private ColorRect _cinematicOverlay;
    private ShaderMaterial _cinematicMaterial;
    private Label _comboLabel;
    private Label _styleLabel;
    private Label _finisherLabel;
    private AudioStreamPlayer _impactAudio;
    private AudioStreamPlayer _vitalsAudio;
    private readonly AudioStreamWav[] _comboTones =
        new AudioStreamWav[8];
    private float _labelHoldTimer;
    private float _nitroFx;
    private float _criticalFx;
    private float _fxTime;
    private float _lastHealthRatio = 1.0f;
    private float _cameraKickHorizontal;
    private float _cameraKickVertical;
    private float _nitroRoadPhase;
    private TorusMesh _impactRingMesh;
    private BoxMesh _impactShardMesh;
    private BoxMesh _impactFinisherShardMesh;
    private readonly Dictionary<ulong, StandardMaterial3D>
        _effectMaterialCache = new();
    private readonly ImpactBurstSlot[] _impactBurstPool =
        new ImpactBurstSlot[ImpactBurstPoolSize];
    private int _nextImpactBurstSlot;
    private int _firstUseWarmupFrames;
    private Node3D _slipstreamCueRoot;
    private StandardMaterial3D
        _slipstreamCueMaterial;
    private Label3D _slipstreamCueLabel;
    private float _slipstreamCuePulseTime;
    private Node3D _clutchVaultCueRoot;
    private StandardMaterial3D
        _clutchVaultCueMaterial;
    private Label3D _clutchVaultCueLabel;
    private float _clutchVaultCuePulseTime;
    private Node3D
        _clutchVaultPlayerPrompt;
    private Node3D
        _clutchVaultTargetReticle;
    private MeshInstance3D
        _clutchVaultPlayerRing;
    private readonly MeshInstance3D[]
        _clutchVaultTargetRings =
            new MeshInstance3D[2];
    private readonly MeshInstance3D[]
        _clutchVaultLinkRails =
            new MeshInstance3D[2];
    private readonly MeshInstance3D[]
        _clutchVaultLinkSegments =
            new MeshInstance3D[7];

    public void Initialize(
        Player player,
        Camera3D camera,
        Node worldRoot,
        Control userInterface)
    {
        _player = player;
        _camera = camera;
        _worldRoot = worldRoot;
        CreateInterface(userInterface);
        CreateAudio();
        CreateImpactResources();
        CreateSlipstreamCueVisual();
        CreateClutchVaultCueVisual();
        BeginFirstUseWarmup();
        _lastHealthRatio =
            _player.HealthRatio;
        _player.RiftStepStarted +=
            OnRiftStepStarted;
        _player.SlipstreamDodge +=
            OnSlipstreamDodge;
        _player.ClutchVaultStarted +=
            OnClutchVaultStarted;
        _player.HealthChanged +=
            OnHealthChanged;
    }

    public override void _Process(double delta)
    {
        float deltaSeconds = (float)delta;
        bool warmingFirstUse =
            UpdateFirstUseWarmup();
        if (!warmingFirstUse)
        {
            UpdateComboInterface(deltaSeconds);
        }
        UpdateCameraKick(deltaSeconds);
        UpdateSlipstreamCueVisual(
            deltaSeconds);
        UpdateClutchVaultCueVisual(
            deltaSeconds);
        UpdateCinematicVitals(
            deltaSeconds);
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1.0;
        if (IsInstanceValid(_player))
        {
            _player.RiftStepStarted -=
                OnRiftStepStarted;
            _player.SlipstreamDodge -=
                OnSlipstreamDodge;
            _player.ClutchVaultStarted -=
                OnClutchVaultStarted;
            _player.HealthChanged -=
                OnHealthChanged;
        }
        if (IsInstanceValid(_camera))
        {
            _camera.HOffset = 0.0f;
            _camera.VOffset = 0.0f;
        }
        if (IsInstanceValid(
            _slipstreamCueRoot))
        {
            _slipstreamCueRoot.Hide();
        }
        if (IsInstanceValid(
            _clutchVaultCueRoot))
        {
            _clutchVaultCueRoot.Hide();
        }
        for (int index = 0;
            index < _impactBurstPool.Length;
            index++)
        {
            ImpactBurstSlot slot =
                _impactBurstPool[index];
            if (slot == null)
            {
                continue;
            }
            if (slot.ActiveTween != null &&
                slot.ActiveTween.IsValid())
            {
                slot.ActiveTween.Kill();
            }
            if (IsInstanceValid(slot.Root))
            {
                slot.Root.QueueFree();
            }
            _impactBurstPool[index] = null;
        }
        _effectMaterialCache.Clear();
        _impactRingMesh = null;
        _impactShardMesh = null;
        _impactFinisherShardMesh = null;
    }

    public void OnComboImpact(
        int chainCount,
        int stylePoints,
        int flowTier,
        int archetype,
        bool counterStomp,
        bool milestone,
        bool finisher,
        Vector3 worldPosition)
    {
        FinishFirstUseWarmup();
        Player.ComboFlowTier tier =
            (Player.ComboFlowTier)Mathf.Clamp(
                flowTier,
                0,
                (int)Player.ComboFlowTier.Legendary);
        Color tierColor = GetTierColor(tier);
        _labelHoldTimer =
            finisher ? 1.8f : milestone ? 1.35f : 0.95f;
        _comboLabel.Text = $"x{Mathf.Max(1, chainCount)}";
        _comboLabel.Modulate = tierColor;
        _styleLabel.Text = finisher
            ? "FINISHER"
            : milestone
                ? GetMilestoneName(tier)
                : $"STYLE  {stylePoints}";
        _styleLabel.Modulate = tierColor.Lerp(
            Colors.White,
            0.42f);
        _comboLabel.Show();
        _styleLabel.Show();
        _comboLabel.Scale =
            Vector2.One *
            (finisher ? 1.52f : milestone ? 1.34f : 1.18f);
        _styleLabel.Scale =
            Vector2.One * (finisher ? 1.18f : 1.0f);

        float impactStrength =
            0.35f +
            (int)tier * 0.18f +
            (counterStomp ? 0.18f : 0.0f) +
            (milestone ? 0.22f : 0.0f) +
            (finisher ? 0.48f : 0.0f);
        CreateImpactBurst(
            worldPosition,
            tierColor,
            impactStrength,
            finisher,
            Vector3.Zero);
        FlashScreen(
            tierColor,
            finisher
                ? 0.26f
                : milestone
                    ? 0.14f
                    : 0.055f);
        KickCamera(worldPosition, impactStrength);
        PlayImpactTone(
            chainCount,
            tier,
            counterStomp,
            finisher);

        // Never freeze global physics for an airborne impact. Even a short
        // hit-stop reads as a broken jump in a vertical scroller, especially
        // when the player presses Nitro during a dense chain. Camera, audio,
        // flash and particles carry the impact instead.
    }

    public void OnComboRescued(
        int chainCount,
        bool fromGroundGrace)
    {
        _labelHoldTimer = 1.1f;
        _comboLabel.Text = $"x{Mathf.Max(1, chainCount)}";
        _comboLabel.Modulate =
            new Color(0.28f, 0.94f, 1.0f);
        _styleLabel.Text = fromGroundGrace
            ? "ROUTE SAVED"
            : "AIR SAVE";
        _styleLabel.Modulate =
            new Color(0.72f, 1.0f, 1.0f);
        _comboLabel.Show();
        _styleLabel.Show();
        _comboLabel.Scale = Vector2.One * 1.26f;
        FlashScreen(
            new Color(0.25f, 0.9f, 1.0f),
            0.08f);
        PlayImpactTone(
            chainCount,
            Player.ComboFlowTier.Rhythm,
            false,
            false);
    }

    public void OnComboEnded(
        int chainCount,
        int stylePoints)
    {
        if (chainCount < 3)
        {
            _labelHoldTimer = 0.0f;
            return;
        }

        _labelHoldTimer = 0.72f;
        _comboLabel.Text = $"x{chainCount}";
        _comboLabel.Modulate =
            new Color(0.82f, 0.86f, 0.94f);
        _styleLabel.Text = $"BANKED  {stylePoints}";
        _styleLabel.Modulate =
            new Color(0.72f, 0.78f, 0.86f);
        _comboLabel.Show();
        _styleLabel.Show();
    }

    private void CreateInterface(Control userInterface)
    {
        _overlayRoot = new Control
        {
            Name = "CombatFeedback",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _overlayRoot.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect);
        userInterface.AddChild(_overlayRoot);

        _impactFlash = new ColorRect
        {
            Name = "ImpactFlash",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(1.0f, 1.0f, 1.0f, 0.0f)
        };
        _impactFlash.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.FullRect);
        _overlayRoot.AddChild(_impactFlash);

        _cinematicMaterial =
            CreateCinematicMaterial();
        _cinematicOverlay =
            new ColorRect
            {
                Name =
                    "CinematicVitals",
                Color = Colors.White,
                Material =
                    _cinematicMaterial,
                MouseFilter =
                    Control.MouseFilterEnum.Ignore
            };
        _cinematicOverlay
            .SetAnchorsAndOffsetsPreset(
                Control.LayoutPreset.FullRect);
        _overlayRoot.AddChild(
            _cinematicOverlay);

        _comboLabel = CreateFeedbackLabel(
            "Combo",
            42,
            new Vector2(150.0f, 58.0f));
        _styleLabel = CreateFeedbackLabel(
            "Style",
            15,
            new Vector2(210.0f, 28.0f));
        _finisherLabel = CreateFeedbackLabel(
            "Finisher",
            12,
            new Vector2(190.0f, 24.0f));
        _comboLabel.HorizontalAlignment =
            HorizontalAlignment.Center;
        _styleLabel.HorizontalAlignment =
            HorizontalAlignment.Center;
        _finisherLabel.HorizontalAlignment =
            HorizontalAlignment.Center;
        _comboLabel.Hide();
        _styleLabel.Hide();
        _finisherLabel.Hide();
    }

    private void OnRiftStepStarted(
        bool teleported,
        Vector3 origin,
        Vector3 destination)
    {
        FinishFirstUseWarmup();
        if (!IsInstanceValid(_player))
        {
            return;
        }

        Color color =
            teleported
                ? new Color(
                    0.82f,
                    0.24f,
                    1.0f)
                : new Color(
                    0.24f,
                    0.82f,
                    1.0f);
        float strength =
            teleported ? 1.18f : 0.9f;
        _nitroFx =
            Mathf.Max(
                _nitroFx,
                teleported ? 1.0f : 0.82f);
        CreateImpactBurst(
            origin +
                Vector3.Up * 0.18f,
            color,
            strength,
            false,
            destination - origin);
        if (teleported)
        {
            CreateImpactBurst(
                destination,
                color.Lerp(
                    new Color(
                        0.18f,
                        1.0f,
                        0.92f),
                    0.48f),
                1.08f,
                false,
                origin - destination);
            CreateRiftGate(
                origin +
                    Vector3.Up * 0.72f,
                destination - origin,
                color,
                false);
            CreateRiftGate(
                destination +
                    Vector3.Up * 0.2f,
                origin - destination,
                color.Lerp(
                    new Color(
                        0.12f,
                        1.0f,
                        0.9f),
                    0.62f),
                true);
            CreateRiftTrace(
                origin,
                destination,
                color);
            _labelHoldTimer = 0.62f;
            _styleLabel.Text =
                "RIFT CHAIN";
            _styleLabel.Modulate =
                color.Lerp(
                    Colors.White,
                    0.55f);
            _styleLabel.Scale =
                Vector2.One * 1.14f;
            _styleLabel.Show();
        }
        else
        {
            CreateGroundBurstWake(
                origin,
                destination,
                color);
        }
        FlashScreen(
            color,
            teleported ? 0.19f : 0.11f);
        KickCamera(
            destination,
            strength);
        PlayRiftStepTone(teleported);
    }

    private void OnSlipstreamDodge(
        int scoreReward,
        float nitroReward,
        float speedImpulse,
        bool perfectDodge,
        Vector3 worldPosition,
        Vector3 passDirection)
    {
        FinishFirstUseWarmup();
        if (!IsInstanceValid(_player))
        {
            return;
        }

        passDirection.Y = 0.0f;
        if (passDirection.IsZeroApprox())
        {
            passDirection =
                _player.Velocity;
            passDirection.Y = 0.0f;
        }
        if (passDirection.IsZeroApprox())
        {
            passDirection =
                Vector3.Forward;
        }
        passDirection =
            passDirection.Normalized();
        Color color =
            perfectDodge
                ? new Color(
                    1.0f,
                    0.72f,
                    0.12f)
                : new Color(
                    0.16f,
                    0.94f,
                    1.0f);
        float strength =
            perfectDodge ? 1.34f : 1.02f;
        _nitroFx =
            Mathf.Max(
                _nitroFx,
                perfectDodge ? 1.0f : 0.84f);
        CreateImpactBurst(
            worldPosition +
                Vector3.Up * 0.22f,
            color,
            strength,
            perfectDodge,
            passDirection);
        CreateGroundBurstWake(
            worldPosition -
                passDirection * 2.1f,
            worldPosition +
                passDirection *
                    (perfectDodge
                        ? 4.2f
                        : 3.4f),
            color);
        _labelHoldTimer =
            perfectDodge ? 0.92f : 0.7f;
        _styleLabel.Text =
            perfectDodge
                ? $"PERFECT LINE  +{scoreReward}"
                : $"SLIPSTREAM  +{scoreReward}";
        _styleLabel.Modulate =
            color.Lerp(
                Colors.White,
                0.38f);
        _styleLabel.Scale =
            Vector2.One *
            (perfectDodge ? 1.22f : 1.1f);
        _styleLabel.Show();
        FlashScreen(
            color,
            perfectDodge ? 0.18f : 0.11f);
        KickCamera(
            worldPosition,
            strength);
        PlaySlipstreamTone(
            perfectDodge);
    }

    private void OnClutchVaultStarted(
        Vector3 origin,
        Vector3 targetPosition)
    {
        FinishFirstUseWarmup();
        if (!IsInstanceValid(_player))
        {
            return;
        }

        Color color =
            new Color(
                0.12f,
                1.0f,
                0.7f);
        Vector3 travel =
            targetPosition -
            origin;
        CreateImpactBurst(
            origin +
                Vector3.Up * 0.2f,
            color,
            0.86f,
            false,
            travel);
        CreateImpactBurst(
            targetPosition,
            color.Lerp(
                Colors.White,
                0.34f),
            0.68f,
            false,
            -travel);
        CreateClutchBlinkTrace(
            origin,
            targetPosition,
            color);
        CreateClutchBlinkPhaseGates(
            origin,
            targetPosition,
            color);
        _labelHoldTimer = 0.58f;
        _styleLabel.Text =
            "CLUTCH BLINK";
        _styleLabel.Modulate =
            color.Lerp(
                Colors.White,
                0.42f);
        _styleLabel.Scale =
            Vector2.One * 1.08f;
        _styleLabel.Show();
        FlashScreen(
            color,
            0.11f);
        KickCamera(
            targetPosition,
            0.92f);
        PlayRiftStepTone(false);
    }

    private void BeginFirstUseWarmup()
    {
        if (_impactBurstPool.Length == 0 ||
            _impactBurstPool[0] == null ||
            !IsInstanceValid(_player))
        {
            return;
        }

        ImpactBurstSlot slot =
            _impactBurstPool[0];
        _nextImpactBurstSlot = 1;
        slot.Root.GlobalPosition =
            _player.GlobalPosition +
            Vector3.Up * 0.5f;
        slot.Root.Scale =
            Vector3.One * 0.0005f;
        slot.Root.Show();
        slot.Ring.Transparency = 0.999f;
        slot.Ring.Show();
        for (int index = 0;
            index < slot.Shards.Length;
            index++)
        {
            MeshInstance3D shard =
                slot.Shards[index];
            shard.Position = Vector3.Zero;
            shard.Scale = Vector3.One;
            shard.Transparency = 0.999f;
            shard.Show();
        }

        // The first visible combo string can otherwise rasterize its glyphs
        // in the same frame as the first stomp. Render the required glyph
        // set at effectively zero opacity during the run transition.
        _comboLabel.Text =
            "x0123456789";
        _styleLabel.Text =
            "STYLE FLOW ROUTE FINISHER AIR SAVE BANKED";
        _finisherLabel.Text =
            "FINISHER 100%";
        Color warmupColor =
            new(1.0f, 1.0f, 1.0f, 0.001f);
        _comboLabel.Modulate = warmupColor;
        _styleLabel.Modulate = warmupColor;
        _finisherLabel.Modulate = warmupColor;
        _comboLabel.Show();
        _styleLabel.Show();
        _finisherLabel.Show();
        _firstUseWarmupFrames = 2;
    }

    private bool UpdateFirstUseWarmup()
    {
        if (_firstUseWarmupFrames <= 0)
        {
            return false;
        }

        if (_firstUseWarmupFrames == 1)
        {
            FinishFirstUseWarmup();
            return false;
        }
        _firstUseWarmupFrames--;
        return true;
    }

    private void FinishFirstUseWarmup()
    {
        if (_firstUseWarmupFrames <= 0)
        {
            return;
        }

        _firstUseWarmupFrames = 0;
        ImpactBurstSlot slot =
            _impactBurstPool[0];
        if (slot != null &&
            IsInstanceValid(slot.Root) &&
            slot.ActiveTween == null)
        {
            slot.Root.Hide();
        }
        _comboLabel.Hide();
        _styleLabel.Hide();
        _finisherLabel.Hide();
    }

    private void OnHealthChanged(
        float health,
        float maxHealth)
    {
        float ratio =
            maxHealth <= 0.0f
                ? 0.0f
                : Mathf.Clamp(
                    health / maxHealth,
                    0.0f,
                    1.0f);
        if (ratio <
            _lastHealthRatio -
                0.001f)
        {
            float severity =
                1.0f - ratio;
            FlashScreen(
                new Color(
                    1.0f,
                    0.08f,
                    0.04f),
                Mathf.Lerp(
                    0.08f,
                    0.2f,
                    severity));
            _criticalFx =
                Mathf.Max(
                    _criticalFx,
                    ratio <= 0.3f
                        ? 1.0f
                        : 0.42f);
            KickCamera(
                _player.GlobalPosition +
                    Vector3.Right,
                Mathf.Lerp(
                    0.38f,
                    0.68f,
                    severity));
            if (ratio <= 0.3f)
            {
                PlayCriticalTone(ratio);
            }
        }
        _lastHealthRatio = ratio;
    }

    private void UpdateCinematicVitals(
        float deltaSeconds)
    {
        if (!IsInstanceValid(_player) ||
            !IsInstanceValid(
                _cinematicMaterial))
        {
            return;
        }

        _fxTime += deltaSeconds;
        float nitroTarget =
            _player.IsNitroBurning
                ? _player.IsNitroOverdrive
                    ? 0.84f
                    : Mathf.Lerp(
                        0.08f,
                        0.58f,
                        _player
                            .NitroThrottle)
                : 0.0f;
        _nitroFx =
            Mathf.MoveToward(
                _nitroFx,
                nitroTarget,
                deltaSeconds *
                    (nitroTarget >
                        _nitroFx
                            ? 5.5f
                            : 1.85f));
        float criticalTarget =
            _player.HealthRatio <= 0.3f
                ? Mathf.Clamp(
                    (0.34f -
                        _player.HealthRatio) /
                        0.24f,
                    0.18f,
                    1.0f)
                : 0.0f;
        _criticalFx =
            Mathf.MoveToward(
                _criticalFx,
                criticalTarget,
                deltaSeconds *
                    (criticalTarget >
                        _criticalFx
                            ? 4.8f
                            : 1.25f));
        _cinematicMaterial
            .SetShaderParameter(
                "nitro_level",
                _nitroFx);
        _cinematicMaterial
            .SetShaderParameter(
                "critical_level",
                _criticalFx);
        _cinematicMaterial
            .SetShaderParameter(
                "fx_time",
                _fxTime);
    }

    private static ShaderMaterial
        CreateCinematicMaterial()
    {
        Shader shader = new()
        {
            Code = @"
shader_type canvas_item;
render_mode unshaded;

uniform float nitro_level = 0.0;
uniform float critical_level = 0.0;
uniform float fx_time = 0.0;

float hash(vec2 p) {
    return fract(
        sin(dot(
            p,
            vec2(127.1, 311.7))) *
        43758.5453);
}

void fragment() {
    vec2 p =
        (UV - vec2(0.5)) *
        vec2(1.42, 1.0);
    float radius =
        length(p);
    float edge =
        smoothstep(
            0.34,
            0.78,
            radius);
    float angle =
        atan(p.y, p.x);
    float rayCell =
        floor(
            (angle + 3.14159) *
            18.0);
    float ray =
        pow(
            max(
                0.0,
                sin(
                    angle * 36.0 -
                    fx_time * 9.0 +
                    hash(vec2(
                        rayCell,
                        2.0)) * 5.0)),
            9.0);
    ray *=
        smoothstep(
            0.18,
            0.82,
            radius);
    float nitroAlpha =
        nitro_level *
        (edge * 0.09 +
            ray * 0.2);
    float heartbeat =
        0.72 +
        0.28 *
        pow(
            max(
                0.0,
                sin(
                    fx_time * 6.4)),
            4.0);
    float criticalAlpha =
        critical_level *
        edge *
        heartbeat *
        0.2;
    vec3 color =
        vec3(
            0.1,
            0.58,
            1.0) *
            nitroAlpha +
        vec3(
            1.0,
            0.035,
            0.015) *
            criticalAlpha;
    COLOR =
        vec4(
            color,
            clamp(
                nitroAlpha +
                criticalAlpha,
                0.0,
                0.32));
}",
        };
        return new ShaderMaterial
        {
            Shader = shader
        };
    }

    private Label CreateFeedbackLabel(
        string name,
        int fontSize,
        Vector2 size)
    {
        Label label = new()
        {
            Name = name,
            CustomMinimumSize = size,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PivotOffset = size * 0.5f
        };
        label.AddThemeFontSizeOverride(
            "font_size",
            fontSize);
        label.AddThemeColorOverride(
            "font_shadow_color",
            new Color(0.0f, 0.0f, 0.0f, 0.82f));
        label.AddThemeConstantOverride(
            "shadow_offset_x",
            2);
        label.AddThemeConstantOverride(
            "shadow_offset_y",
            3);
        _overlayRoot.AddChild(label);
        return label;
    }

    private void CreateAudio()
    {
        _impactAudio = new AudioStreamPlayer
        {
            Name = "ComboImpactAudio",
            MaxPolyphony = 8,
            VolumeDb = -9.0f
        };
        AddChild(_impactAudio);
        _vitalsAudio = new AudioStreamPlayer
        {
            Name = "CinematicVitalsAudio",
            MaxPolyphony = 4,
            VolumeDb = -10.0f
        };
        AddChild(_vitalsAudio);
        float[] frequencies =
        {
            196.0f,
            220.0f,
            261.63f,
            293.66f,
            329.63f,
            392.0f,
            440.0f,
            523.25f
        };
        for (int index = 0;
            index < _comboTones.Length;
            index++)
        {
            _comboTones[index] =
                CreateImpactTone(
                    frequencies[index],
                    0.105f);
        }
    }

    private void UpdateComboInterface(float deltaSeconds)
    {
        if (!IsInstanceValid(_player) ||
            !IsInstanceValid(_camera))
        {
            return;
        }

        bool behind =
            _camera.IsPositionBehind(
                _player.GlobalPosition);
        if (!behind)
        {
            Vector2 screenPosition =
                _camera.UnprojectPosition(
                    _player.GlobalPosition +
                    Vector3.Up * 2.2f);
            _comboLabel.Position =
                screenPosition -
                new Vector2(
                    _comboLabel.Size.X * 0.5f,
                    82.0f);
            _styleLabel.Position =
                screenPosition -
                new Vector2(
                    _styleLabel.Size.X * 0.5f,
                    35.0f);
            _finisherLabel.Position =
                screenPosition -
                new Vector2(
                    _finisherLabel.Size.X * 0.5f,
                    8.0f);
        }

        _labelHoldTimer = Mathf.Max(
            0.0f,
            _labelHoldTimer - deltaSeconds);
        float labelAlpha = Mathf.Clamp(
            _labelHoldTimer / 0.22f,
            0.0f,
            1.0f);
        _comboLabel.Modulate =
            new Color(
                _comboLabel.Modulate.R,
                _comboLabel.Modulate.G,
                _comboLabel.Modulate.B,
                labelAlpha);
        _styleLabel.Modulate =
            new Color(
                _styleLabel.Modulate.R,
                _styleLabel.Modulate.G,
                _styleLabel.Modulate.B,
                labelAlpha);
        _comboLabel.Scale =
            _comboLabel.Scale.Lerp(
                Vector2.One,
                1.0f - Mathf.Exp(-10.0f * deltaSeconds));
        _styleLabel.Scale =
            _styleLabel.Scale.Lerp(
                Vector2.One,
                1.0f - Mathf.Exp(-9.0f * deltaSeconds));
        if (_labelHoldTimer <= 0.0f)
        {
            _comboLabel.Hide();
            _styleLabel.Hide();
        }

        if (_player.AirStompChain >= 3 &&
            _player.ComboFinisherCharge > 0.0f)
        {
            _finisherLabel.Text =
                $"FINISHER  {Mathf.RoundToInt(
                    _player.ComboFinisherRatio * 100.0f)}%";
            _finisherLabel.Modulate =
                GetTierColor(
                    _player.CurrentComboFlowTier);
            _finisherLabel.Show();
        }
        else
        {
            _finisherLabel.Hide();
        }
    }

    private void UpdateCameraKick(float deltaSeconds)
    {
        if (!IsInstanceValid(_camera))
        {
            return;
        }

        float recoveryWeight =
            1.0f -
            Mathf.Exp(-18.0f * deltaSeconds);
        _cameraKickHorizontal = Mathf.Lerp(
            _cameraKickHorizontal,
            0.0f,
            recoveryWeight);
        _cameraKickVertical = Mathf.Lerp(
            _cameraKickVertical,
            0.0f,
            recoveryWeight);
        float throttle =
            IsInstanceValid(_player) &&
            _player.IsNitroBurning
                ? _player.NitroThrottle
                : 0.0f;
        float roadStrength =
            Mathf.Pow(
                Mathf.Clamp(
                    throttle,
                    0.0f,
                    1.0f),
                1.25f);
        if (IsInstanceValid(_player) &&
            _player.IsNitroOverdrive)
        {
            roadStrength *= 1.18f;
        }
        _nitroRoadPhase +=
            deltaSeconds *
            Mathf.Lerp(
                19.0f,
                34.0f,
                throttle);
        float roadHorizontal =
            (Mathf.Sin(
                _nitroRoadPhase * 1.73f) +
            Mathf.Sin(
                _nitroRoadPhase * 2.91f) *
                0.38f) *
            0.032f *
            roadStrength;
        float roadVertical =
            (Mathf.Sin(
                _nitroRoadPhase * 2.37f) +
            Mathf.Sin(
                _nitroRoadPhase * 4.13f) *
                0.24f) *
            0.021f *
            roadStrength;
        _camera.HOffset =
            _cameraKickHorizontal +
            roadHorizontal;
        _camera.VOffset =
            _cameraKickVertical +
            roadVertical;
    }

    private void KickCamera(
        Vector3 worldPosition,
        float strength)
    {
        if (!IsInstanceValid(_camera) ||
            !IsInstanceValid(_player))
        {
            return;
        }

        float side = Mathf.Sign(
            worldPosition.X -
            _player.GlobalPosition.X);
        if (Mathf.IsZeroApprox(side))
        {
            side =
                _player.GetInstanceId() % 2UL == 0UL
                    ? -1.0f
                    : 1.0f;
        }

        _cameraKickHorizontal = Mathf.Clamp(
            _cameraKickHorizontal -
                side * strength * 0.16f,
            -0.48f,
            0.48f);
        _cameraKickVertical = Mathf.Clamp(
            _cameraKickVertical -
                strength * 0.08f,
            -0.3f,
            0.2f);
        _camera.Size = Mathf.Max(
            10.0f,
            _camera.Size -
                strength * 0.3f);
    }

    private void CreateImpactResources()
    {
        _impactRingMesh = new TorusMesh
        {
            InnerRadius = 0.72f,
            OuterRadius = 0.9f,
            Rings = 8,
            RingSegments = 28
        };
        _impactShardMesh = new BoxMesh
        {
            Size = new Vector3(
                0.09f,
                0.09f,
                0.3f)
        };
        _impactFinisherShardMesh =
            new BoxMesh
            {
                Size = new Vector3(
                    0.09f,
                    0.09f,
                    0.48f)
            };

        // Build the small, finite material palette while entering a run.
        // This keeps the first stomp/dash frame from also having to create
        // every material resource used by the burst.
        Color[] tierPalette =
        {
            GetTierColor(Player.ComboFlowTier.Warmup),
            GetTierColor(Player.ComboFlowTier.Rhythm),
            GetTierColor(Player.ComboFlowTier.Dominant),
            GetTierColor(Player.ComboFlowTier.Legendary)
        };
        foreach (Color tierColor in tierPalette)
        {
            PrecacheImpactMaterials(tierColor);
        }
        PrecacheImpactMaterials(
            new Color(
                0.82f,
                0.24f,
                1.0f));
        PrecacheImpactMaterials(
            new Color(
                0.24f,
                0.82f,
                1.0f));
        PrecacheImpactMaterials(
            new Color(
                0.18f,
                1.0f,
                0.92f));
        CreateImpactBurstPool();
    }

    private void CreateSlipstreamCueVisual()
    {
        if (!IsInstanceValid(_worldRoot))
        {
            return;
        }

        _slipstreamCueMaterial =
            new StandardMaterial3D
            {
                Transparency =
                    BaseMaterial3D
                        .TransparencyEnum
                        .Alpha,
                ShadingMode =
                    BaseMaterial3D
                        .ShadingModeEnum
                        .Unshaded,
                CullMode =
                    BaseMaterial3D
                        .CullModeEnum
                        .Disabled,
                AlbedoColor =
                    new Color(
                        1.0f,
                        0.78f,
                        0.12f,
                        0.18f),
                EmissionEnabled = true,
                Emission =
                    new Color(
                        1.0f,
                        0.68f,
                        0.08f),
                EmissionEnergyMultiplier =
                    1.8f
            };
        _slipstreamCueRoot =
            new Node3D
            {
                Name =
                    "SlipstreamDodgeCue",
                Visible = false
            };
        _worldRoot.AddChild(
            _slipstreamCueRoot);
        for (int sideIndex = 0;
            sideIndex < 2;
            sideIndex++)
        {
            float side =
                sideIndex == 0
                    ? -1.0f
                    : 1.0f;
            for (int segment = 0;
                segment < 3;
                segment++)
            {
                float lane =
                    segment - 1.0f;
                MeshInstance3D rail =
                    new()
                    {
                        Name =
                            $"CueRail{sideIndex + 1}_{segment + 1}",
                        Mesh = new BoxMesh
                        {
                            Size =
                                new Vector3(
                                    0.13f,
                                    0.045f,
                                    0.62f)
                        },
                        MaterialOverride =
                            _slipstreamCueMaterial,
                        Position =
                            new Vector3(
                                side *
                                    (2.0f -
                                        Mathf.Abs(
                                            lane) *
                                        0.1f),
                                0.08f,
                                lane * 0.78f),
                        RotationDegrees =
                            new Vector3(
                                0.0f,
                                -side *
                                    lane *
                                    12.0f,
                                0.0f),
                        CastShadow =
                            GeometryInstance3D
                                .ShadowCastingSetting
                                .Off
                    };
                _slipstreamCueRoot
                    .AddChild(rail);
            }
        }
        _slipstreamCueLabel =
            new Label3D
            {
                Name = "DodgeLabel",
                Text = "DODGE",
                Position =
                    new Vector3(
                        0.0f,
                        2.65f,
                        0.0f),
                FontSize = 38,
                PixelSize = 0.012f,
                OutlineSize = 8,
                Billboard =
                    BaseMaterial3D
                        .BillboardModeEnum
                        .Enabled,
                NoDepthTest = true,
                Visible = false
            };
        _slipstreamCueRoot.AddChild(
            _slipstreamCueLabel);
    }

    private void UpdateSlipstreamCueVisual(
        float deltaSeconds)
    {
        if (!IsInstanceValid(
                _slipstreamCueRoot) ||
            !IsInstanceValid(_player))
        {
            return;
        }

        Mob target =
            _player.SlipstreamCueTarget;
        Player.SlipstreamCueLevel level =
            _player.CurrentSlipstreamCue;
        bool visible =
            IsInstanceValid(target) &&
            level !=
                Player.SlipstreamCueLevel
                    .None &&
            !_player.IsGroundBurstActive;
        _slipstreamCueRoot.Visible =
            visible;
        if (!visible)
        {
            return;
        }

        _slipstreamCuePulseTime +=
            deltaSeconds;
        float urgency =
            Mathf.Clamp(
                _player
                    .SlipstreamCueUrgency,
                0.0f,
                1.0f);
        Color color =
            level switch
            {
                Player
                    .SlipstreamCueLevel
                    .Perfect =>
                    new Color(
                        1.0f,
                        0.72f,
                        0.1f),
                Player
                    .SlipstreamCueLevel
                    .Ready =>
                    new Color(
                        0.12f,
                        0.92f,
                        1.0f),
                _ =>
                    new Color(
                        1.0f,
                        0.78f,
                        0.12f)
            };
        _slipstreamCueRoot
            .GlobalPosition =
            target.GlobalPosition +
            Vector3.Up * 0.04f;
        Vector3 direction =
            _player
                .SlipstreamCueDirection;
        direction.Y = 0.0f;
        if (direction.IsZeroApprox())
        {
            direction =
                Vector3.Forward;
        }
        _slipstreamCueRoot.LookAt(
            _slipstreamCueRoot
                .GlobalPosition +
                direction.Normalized(),
            Vector3.Up);
        float pulse =
            1.0f +
            Mathf.Sin(
                _slipstreamCuePulseTime *
                Mathf.Lerp(
                    4.0f,
                    10.0f,
                    urgency)) *
            Mathf.Lerp(
                0.025f,
                0.075f,
                urgency);
        _slipstreamCueRoot.Scale =
            Vector3.One * pulse;
        _slipstreamCueMaterial
            .AlbedoColor =
            new Color(
                color.R,
                color.G,
                color.B,
                Mathf.Lerp(
                    0.14f,
                    0.36f,
                    urgency));
        _slipstreamCueMaterial.Emission =
            color;
        _slipstreamCueMaterial
            .EmissionEnergyMultiplier =
            Mathf.Lerp(
                1.4f,
                3.2f,
                urgency);
        bool ready =
            level ==
                Player.SlipstreamCueLevel
                    .Ready ||
            level ==
                Player.SlipstreamCueLevel
                    .Perfect;
        _slipstreamCueLabel.Visible =
            ready;
        _slipstreamCueLabel.Text =
            level ==
                Player.SlipstreamCueLevel
                    .Perfect
                ? "PERFECT  DODGE"
                : "DODGE";
        _slipstreamCueLabel.Modulate =
            new Color(
                color.R,
                color.G,
                color.B,
                ready ? 0.94f : 0.0f);
    }

    private void CreateClutchVaultCueVisual()
    {
        if (!IsInstanceValid(_worldRoot))
        {
            return;
        }

        _clutchVaultCueMaterial =
            new StandardMaterial3D
            {
                Transparency =
                    BaseMaterial3D
                        .TransparencyEnum
                        .Alpha,
                ShadingMode =
                    BaseMaterial3D
                        .ShadingModeEnum
                        .Unshaded,
                CullMode =
                    BaseMaterial3D
                        .CullModeEnum
                        .Disabled,
                AlbedoColor =
                    new Color(
                        0.1f,
                        0.92f,
                        0.74f,
                        0.24f),
                EmissionEnabled = true,
                Emission =
                    new Color(
                        0.1f,
                        0.92f,
                        0.74f),
                EmissionEnergyMultiplier =
                    2.2f
            };
        _clutchVaultCueRoot =
            new Node3D
            {
                Name =
                    "ClutchVaultCue",
                Visible = false
            };
        _worldRoot.AddChild(
            _clutchVaultCueRoot);
        _clutchVaultPlayerPrompt =
            new Node3D
            {
                Name =
                    "BlinkPlayerPrompt"
            };
        _clutchVaultCueRoot.AddChild(
            _clutchVaultPlayerPrompt);
        _clutchVaultTargetReticle =
            new Node3D
            {
                Name =
                    "BlinkTargetReticle"
            };
        _clutchVaultCueRoot.AddChild(
            _clutchVaultTargetReticle);

        _clutchVaultPlayerRing =
            new MeshInstance3D
            {
                Name =
                    "BlinkWindowRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.84f,
                    OuterRadius = 1.0f,
                    Rings = 12,
                    RingSegments = 48
                },
                MaterialOverride =
                    _clutchVaultCueMaterial,
                Position =
                    new Vector3(
                        0.0f,
                        0.08f,
                        0.0f),
                CastShadow =
                    GeometryInstance3D
                        .ShadowCastingSetting
                        .Off
            };
        _clutchVaultPlayerPrompt.AddChild(
            _clutchVaultPlayerRing);

        for (int ringIndex = 0;
            ringIndex <
                _clutchVaultTargetRings.Length;
            ringIndex++)
        {
            float radius =
                0.84f +
                ringIndex * 0.24f;
            MeshInstance3D ring =
                new()
                {
                    Name =
                        $"BlinkTargetRing{ringIndex + 1}",
                    Mesh = new TorusMesh
                    {
                        InnerRadius = radius,
                        OuterRadius =
                            radius + 0.12f,
                        Rings = 10,
                        RingSegments = 40
                    },
                    MaterialOverride =
                        _clutchVaultCueMaterial,
                    Position =
                        new Vector3(
                            0.0f,
                            -0.78f +
                                ringIndex * 0.055f,
                            0.0f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            _clutchVaultTargetReticle
                .AddChild(ring);
            _clutchVaultTargetRings[
                ringIndex] = ring;
        }

        for (int sideIndex = 0;
            sideIndex < 2;
            sideIndex++)
        {
            float side =
                sideIndex == 0
                    ? -1.0f
                    : 1.0f;
            MeshInstance3D sideBar =
                new()
                {
                    Name =
                        $"TargetSide{sideIndex + 1}",
                    Mesh = new BoxMesh
                    {
                        Size =
                            new Vector3(
                                0.16f,
                                1.24f,
                                0.14f)
                    },
                    MaterialOverride =
                        _clutchVaultCueMaterial,
                    Position =
                        new Vector3(
                            side * 1.34f,
                            0.0f,
                            0.0f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            _clutchVaultTargetReticle
                .AddChild(sideBar);
            for (int capIndex = 0;
                capIndex < 2;
                capIndex++)
            {
                float capY =
                    capIndex == 0
                        ? -0.39f
                        : 0.39f;
                MeshInstance3D cap =
                    new()
                    {
                        Name =
                            $"TargetCap{sideIndex + 1}_{capIndex + 1}",
                        Mesh = new BoxMesh
                        {
                            Size =
                                new Vector3(
                                    0.52f,
                                    0.16f,
                                    0.14f)
                        },
                        MaterialOverride =
                            _clutchVaultCueMaterial,
                        Position =
                            new Vector3(
                                side * 1.16f,
                                capY,
                                0.0f),
                        CastShadow =
                            GeometryInstance3D
                                .ShadowCastingSetting
                                .Off
                    };
                _clutchVaultTargetReticle
                    .AddChild(cap);
            }

            MeshInstance3D rail =
                new()
                {
                    Name =
                        $"BlinkPhaseRail{sideIndex + 1}",
                    Mesh = new BoxMesh
                    {
                        Size =
                            new Vector3(
                                0.075f,
                                0.06f,
                                1.0f)
                    },
                    MaterialOverride =
                        _clutchVaultCueMaterial,
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            _clutchVaultCueRoot.AddChild(
                rail);
            _clutchVaultLinkRails[
                sideIndex] = rail;
        }

        for (int index = 0;
            index <
                _clutchVaultLinkSegments
                    .Length;
            index++)
        {
            MeshInstance3D segment =
                new()
                {
                    Name =
                        $"BlinkLink{index + 1}",
                    Mesh = new BoxMesh
                    {
                        Size =
                            new Vector3(
                                0.24f,
                                0.1f,
                                0.42f)
                    },
                    MaterialOverride =
                        _clutchVaultCueMaterial,
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            _clutchVaultCueRoot.AddChild(
                segment);
            _clutchVaultLinkSegments[index] =
                segment;
        }

        _clutchVaultCueLabel =
            new Label3D
            {
                Name =
                    "ClutchVaultLabel",
                Text = "JUMP  /  BLINK",
                Position =
                    new Vector3(
                        0.0f,
                        2.58f,
                        0.0f),
                FontSize = 44,
                PixelSize = 0.0125f,
                OutlineSize = 12,
                Billboard =
                    BaseMaterial3D
                        .BillboardModeEnum
                        .Enabled,
                NoDepthTest = true
            };
        _clutchVaultPlayerPrompt.AddChild(
            _clutchVaultCueLabel);
    }

    private void UpdateClutchVaultCueVisual(
        float deltaSeconds)
    {
        if (!IsInstanceValid(
                _clutchVaultCueRoot) ||
            !IsInstanceValid(_player))
        {
            return;
        }

        Mob target =
            _player.ClutchVaultCueTarget;
        bool visible =
            IsInstanceValid(target) &&
            !_player.IsClutchVaultActive;
        _clutchVaultCueRoot.Visible =
            visible;
        if (!visible)
        {
            return;
        }

        _clutchVaultCuePulseTime +=
            deltaSeconds;
        float urgency =
            Mathf.Clamp(
                _player
                    .ClutchVaultCueUrgency,
                0.0f,
                1.0f);
        Color color =
            new Color(
                0.1f,
                0.88f,
                0.78f)
                .Lerp(
                    new Color(
                        0.72f,
                        1.0f,
                        0.28f),
                    urgency);
        Vector3 linkOrigin =
            _player.GlobalPosition +
            Vector3.Up * 1.05f;
        Vector3 linkDestination =
            target.GlobalPosition +
            Vector3.Up * 0.9f;
        _clutchVaultPlayerPrompt
            .GlobalPosition =
            _player.GlobalPosition;
        _clutchVaultTargetReticle
            .GlobalPosition =
            linkDestination;
        Vector3 towardPlayer =
            linkOrigin -
            linkDestination;
        towardPlayer.Y = 0.0f;
        if (towardPlayer.IsZeroApprox())
        {
            towardPlayer =
                Vector3.Back;
        }
        _clutchVaultTargetReticle.LookAt(
            linkDestination +
                towardPlayer.Normalized(),
            Vector3.Up);
        float pulse =
            1.0f +
            Mathf.Sin(
                _clutchVaultCuePulseTime *
                Mathf.Lerp(
                    5.0f,
                    11.0f,
                    urgency)) *
            Mathf.Lerp(
                0.035f,
                0.09f,
                urgency);
        _clutchVaultPlayerPrompt.Scale =
            Vector3.One *
            Mathf.Lerp(
                0.96f,
                pulse,
                0.72f);
        _clutchVaultTargetReticle.Scale =
            Vector3.One * pulse;

        Vector3 railDirection =
            linkDestination -
            linkOrigin;
        float railLength =
            railDirection.Length();
        Vector3 railSide =
            Vector3.Up.Cross(
                railDirection.Normalized());
        if (railSide.IsZeroApprox())
        {
            railSide = Vector3.Right;
        }
        for (int railIndex = 0;
            railIndex <
                _clutchVaultLinkRails.Length;
            railIndex++)
        {
            MeshInstance3D rail =
                _clutchVaultLinkRails[
                    railIndex];
            if (!IsInstanceValid(rail))
            {
                continue;
            }
            float sideSign =
                railIndex == 0
                    ? -1.0f
                    : 1.0f;
            float corridorWidth =
                Mathf.Lerp(
                    0.62f,
                    0.42f,
                    urgency);
            rail.GlobalPosition =
                linkOrigin.Lerp(
                    linkDestination,
                    0.5f) +
                railSide *
                    corridorWidth *
                    sideSign +
                Vector3.Up * 0.08f;
            rail.LookAt(
                linkDestination +
                    railSide *
                    corridorWidth *
                    sideSign,
                Vector3.Up);
            rail.Scale =
                new Vector3(
                    1.0f,
                    1.0f,
                    railLength *
                        Mathf.Lerp(
                            0.76f,
                            0.94f,
                            urgency));
        }
        for (int index = 0;
            index <
                _clutchVaultLinkSegments
                    .Length;
            index++)
        {
            MeshInstance3D segment =
                _clutchVaultLinkSegments[
                    index];
            if (!IsInstanceValid(segment))
            {
                continue;
            }
            float progress =
                Mathf.PosMod(
                    _clutchVaultCuePulseTime *
                        Mathf.Lerp(
                            0.52f,
                            0.92f,
                            urgency) +
                    index /
                        (float)_clutchVaultLinkSegments
                            .Length,
                    1.0f);
            Vector3 position =
                linkOrigin.Lerp(
                    linkDestination,
                    progress) +
                Vector3.Up *
                    Mathf.Sin(
                        progress *
                            Mathf.Pi) *
                    0.2f;
            segment.GlobalPosition =
                position;
            segment.LookAt(
                linkDestination,
                Vector3.Up);
            float flowPulse =
                0.94f +
                Mathf.Max(
                    0.0f,
                    Mathf.Sin(
                        _clutchVaultCuePulseTime *
                            8.0f -
                        index * 1.15f)) *
                    0.42f;
            segment.Scale =
                new Vector3(
                    flowPulse *
                        Mathf.Lerp(
                            0.7f,
                            1.0f,
                            urgency),
                    flowPulse,
                    flowPulse);
        }
        if (IsInstanceValid(
            _clutchVaultPlayerRing))
        {
            float iris =
                Mathf.Lerp(
                    1.14f,
                    0.86f,
                    0.5f +
                        Mathf.Sin(
                            _clutchVaultCuePulseTime *
                                Mathf.Lerp(
                                    4.5f,
                                    8.0f,
                                    urgency)) *
                        0.5f);
            _clutchVaultPlayerRing.Scale =
                new Vector3(
                    iris,
                    1.0f,
                    iris);
        }
        for (int ringIndex = 0;
            ringIndex <
                _clutchVaultTargetRings.Length;
            ringIndex++)
        {
            MeshInstance3D ring =
                _clutchVaultTargetRings[
                    ringIndex];
            if (!IsInstanceValid(ring))
            {
                continue;
            }
            float ringPulse =
                0.9f +
                Mathf.Max(
                    0.0f,
                    Mathf.Sin(
                        _clutchVaultCuePulseTime *
                            7.4f -
                        ringIndex * 1.6f)) *
                    0.24f;
            ring.Scale =
                new Vector3(
                    ringPulse,
                    1.0f,
                    ringPulse);
        }
        _clutchVaultCueMaterial
            .AlbedoColor =
            new Color(
                color.R,
                color.G,
                color.B,
                Mathf.Lerp(
                    0.3f,
                    0.58f,
                    urgency));
        _clutchVaultCueMaterial.Emission =
            color;
        _clutchVaultCueMaterial
            .EmissionEnergyMultiplier =
            Mathf.Lerp(
                3.0f,
                5.2f,
                urgency);
        _clutchVaultCueLabel.Modulate =
            new Color(
                color.R,
                color.G,
                color.B,
                Mathf.Lerp(
                    0.78f,
                    1.0f,
                    urgency));
        _clutchVaultCueLabel.Scale =
            Vector3.One *
            Mathf.Lerp(
                0.94f,
                1.08f,
                urgency);
    }

    private void PrecacheImpactMaterials(Color color)
    {
        GetEffectMaterial(color, 2.4f);
        GetEffectMaterial(color, 3.8f);
        GetEffectMaterial(
            color.Lerp(
                Colors.White,
                0.12f),
            2.0f);
        GetEffectMaterial(
            color.Lerp(
                Colors.White,
                0.22f),
            3.4f);
    }

    private void CreateImpactBurstPool()
    {
        if (!IsInstanceValid(_worldRoot))
        {
            return;
        }

        StandardMaterial3D initialRingMaterial =
            GetEffectMaterial(
                GetTierColor(
                    Player.ComboFlowTier.Warmup),
                2.4f);
        StandardMaterial3D initialShardMaterial =
            GetEffectMaterial(
                GetTierColor(
                    Player.ComboFlowTier.Warmup)
                    .Lerp(
                        Colors.White,
                        0.12f),
                2.0f);
        for (int poolIndex = 0;
            poolIndex < _impactBurstPool.Length;
            poolIndex++)
        {
            Node3D root = new()
            {
                Name =
                    $"ComboImpactBurst{poolIndex + 1}",
                Scale = Vector3.One * 0.25f,
                Visible = false
            };
            _worldRoot.AddChild(root);
            MeshInstance3D ring = new()
            {
                Name = "Ring",
                Mesh = _impactRingMesh,
                MaterialOverride =
                    initialRingMaterial
            };
            root.AddChild(ring);
            MeshInstance3D[] shards =
                new MeshInstance3D[
                    MaximumImpactShards];
            for (int shardIndex = 0;
                shardIndex < shards.Length;
                shardIndex++)
            {
                MeshInstance3D shard = new()
                {
                    Name =
                        $"Shard{shardIndex + 1}",
                    Mesh = _impactShardMesh,
                    MaterialOverride =
                        initialShardMaterial,
                    Visible = false
                };
                root.AddChild(shard);
                shards[shardIndex] = shard;
            }
            _impactBurstPool[poolIndex] =
                new ImpactBurstSlot
                {
                    Root = root,
                    Ring = ring,
                    Shards = shards
                };
        }
    }

    private void CreateImpactBurst(
        Vector3 worldPosition,
        Color color,
        float strength,
        bool finisher,
        Vector3 travelDirection = default)
    {
        if (!IsInstanceValid(_worldRoot) ||
            _player.EffectsQuality <= 0 ||
            _impactBurstPool.Length == 0)
        {
            return;
        }

        ImpactBurstSlot slot =
            _impactBurstPool[
                _nextImpactBurstSlot];
        _nextImpactBurstSlot =
            (_nextImpactBurstSlot + 1) %
            _impactBurstPool.Length;
        if (slot == null ||
            !IsInstanceValid(slot.Root))
        {
            return;
        }
        if (slot.ActiveTween != null &&
            slot.ActiveTween.IsValid())
        {
            slot.ActiveTween.Kill();
        }

        Node3D root = slot.Root;
        MeshInstance3D ring = slot.Ring;
        root.Show();
        root.Scale =
            Vector3.One * 0.25f;
        root.Rotation = Vector3.Zero;
        root.GlobalPosition = worldPosition +
            Vector3.Up * 0.85f;

        StandardMaterial3D ringMaterial =
            GetEffectMaterial(
                color,
                finisher ? 3.8f : 2.4f);
        ring.Mesh = _impactRingMesh;
        ring.MaterialOverride = ringMaterial;
        ring.Transparency = 0.0f;
        ring.Show();

        int shardCount = _player.EffectsQuality >= 3
            ? finisher ? 12 : 7
            : finisher ? 7 : 4;
        Tween tween = root.CreateTween()
            .SetParallel(true)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        slot.ActiveTween = tween;
        tween.TweenProperty(
            root,
            "scale",
            Vector3.One *
                (1.4f + strength * 0.55f),
            finisher ? 0.42f : 0.28f);
        tween.TweenProperty(
            ring,
            "transparency",
            1.0f,
            finisher ? 0.48f : 0.34f);
        StandardMaterial3D shardMaterial =
            GetEffectMaterial(
                color.Lerp(
                    Colors.White,
                    finisher ? 0.22f : 0.12f),
                finisher ? 3.4f : 2.0f);
        travelDirection.Y = 0.0f;
        bool directional =
            travelDirection.LengthSquared() >
                0.001f;
        Vector3 forward = directional
            ? travelDirection.Normalized()
            : Vector3.Zero;
        Vector3 side = directional
            ? Vector3.Up.Cross(forward)
                .Normalized()
            : Vector3.Zero;

        for (int index = 0;
            index < slot.Shards.Length;
            index++)
        {
            MeshInstance3D shard =
                slot.Shards[index];
            if (index >= shardCount)
            {
                shard.Hide();
                continue;
            }

            Vector3 direction;
            if (directional)
            {
                float lane =
                    shardCount <= 1
                        ? 0.0f
                        : index /
                            (shardCount - 1.0f) *
                            2.0f -
                            1.0f;
                direction =
                    forward *
                        (float)GD.RandRange(
                            0.86f,
                            1.18f) +
                    side * lane *
                        (float)GD.RandRange(
                            0.18f,
                            0.48f) +
                    Vector3.Up *
                        (float)GD.RandRange(
                            0.12f,
                            0.54f);
                direction =
                    direction.Normalized();
            }
            else
            {
                float angle =
                    Mathf.Tau * index /
                        shardCount +
                    (float)GD.RandRange(
                        -0.16f,
                        0.16f);
                direction = new Vector3(
                    Mathf.Cos(angle),
                    (float)GD.RandRange(
                        0.18f,
                        0.72f),
                    Mathf.Sin(angle))
                    .Normalized();
            }
            shard.Show();
            shard.Mesh = finisher
                ? _impactFinisherShardMesh
                : _impactShardMesh;
            shard.MaterialOverride =
                shardMaterial;
            shard.Position = Vector3.Zero;
            shard.Scale = Vector3.One;
            shard.Transparency = 0.0f;
            shard.LookAtFromPosition(
                Vector3.Zero,
                direction,
                Vector3.Up);
            tween.TweenProperty(
                shard,
                "position",
                direction *
                    (finisher
                        ? 4.4f
                        : 2.2f + strength),
                finisher ? 0.44f : 0.3f);
            tween.TweenProperty(
                shard,
                "scale",
                Vector3.Zero,
                finisher ? 0.5f : 0.36f);
        }

        tween.Chain().TweenCallback(
            Callable.From(slot.Hide));
    }

    private void CreateRiftTrace(
        Vector3 origin,
        Vector3 destination,
        Color color)
    {
        if (!IsInstanceValid(_worldRoot) ||
            _player.EffectsQuality <= 0)
        {
            return;
        }

        Vector3 travel =
            destination - origin;
        float length = travel.Length();
        if (length <= 0.2f)
        {
            return;
        }

        MeshInstance3D trace =
            new()
            {
                Name = "RiftAfterimage",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(
                        0.42f,
                        0.26f,
                        length)
                },
                MaterialOverride =
                    GetEffectMaterial(
                        color.Lerp(
                            Colors.White,
                            0.18f),
                        6.2f),
                CastShadow =
                    GeometryInstance3D
                        .ShadowCastingSetting
                        .Off,
                Transparency = 0.0f
            };
        _worldRoot.AddChild(trace);
        trace.GlobalPosition =
            origin.Lerp(
                destination,
                0.5f);
        trace.LookAt(
            destination,
            Vector3.Up);
        trace.Scale =
            new Vector3(
                1.0f,
                1.0f,
                0.96f);
        Tween tween = trace.CreateTween()
            .SetParallel(true)
            .SetTrans(
                Tween.TransitionType.Expo)
            .SetEase(
                Tween.EaseType.Out);
        tween.TweenProperty(
            trace,
            "scale",
            new Vector3(
                0.02f,
                0.08f,
                1.0f),
            0.28f);
        tween.TweenProperty(
            trace,
            "transparency",
            1.0f,
            0.36f);
        tween.Chain().TweenCallback(
            Callable.From(
                trace.QueueFree));

        CreateRiftEchoes(
            origin,
            destination,
            color);
    }

    private void CreateRiftGate(
        Vector3 position,
        Vector3 facing,
        Color color,
        bool arrival)
    {
        if (!IsInstanceValid(_worldRoot) ||
            _player.EffectsQuality <= 0)
        {
            return;
        }

        facing.Y = 0.0f;
        if (facing.IsZeroApprox())
        {
            facing = Vector3.Forward;
        }

        Node3D gate = new()
        {
            Name = arrival
                ? "RiftArrivalGate"
                : "RiftDepartureGate"
        };
        _worldRoot.AddChild(gate);
        gate.GlobalPosition = position;
        gate.LookAt(
            position + facing.Normalized(),
            Vector3.Up);

        Tween tween = gate.CreateTween()
            .SetParallel(true)
            .SetTrans(
                Tween.TransitionType.Expo)
            .SetEase(
                arrival
                    ? Tween.EaseType.In
                    : Tween.EaseType.Out);
        int ringCount =
            _player.EffectsQuality >= 2
                ? 2
                : 1;
        for (int index = 0;
            index < ringCount;
            index++)
        {
            float radius =
                0.72f + index * 0.28f;
            Color ringColor =
                index == 0
                    ? color
                    : color.Lerp(
                        Colors.White,
                        0.42f);
            MeshInstance3D ring =
                new()
                {
                    Name =
                        $"GateRing{index + 1}",
                    Mesh = new TorusMesh
                    {
                        InnerRadius = radius,
                        OuterRadius =
                            radius + 0.12f,
                        Rings = 8,
                        RingSegments = 32
                    },
                    MaterialOverride =
                        GetEffectMaterial(
                            ringColor,
                            5.4f -
                                index * 0.8f),
                    RotationDegrees =
                        new Vector3(
                            90.0f,
                            0.0f,
                            index * 18.0f),
                    Scale = Vector3.One *
                        (arrival
                            ? 1.38f
                            : 0.28f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            gate.AddChild(ring);
            tween.TweenProperty(
                ring,
                "scale",
                Vector3.One *
                    (arrival
                        ? 0.08f
                        : 1.4f +
                            index * 0.12f),
                arrival ? 0.32f : 0.38f);
            tween.TweenProperty(
                ring,
                "transparency",
                1.0f,
                arrival ? 0.35f : 0.42f);
        }

        MeshInstance3D core =
            new()
            {
                Name = "GateCore",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.7f,
                    BottomRadius = 0.7f,
                    Height = 0.025f,
                    RadialSegments = 32
                },
                MaterialOverride =
                    GetEffectMaterial(
                        new Color(
                            color.R,
                            color.G,
                            color.B,
                            0.2f),
                        3.8f),
                RotationDegrees =
                    new Vector3(
                        90.0f,
                        0.0f,
                        0.0f),
                Scale = Vector3.One *
                    (arrival ? 1.18f : 0.2f),
                Transparency = 0.15f,
                CastShadow =
                    GeometryInstance3D
                        .ShadowCastingSetting
                        .Off
            };
        gate.AddChild(core);
        tween.TweenProperty(
            core,
            "scale",
            Vector3.One *
                (arrival ? 0.04f : 1.18f),
            arrival ? 0.28f : 0.34f);
        tween.TweenProperty(
            core,
            "transparency",
            1.0f,
            arrival ? 0.31f : 0.38f);
        tween.Chain().TweenCallback(
            Callable.From(
                gate.QueueFree));
    }

    private void CreateRiftEchoes(
        Vector3 origin,
        Vector3 destination,
        Color color)
    {
        int echoCount =
            _player.EffectsQuality >= 3
                ? 6
                : _player.EffectsQuality >= 2
                    ? 4
                    : 2;
        Node3D echoRoot = new()
        {
            Name = "RiftPhaseEchoes"
        };
        _worldRoot.AddChild(echoRoot);
        SphereMesh echoMesh = new()
        {
            Radius = 0.42f,
            Height = 0.9f,
            RadialSegments = 12,
            Rings = 6
        };
        Tween tween = echoRoot.CreateTween()
            .SetParallel(true)
            .SetTrans(
                Tween.TransitionType.Expo)
            .SetEase(
                Tween.EaseType.Out);
        for (int index = 0;
            index < echoCount;
            index++)
        {
            float progress =
                (index + 1.0f) /
                (echoCount + 1.0f);
            Color echoColor =
                color.Lerp(
                    new Color(
                        0.12f,
                        1.0f,
                        0.9f),
                    progress * 0.74f);
            MeshInstance3D echo =
                new()
                {
                    Name =
                        $"PhaseEcho{index + 1}",
                    Mesh = echoMesh,
                    MaterialOverride =
                        GetEffectMaterial(
                            echoColor,
                            5.0f),
                    Scale =
                        new Vector3(
                            0.72f,
                            1.08f,
                            0.1f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            echoRoot.AddChild(echo);
            echo.GlobalPosition =
                origin.Lerp(
                    destination,
                    progress) +
                Vector3.Up * 0.18f;
            echo.LookAt(
                destination,
                Vector3.Up);
            tween.TweenProperty(
                echo,
                "scale",
                new Vector3(
                    0.08f,
                    0.34f,
                    0.03f),
                0.28f +
                    index * 0.018f);
            tween.TweenProperty(
                echo,
                "transparency",
                1.0f,
                0.34f +
                    index * 0.018f);
        }
        tween.Chain().TweenCallback(
            Callable.From(
                echoRoot.QueueFree));
    }

    private void CreateGroundBurstWake(
        Vector3 origin,
        Vector3 destination,
        Color color)
    {
        Vector3 travel =
            destination - origin;
        travel.Y = 0.0f;
        float length = travel.Length();
        if (!IsInstanceValid(_worldRoot) ||
            _player.EffectsQuality <= 0 ||
            length <= 0.2f)
        {
            return;
        }

        Vector3 forward =
            travel / length;
        Vector3 side =
            Vector3.Up.Cross(forward)
                .Normalized();
        Node3D wakeRoot = new()
        {
            Name = "GroundBurstWake"
        };
        _worldRoot.AddChild(wakeRoot);
        Tween tween = wakeRoot.CreateTween()
            .SetParallel(true)
            .SetTrans(
                Tween.TransitionType.Expo)
            .SetEase(
                Tween.EaseType.Out);
        int laneCount =
            _player.EffectsQuality >= 2
                ? 3
                : 1;
        for (int index = 0;
            index < laneCount;
            index++)
        {
            float lane =
                laneCount == 1
                    ? 0.0f
                    : index - 1.0f;
            MeshInstance3D streak =
                new()
                {
                    Name =
                        $"WakeStreak{index + 1}",
                    Mesh = new BoxMesh
                    {
                        Size = new Vector3(
                            lane == 0.0f
                                ? 0.3f
                                : 0.12f,
                            0.075f,
                            length *
                                (lane == 0.0f
                                    ? 0.96f
                                    : 0.78f))
                    },
                    MaterialOverride =
                        GetEffectMaterial(
                            lane == 0.0f
                                ? color.Lerp(
                                    Colors.White,
                                    0.34f)
                                : color,
                            lane == 0.0f
                                ? 6.6f
                                : 5.0f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            wakeRoot.AddChild(streak);
            streak.GlobalPosition =
                origin +
                travel * 0.45f +
                side * lane * 0.34f +
                Vector3.Up * 0.12f;
            streak.LookAt(
                destination +
                    side * lane * 0.34f,
                Vector3.Up);
            tween.TweenProperty(
                streak,
                "scale",
                new Vector3(
                    0.04f,
                    0.1f,
                    0.68f),
                0.28f);
            tween.TweenProperty(
                streak,
                "transparency",
                1.0f,
                0.36f);
        }

        int pulseCount =
            _player.EffectsQuality >= 2
                ? 3
                : 1;
        for (int index = 0;
            index < pulseCount;
            index++)
        {
            float progress =
                (index + 0.6f) /
                (pulseCount + 0.4f);
            MeshInstance3D pulse =
                new()
                {
                    Name =
                        $"WakePulse{index + 1}",
                    Mesh = new TorusMesh
                    {
                        InnerRadius = 0.34f,
                        OuterRadius = 0.45f,
                        Rings = 7,
                        RingSegments = 24
                    },
                    MaterialOverride =
                        GetEffectMaterial(
                            color.Lerp(
                                Colors.White,
                                progress * 0.28f),
                            5.2f),
                    Scale =
                        new Vector3(
                            0.82f,
                            0.82f,
                            1.18f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            wakeRoot.AddChild(pulse);
            pulse.GlobalPosition =
                origin +
                travel * progress +
                Vector3.Up * 0.09f;
            tween.TweenProperty(
                pulse,
                "scale",
                new Vector3(
                    0.08f,
                    0.08f,
                    0.18f),
                0.3f +
                    index * 0.025f);
            tween.TweenProperty(
                pulse,
                "transparency",
                1.0f,
                0.34f +
                    index * 0.025f);
        }
        tween.Chain().TweenCallback(
            Callable.From(
                wakeRoot.QueueFree));
    }

    private void CreateClutchBlinkTrace(
        Vector3 origin,
        Vector3 destination,
        Color color)
    {
        if (!IsInstanceValid(_worldRoot) ||
            _player.EffectsQuality <= 0)
        {
            return;
        }

        Vector3 travel =
            destination -
            origin;
        float travelLength =
            travel.Length();
        if (travelLength <= 0.2f)
        {
            return;
        }

        Vector3 planarForward =
            travel;
        planarForward.Y = 0.0f;
        if (planarForward.IsZeroApprox())
        {
            planarForward =
                Vector3.Forward;
        }
        Vector3 side =
            Vector3.Up.Cross(
                planarForward
                    .Normalized());
        Node3D traceRoot =
            new()
            {
                Name =
                    "ClutchBlinkTrace"
            };
        _worldRoot.AddChild(
            traceRoot);
        Tween tween =
            traceRoot.CreateTween()
                .SetParallel(true)
                .SetTrans(
                    Tween.TransitionType
                        .Expo)
                .SetEase(
                    Tween.EaseType.Out);
        for (int railIndex = -1;
            railIndex <= 1;
            railIndex++)
        {
            Vector3 railOffset =
                side *
                railIndex *
                0.18f;
            MeshInstance3D phaseRail =
                new()
                {
                    Name =
                        $"BlinkPhaseRibbon{railIndex + 2}",
                    Mesh = new BoxMesh
                    {
                        Size =
                            new Vector3(
                                railIndex == 0
                                    ? 0.08f
                                    : 0.045f,
                                railIndex == 0
                                    ? 0.1f
                                    : 0.055f,
                                travelLength * 0.94f)
                    },
                    MaterialOverride =
                        GetEffectMaterial(
                            railIndex == 0
                                ? color.Lerp(
                                    Colors.White,
                                    0.34f)
                                : color,
                            railIndex == 0
                                ? 7.0f
                                : 5.4f),
                    Scale =
                        new Vector3(
                            1.0f,
                            1.0f,
                            0.08f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            traceRoot.AddChild(
                phaseRail);
            phaseRail.GlobalPosition =
                origin.Lerp(
                    destination,
                    0.5f) +
                railOffset +
                Vector3.Up * 0.12f;
            phaseRail.LookAt(
                destination +
                    railOffset,
                Vector3.Up);
            tween.TweenProperty(
                phaseRail,
                "scale:z",
                1.0f,
                0.1f +
                    Mathf.Abs(railIndex) *
                    0.018f);
            tween.TweenProperty(
                phaseRail,
                "transparency",
                1.0f,
                0.32f);
        }
        int segmentCount =
            _player.EffectsQuality >= 2
                ? 6
                : 3;
        for (int index = 0;
            index < segmentCount;
            index++)
        {
            float startProgress =
                (index + 0.08f) /
                (float)segmentCount;
            float endProgress =
                (index + 0.66f) /
                segmentCount;
            float sideOffset =
                (index % 2 == 0
                    ? -1.0f
                    : 1.0f) *
                0.09f *
                (1.0f -
                    startProgress);
            Vector3 start =
                origin.Lerp(
                    destination,
                    startProgress) +
                side * sideOffset;
            Vector3 end =
                origin.Lerp(
                    destination,
                    endProgress) +
                side * sideOffset;
            Vector3 segment =
                end -
                start;
            float length =
                segment.Length();
            if (length <= 0.04f)
            {
                continue;
            }

            MeshInstance3D streak =
                new()
                {
                    Name =
                        $"BlinkTraceSegment{index + 1}",
                    Mesh = new BoxMesh
                    {
                        Size =
                            new Vector3(
                                index % 2 == 0
                                    ? 0.13f
                                    : 0.09f,
                                0.07f,
                                length)
                    },
                    MaterialOverride =
                        GetEffectMaterial(
                            color.Lerp(
                                Colors.White,
                                index /
                                    (float)segmentCount *
                                    0.38f),
                            5.8f),
                    CastShadow =
                        GeometryInstance3D
                            .ShadowCastingSetting
                            .Off
                };
            traceRoot.AddChild(streak);
            streak.GlobalPosition =
                start.Lerp(
                    end,
                    0.5f);
            streak.LookAt(
                end,
                Vector3.Up);
            tween.TweenProperty(
                streak,
                "scale",
                new Vector3(
                    0.08f,
                    0.08f,
                    0.72f),
                0.3f +
                    index * 0.012f);
            tween.TweenProperty(
                streak,
                "transparency",
                1.0f,
                0.26f +
                    index * 0.014f);
        }
        tween.Chain().TweenCallback(
            Callable.From(
                traceRoot.QueueFree));
    }

    private void CreateClutchBlinkPhaseGates(
        Vector3 origin,
        Vector3 destination,
        Color color)
    {
        if (!IsInstanceValid(_worldRoot) ||
            !IsInstanceValid(_player) ||
            _player.EffectsQuality <= 0)
        {
            return;
        }

        Vector3[] positions =
        {
            origin + Vector3.Up * 0.86f,
            destination + Vector3.Up * 0.82f
        };
        for (int gateIndex = 0;
            gateIndex < positions.Length;
            gateIndex++)
        {
            bool arrival =
                gateIndex == 1;
            Node3D gate =
                new()
                {
                    Name = arrival
                        ? "ClutchBlinkReassembly"
                        : "ClutchBlinkCompression"
                };
            _worldRoot.AddChild(gate);
            gate.GlobalPosition =
                positions[gateIndex];

            Tween tween =
                gate.CreateTween()
                    .SetParallel(true)
                    .SetTrans(
                        Tween.TransitionType
                            .Expo)
                    .SetEase(
                        arrival
                            ? Tween.EaseType.Out
                            : Tween.EaseType.In);
            int ringCount =
                _player.EffectsQuality >= 2
                    ? 3
                    : 2;
            for (int ringIndex = 0;
                ringIndex < ringCount;
                ringIndex++)
            {
                float radius =
                    0.62f +
                    ringIndex * 0.24f;
                MeshInstance3D ring =
                    new()
                    {
                        Name =
                            $"PhaseRing{ringIndex + 1}",
                        Position =
                            Vector3.Up *
                            (ringIndex - 1) *
                            0.42f,
                        Mesh = new TorusMesh
                        {
                            InnerRadius =
                                radius,
                            OuterRadius =
                                radius +
                                0.1f,
                            Rings = 10,
                            RingSegments = 40
                        },
                        MaterialOverride =
                            GetEffectMaterial(
                                color.Lerp(
                                    Colors.White,
                                    ringIndex *
                                        0.16f),
                                6.8f -
                                    ringIndex *
                                    0.7f),
                        Scale =
                            Vector3.One *
                            (arrival
                                ? 0.08f
                                : 1.42f +
                                    ringIndex *
                                    0.1f),
                        CastShadow =
                            GeometryInstance3D
                                .ShadowCastingSetting
                                .Off
                    };
                gate.AddChild(ring);
                tween.TweenProperty(
                    ring,
                    "scale",
                    Vector3.One *
                        (arrival
                            ? 1.36f +
                                ringIndex *
                                0.1f
                            : 0.04f),
                    arrival
                        ? 0.24f
                        : 0.16f);
                tween.TweenProperty(
                    ring,
                    "position:y",
                    ring.Position.Y +
                        (arrival
                            ? 0.34f
                            : -0.18f),
                    0.26f);
                tween.TweenProperty(
                    ring,
                    "transparency",
                    1.0f,
                    arrival
                        ? 0.34f
                        : 0.22f);
            }

            int shardCount =
                _player.EffectsQuality >= 2
                    ? 10
                    : 5;
            for (int shardIndex = 0;
                shardIndex < shardCount;
                shardIndex++)
            {
                float angle =
                    Mathf.Tau *
                    shardIndex /
                    shardCount;
                Vector3 radial =
                    new Vector3(
                        Mathf.Cos(angle),
                        Mathf.Sin(
                            angle * 2.0f) *
                            0.32f,
                        Mathf.Sin(angle));
                Vector3 startOffset =
                    radial *
                    (arrival
                        ? 0.18f
                        : 1.35f);
                Vector3 endOffset =
                    radial *
                    (arrival
                        ? 1.58f
                        : 0.04f);
                MeshInstance3D shard =
                    new()
                    {
                        Name =
                            $"PhaseSpark{shardIndex + 1}",
                        Position =
                            startOffset,
                        Mesh = new BoxMesh
                        {
                            Size =
                                new Vector3(
                                    0.075f,
                                    0.075f,
                                    0.34f)
                        },
                        MaterialOverride =
                            GetEffectMaterial(
                                color.Lerp(
                                    Colors.White,
                                    0.28f),
                                7.2f),
                        CastShadow =
                            GeometryInstance3D
                                .ShadowCastingSetting
                                .Off
                    };
                gate.AddChild(shard);
                shard.LookAt(
                    gate.GlobalPosition +
                        endOffset,
                    Vector3.Up);
                tween.TweenProperty(
                    shard,
                    "position",
                    endOffset,
                    arrival
                        ? 0.3f
                        : 0.14f);
                tween.TweenProperty(
                    shard,
                    "scale",
                    arrival
                        ? new Vector3(
                            0.28f,
                            0.28f,
                            1.65f)
                        : Vector3.One *
                            0.12f,
                    arrival
                        ? 0.24f
                        : 0.14f);
                tween.TweenProperty(
                    shard,
                    "transparency",
                    1.0f,
                    arrival
                        ? 0.38f
                        : 0.2f);
            }

            tween.Chain().TweenCallback(
                Callable.From(
                    gate.QueueFree));
        }
    }

    private StandardMaterial3D GetEffectMaterial(
        Color color,
        float emissionEnergy)
    {
        ulong key =
            ((ulong)PackColor(color) << 16) |
            (uint)Mathf.Clamp(
                Mathf.RoundToInt(
                    emissionEnergy * 100.0f),
                0,
                ushort.MaxValue);
        if (_effectMaterialCache.TryGetValue(
            key,
            out StandardMaterial3D cached))
        {
            return cached;
        }

        StandardMaterial3D material = new()
        {
            Transparency =
                BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode =
                BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = emissionEnergy
        };
        _effectMaterialCache[key] = material;
        return material;
    }

    private static uint PackColor(Color color)
    {
        uint red = (uint)Mathf.Clamp(
            Mathf.RoundToInt(color.R * 255.0f),
            0,
            255);
        uint green = (uint)Mathf.Clamp(
            Mathf.RoundToInt(color.G * 255.0f),
            0,
            255);
        uint blue = (uint)Mathf.Clamp(
            Mathf.RoundToInt(color.B * 255.0f),
            0,
            255);
        uint alpha = (uint)Mathf.Clamp(
            Mathf.RoundToInt(color.A * 255.0f),
            0,
            255);
        return
            (red << 24) |
            (green << 16) |
            (blue << 8) |
            alpha;
    }

    private void FlashScreen(Color color, float alpha)
    {
        if (!IsInstanceValid(_impactFlash))
        {
            return;
        }

        _impactFlash.Color = new Color(
            color.R,
            color.G,
            color.B,
            Mathf.Clamp(alpha, 0.0f, 0.32f));
        Tween tween = _impactFlash.CreateTween();
        tween.TweenProperty(
            _impactFlash,
            "color:a",
            0.0f,
            0.14f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private void PlayImpactTone(
        int chainCount,
        Player.ComboFlowTier tier,
        bool counter,
        bool finisher)
    {
        if (!IsInstanceValid(_impactAudio))
        {
            return;
        }

        int toneIndex = Mathf.PosMod(
            chainCount - 1,
            _comboTones.Length);
        _impactAudio.Stream = _comboTones[toneIndex];
        _impactAudio.PitchScale =
            0.92f +
            (int)tier * 0.055f +
            (counter ? 0.07f : 0.0f);
        _impactAudio.VolumeDb =
            finisher ? -2.0f : counter ? -5.0f : -8.0f;
        _impactAudio.Play();
    }

    private void PlayRiftStepTone(
        bool teleported)
    {
        if (!IsInstanceValid(_vitalsAudio))
        {
            return;
        }

        _vitalsAudio.Stream =
            _comboTones[teleported ? 7 : 4];
        _vitalsAudio.PitchScale =
            teleported ? 1.34f : 1.08f;
        _vitalsAudio.VolumeDb =
            teleported ? -2.5f : -6.5f;
        _vitalsAudio.Play();
    }

    private void PlaySlipstreamTone(
        bool perfectDodge)
    {
        if (!IsInstanceValid(_vitalsAudio))
        {
            return;
        }

        _vitalsAudio.Stream =
            _comboTones[
                perfectDodge ? 7 : 6];
        _vitalsAudio.PitchScale =
            perfectDodge ? 1.48f : 1.22f;
        _vitalsAudio.VolumeDb =
            perfectDodge ? -2.0f : -4.5f;
        _vitalsAudio.Play();
    }

    private void PlayCriticalTone(
        float healthRatio)
    {
        if (!IsInstanceValid(_vitalsAudio))
        {
            return;
        }

        _vitalsAudio.Stream =
            _comboTones[0];
        _vitalsAudio.PitchScale =
            Mathf.Lerp(
                0.68f,
                0.88f,
                healthRatio / 0.3f);
        _vitalsAudio.VolumeDb = -6.0f;
        _vitalsAudio.Play();
    }

    private static AudioStreamWav CreateImpactTone(
        float frequency,
        float duration)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.Max(
            1,
            Mathf.RoundToInt(sampleRate * duration));
        byte[] data = new byte[sampleCount * 2];
        for (int index = 0;
            index < sampleCount;
            index++)
        {
            float time = index / (float)sampleRate;
            float progress =
                index / (float)Mathf.Max(1, sampleCount - 1);
            float envelope =
                Mathf.Pow(1.0f - progress, 2.1f) *
                Mathf.Min(1.0f, progress * 18.0f);
            float signal =
                Mathf.Sin(Mathf.Tau * frequency * time) *
                    0.68f +
                Mathf.Sin(
                    Mathf.Tau *
                    frequency * 2.0f *
                    time) *
                    0.2f;
            short sample = (short)Mathf.Clamp(
                signal * envelope * short.MaxValue,
                short.MinValue,
                short.MaxValue);
            data[index * 2] =
                (byte)(sample & 0xff);
            data[index * 2 + 1] =
                (byte)((sample >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            Data = data
        };
    }

    private static Color GetTierColor(
        Player.ComboFlowTier tier)
    {
        return tier switch
        {
            Player.ComboFlowTier.Rhythm =>
                new Color(0.28f, 0.92f, 1.0f),
            Player.ComboFlowTier.Dominant =>
                new Color(1.0f, 0.72f, 0.16f),
            Player.ComboFlowTier.Legendary =>
                new Color(1.0f, 0.28f, 0.82f),
            _ => new Color(0.84f, 1.0f, 0.48f)
        };
    }

    private static string GetMilestoneName(
        Player.ComboFlowTier tier)
    {
        return tier switch
        {
            Player.ComboFlowTier.Rhythm => "FLOW",
            Player.ComboFlowTier.Dominant => "DOMINANT",
            Player.ComboFlowTier.Legendary => "UNTOUCHABLE",
            _ => "ROUTE FOUND"
        };
    }
}
