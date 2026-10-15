using Godot;
using System.Collections.Generic;

/// <summary>
/// Persistent adaptive score director.
///
/// PATHRAVOR owns the refuge. TROPICANTE and TOXITECH alternate during a
/// run, while their stems respond to momentum and combo flow. BLOOMYHRT is
/// reserved for arenas and opens its arrangement as the waves escalate.
/// Every stem in a song starts on the same audio frame and always keeps a
/// pitch scale of one, so intensity changes never damage musical timing.
/// </summary>
public partial class MusicPlayer : AudioStreamPlayer
{
    private enum ScoreMode
    {
        Silent,
        Refuge,
        Run,
        Arena,
        Defeat
    }

    private enum StemRole
    {
        Lead,
        Backing,
        Drums,
        Bass,
        Percussion,
        Guitar,
        Synth,
        Other,
        Brass
    }

    private sealed class StemLayer
    {
        public int Index;
        public AudioStream Stream;
        public StemRole Role;
        public float Gain;
    }

    private sealed class StemGroup
    {
        public string Name;
        public AudioStreamPlayer Player;
        public AudioStreamSynchronized Synchronized;
        public readonly List<StemLayer> Layers = new();
        public float Length;

        public bool IsPlaying =>
            IsInstanceValid(Player) &&
            Player.Playing;

        public float PlaybackPosition =>
            IsInstanceValid(Player)
                ? Player
                    .GetPlaybackPosition()
                : 0.0f;
    }

    private readonly struct StemDefinition
    {
        public readonly string File;
        public readonly StemRole Role;

        public StemDefinition(
            string file,
            StemRole role)
        {
            File = file;
            Role = role;
        }
    }

    private const string SettingsPath =
        "user://display.cfg";
    private const string AudioSection = "audio";
    private const float SilentDb = -80.0f;
    private const float RegularCrossfadeDuration =
        6.0f;

    private ScoreMode _mode =
        ScoreMode.Silent;
    private StemGroup _tropicante;
    private StemGroup _toxitech;
    private StemGroup _bloomyhrt;
    private StemGroup _regularCurrent;
    private StemGroup _regularNext;
    private bool _regularTransitioning;
    private float _regularTransition;
    private float _hubGain;
    private float _hubTarget;
    private float _regularGain;
    private float _regularTarget;
    private float _arenaGain;
    private float _arenaTarget;
    private float _requestedIntensity;
    private float _smoothedIntensity;
    private float _comboAccent;
    private float _arenaWavePressure = 0.35f;
    private bool _arenaActive;
    private RunRouteStyle _routeStyle =
        RunRouteStyle.Neutral;
    private Vector3 _routeMood;
    private Vector3 _routeMoodTarget;

    public RunRouteStyle CurrentRouteStyle =>
        _routeStyle;

    public override void _EnterTree()
    {
        ProcessMode =
            ProcessModeEnum.Always;
        EnsureMusicBus(this);
        ApplySavedVolumes();
    }

    public override void _Ready()
    {
        Bus = "Music";
        PitchScale = 1.0f;
        ConfigureLoopingStream();
        VolumeDb = SilentDb;

        _tropicante = CreateStemGroup(
            "TROPICANTE",
            "res://Music/TROPICANTE ogg",
            new[]
            {
                new StemDefinition(
                    "0 Lead Vocals.ogg",
                    StemRole.Lead),
                new StemDefinition(
                    "1 Backing Vocals.ogg",
                    StemRole.Backing),
                new StemDefinition(
                    "2 Drums.ogg",
                    StemRole.Drums),
                new StemDefinition(
                    "3 Bass.ogg",
                    StemRole.Bass),
                new StemDefinition(
                    "4 Percussion.ogg",
                    StemRole.Percussion),
                new StemDefinition(
                    "5 Synth.ogg",
                    StemRole.Synth),
                new StemDefinition(
                    "6 Other.ogg",
                    StemRole.Other),
                new StemDefinition(
                    "7 Brass.ogg",
                    StemRole.Brass)
            });
        _toxitech = CreateStemGroup(
            "TOXITECH",
            "res://Music/TOXITECH ogg",
            new[]
            {
                new StemDefinition(
                    "0 Lead Vocals.ogg",
                    StemRole.Lead),
                new StemDefinition(
                    "1 Backing Vocals.ogg",
                    StemRole.Backing),
                new StemDefinition(
                    "2 Drums.ogg",
                    StemRole.Drums),
                new StemDefinition(
                    "3 Bass.ogg",
                    StemRole.Bass),
                new StemDefinition(
                    "4 Percussion.ogg",
                    StemRole.Percussion),
                new StemDefinition(
                    "5 Synth.ogg",
                    StemRole.Synth),
                new StemDefinition(
                    "6 Other.ogg",
                    StemRole.Other)
            });
        _bloomyhrt = CreateStemGroup(
            "BLOOMYHRT",
            "res://Music/BLOOMYHRT ogg",
            new[]
            {
                new StemDefinition(
                    "0 Lead Vocals.ogg",
                    StemRole.Lead),
                new StemDefinition(
                    "1 Backing Vocals.ogg",
                    StemRole.Backing),
                new StemDefinition(
                    "2 Drums.ogg",
                    StemRole.Drums),
                new StemDefinition(
                    "3 Bass.ogg",
                    StemRole.Bass),
                new StemDefinition(
                    "4 Guitar.ogg",
                    StemRole.Guitar),
                new StemDefinition(
                    "5 Synth.ogg",
                    StemRole.Synth),
                new StemDefinition(
                    "6 Other.ogg",
                    StemRole.Other)
            });
    }

    public override void _Process(double delta)
    {
        float deltaSeconds =
            Mathf.Max(
                0.0f,
                (float)delta);
        UpdateScore(deltaSeconds);
    }

    public void EnterRefuge()
    {
        _mode = ScoreMode.Refuge;
        _arenaActive = false;
        _hubTarget = 1.0f;
        _regularTarget = 0.0f;
        _arenaTarget = 0.0f;
        _requestedIntensity = 0.0f;
        if (!Playing)
        {
            PitchScale = 1.0f;
            VolumeDb = SilentDb;
            Play();
        }
    }

    public void BeginRun()
    {
        _mode = ScoreMode.Run;
        _arenaActive = false;
        _routeStyle =
            RunRouteStyle.Neutral;
        _routeMood = Vector3.Zero;
        _routeMoodTarget =
            Vector3.Zero;
        _hubTarget = 0.0f;
        _regularTarget = 1.0f;
        _arenaTarget = 0.0f;
        _requestedIntensity = 0.12f;
        _smoothedIntensity =
            Mathf.Min(
                _smoothedIntensity,
                0.22f);
        _comboAccent = 0.0f;

        StopGroup(_tropicante);
        StopGroup(_toxitech);
        _regularCurrent = _tropicante;
        _regularNext = null;
        _regularTransitioning = false;
        _regularTransition = 0.0f;
        StartGroup(_regularCurrent);
    }

    public void SetGameplayIntensity(
        float momentumRatio,
        float comboIntensity,
        float speedBoostIntensity)
    {
        float momentum =
            Mathf.Clamp(
                momentumRatio,
                0.0f,
                1.0f);
        float combo =
            Mathf.Clamp(
                comboIntensity,
                0.0f,
                1.0f);
        float speed =
            Mathf.Clamp(
                speedBoostIntensity,
                0.0f,
                1.0f);
        _requestedIntensity =
            Mathf.Clamp(
                0.08f +
                momentum * 0.62f +
                combo * 0.34f +
                speed * 0.22f,
                0.0f,
                1.0f);
    }

    /// <summary>
    /// Changes the arrangement rather than restarting either run song. The
    /// three route identities are blended over time in UpdateScore, keeping
    /// every synchronized stem on its original musical frame.
    /// </summary>
    public void SetRouteStyle(
        RunRouteStyle routeStyle)
    {
        _routeStyle = routeStyle;
        _routeMoodTarget =
            routeStyle switch
            {
                RunRouteStyle.Hunt =>
                    Vector3.Right,
                RunRouteStyle.Flow =>
                    Vector3.Up,
                RunRouteStyle.Endure =>
                    Vector3.Back,
                _ => Vector3.Zero
            };
        _comboAccent =
            Mathf.Max(
                _comboAccent,
                routeStyle ==
                    RunRouteStyle.Neutral
                    ? 0.0f
                    : 0.34f);
    }

    public void PunchCombo(int chainCount)
    {
        float milestone =
            chainCount >= 10
                ? 1.0f
                : chainCount >= 6
                    ? 0.88f
                    : chainCount >= 3
                        ? 0.72f
                        : 0.55f;
        _comboAccent =
            Mathf.Max(
                _comboAccent,
                milestone);
    }

    public void EnterArena(int waveCount)
    {
        _mode = ScoreMode.Arena;
        _arenaActive = true;
        _hubTarget = 0.0f;
        _regularTarget = 0.0f;
        _arenaTarget = 1.0f;
        _arenaWavePressure =
            waveCount <= 1
                ? 0.55f
                : 0.36f;
        _comboAccent =
            Mathf.Max(
                _comboAccent,
                0.48f);
        StopGroup(_bloomyhrt);
        StartGroup(
            _bloomyhrt,
            loop: true);
    }

    public void SetArenaWave(
        int wave,
        int waveCount)
    {
        float progress =
            waveCount <= 1
                ? 1.0f
                : Mathf.Clamp(
                    (wave - 1.0f) /
                        (waveCount - 1.0f),
                    0.0f,
                    1.0f);
        _arenaWavePressure =
            Mathf.Lerp(
                0.42f,
                0.96f,
                progress);
        _comboAccent = 1.0f;
    }

    public void ExitArena()
    {
        _mode = ScoreMode.Run;
        _arenaActive = false;
        _arenaTarget = 0.0f;
        _regularTarget = 1.0f;
        _comboAccent =
            Mathf.Max(
                _comboAccent,
                0.42f);
    }

    public void SetRunEnded()
    {
        _mode = ScoreMode.Defeat;
        _hubTarget = 0.0f;
        if (_arenaActive)
        {
            _arenaTarget = 0.3f;
            _regularTarget = 0.0f;
        }
        else
        {
            _arenaTarget = 0.0f;
            _regularTarget = 0.3f;
        }
        _requestedIntensity = 0.0f;
        _comboAccent = 0.0f;
    }

    private void UpdateScore(float deltaSeconds)
    {
        _routeMood =
            _routeMood.Lerp(
                _routeMoodTarget,
                1.0f -
                    Mathf.Exp(
                        -1.05f *
                        deltaSeconds));
        _comboAccent =
            Mathf.MoveToward(
                _comboAccent,
                0.0f,
                deltaSeconds * 0.34f);
        float intensityResponse =
            _requestedIntensity >
                _smoothedIntensity
                ? 2.5f
                : 0.62f;
        _smoothedIntensity =
            Mathf.Lerp(
                _smoothedIntensity,
                _requestedIntensity,
                1.0f -
                    Mathf.Exp(
                        -intensityResponse *
                        deltaSeconds));

        _hubGain = MoveFader(
            _hubGain,
            _hubTarget,
            deltaSeconds,
            3.2f,
            3.8f);
        _regularGain = MoveFader(
            _regularGain,
            _regularTarget,
            deltaSeconds,
            3.4f,
            2.8f);
        _arenaGain = MoveFader(
            _arenaGain,
            _arenaTarget,
            deltaSeconds,
            2.7f,
            3.4f);

        VolumeDb =
            GainToDb(_hubGain);
        PitchScale = 1.0f;

        if (_mode != ScoreMode.Refuge)
        {
            UpdateRegularTimeline(
                deltaSeconds);
        }

        float intensity =
            Mathf.Max(
                _smoothedIntensity,
                _comboAccent);
        float routeIntensityFloor =
            _routeMood.X * 0.34f +
            _routeMood.Y * 0.2f +
            _routeMood.Z * 0.26f;
        intensity =
            Mathf.Max(
                intensity,
                routeIntensityFloor);
        float currentWeight = 1.0f;
        float nextWeight = 0.0f;
        if (_regularTransitioning)
        {
            _regularTransition =
                Mathf.Min(
                    1.0f,
                    _regularTransition +
                        deltaSeconds /
                            RegularCrossfadeDuration);
            currentWeight =
                Mathf.Cos(
                    _regularTransition *
                    Mathf.Pi *
                    0.5f);
            nextWeight =
                Mathf.Sin(
                    _regularTransition *
                    Mathf.Pi *
                    0.5f);
        }

        UpdateStemGroup(
            _regularCurrent,
            intensity,
            _regularGain *
                currentWeight,
            false,
            deltaSeconds);
        UpdateStemGroup(
            _regularNext,
            intensity,
            _regularGain *
                nextWeight,
            false,
            deltaSeconds);

        float arenaIntensity =
            Mathf.Max(
                intensity,
                _arenaWavePressure);
        UpdateStemGroup(
            _bloomyhrt,
            arenaIntensity,
            _arenaGain,
            true,
            deltaSeconds);

        if (_regularTransitioning &&
            _regularTransition >= 1.0f)
        {
            StemGroup previous =
                _regularCurrent;
            _regularCurrent =
                _regularNext;
            _regularNext = null;
            _regularTransitioning = false;
            _regularTransition = 0.0f;
            StopGroup(previous);
        }

        if (_mode == ScoreMode.Refuge &&
            _regularGain <= 0.001f)
        {
            StopGroup(_tropicante);
            StopGroup(_toxitech);
            _regularCurrent = null;
            _regularNext = null;
            _regularTransitioning = false;
        }
        if (!_arenaActive &&
            _arenaGain <= 0.001f)
        {
            StopGroup(_bloomyhrt);
        }
    }

    private void UpdateRegularTimeline(
        float deltaSeconds)
    {
        if (_regularCurrent == null)
        {
            _regularCurrent =
                _tropicante;
            StartGroup(_regularCurrent);
            return;
        }
        if (_regularTransitioning)
        {
            return;
        }

        float transitionPoint =
            Mathf.Max(
                0.0f,
                _regularCurrent.Length -
                    RegularCrossfadeDuration);
        bool nearEnding =
            _regularCurrent.IsPlaying &&
            _regularCurrent
                .PlaybackPosition >=
                transitionPoint;
        bool endedUnexpectedly =
            !_regularCurrent.IsPlaying;
        if (!nearEnding &&
            !endedUnexpectedly)
        {
            return;
        }

        _regularNext =
            _regularCurrent ==
                _tropicante
                ? _toxitech
                : _tropicante;
        StopGroup(_regularNext);
        StartGroup(_regularNext);
        _regularTransitioning = true;
        _regularTransition =
            endedUnexpectedly
                ? 0.72f
                : 0.0f;
    }

    private void UpdateStemGroup(
        StemGroup group,
        float intensity,
        float groupGain,
        bool arena,
        float deltaSeconds)
    {
        if (group == null)
        {
            return;
        }

        foreach (StemLayer layer in
            group.Layers)
        {
            float arrangementGain =
                arena
                    ? GetArenaStemGain(
                        layer.Role,
                        intensity)
                    : GetRunStemGain(
                        layer.Role,
                        intensity,
                        _routeMood);
            float target =
                arrangementGain *
                Mathf.Clamp(
                    groupGain,
                    0.0f,
                    1.0f);
            float response =
                target > layer.Gain
                    ? 4.8f
                    : 2.2f;
            layer.Gain =
                Mathf.Lerp(
                    layer.Gain,
                    target,
                    1.0f -
                        Mathf.Exp(
                            -response *
                            deltaSeconds));
            group.Synchronized
                .SetSyncStreamVolume(
                    layer.Index,
                    GainToDb(
                        layer.Gain));
        }
        group.Player.PitchScale = 1.0f;
    }

    private static float GetRunStemGain(
        StemRole role,
        float intensity,
        Vector3 routeMood)
    {
        float drive =
            SmoothStep(
                0.08f,
                0.86f,
                intensity);
        float baseGain = role switch
        {
            StemRole.Drums =>
                LerpGain(
                    -19.0f,
                    -4.5f,
                    drive),
            StemRole.Bass =>
                LerpGain(
                    -15.0f,
                    -5.0f,
                    drive),
            StemRole.Percussion =>
                Gain(-6.0f) *
                SmoothStep(
                    0.28f,
                    0.82f,
                    intensity),
            StemRole.Synth =>
                LerpGain(
                    -13.0f,
                    -5.5f,
                    drive),
            StemRole.Other =>
                LerpGain(
                    -9.0f,
                    -6.5f,
                    drive),
            StemRole.Brass =>
                Gain(-6.5f) *
                SmoothStep(
                    0.42f,
                    0.9f,
                    intensity),
            StemRole.Backing =>
                Gain(-9.0f) *
                SmoothStep(
                    0.48f,
                    0.9f,
                    intensity),
            StemRole.Lead =>
                Gain(-7.0f) *
                SmoothStep(
                    0.64f,
                    0.96f,
                    intensity),
            StemRole.Guitar =>
                LerpGain(
                    -14.0f,
                    -6.0f,
                    drive),
            _ => 0.0f
        };
        float huntMultiplier =
            role switch
            {
                StemRole.Drums => 1.22f,
                StemRole.Bass => 1.12f,
                StemRole.Percussion => 1.24f,
                StemRole.Brass => 1.1f,
                StemRole.Synth => 0.92f,
                StemRole.Lead => 0.92f,
                _ => 1.0f
            };
        float flowMultiplier =
            role switch
            {
                StemRole.Synth => 1.24f,
                StemRole.Other => 1.12f,
                StemRole.Brass => 1.08f,
                StemRole.Backing => 1.08f,
                StemRole.Lead => 1.05f,
                StemRole.Drums => 0.9f,
                _ => 1.0f
            };
        float endureMultiplier =
            role switch
            {
                StemRole.Bass => 1.17f,
                StemRole.Synth => 1.12f,
                StemRole.Other => 1.1f,
                StemRole.Percussion => 0.83f,
                StemRole.Backing => 0.82f,
                StemRole.Lead => 0.78f,
                _ => 1.0f
            };
        float multiplier =
            1.0f +
            routeMood.X *
                (huntMultiplier - 1.0f) +
            routeMood.Y *
                (flowMultiplier - 1.0f) +
            routeMood.Z *
                (endureMultiplier - 1.0f);
        return Mathf.Clamp(
            baseGain *
                Mathf.Max(0.45f, multiplier),
            0.0f,
            1.0f);
    }

    private static float GetArenaStemGain(
        StemRole role,
        float intensity)
    {
        float pressure =
            SmoothStep(
                0.28f,
                0.94f,
                intensity);
        return role switch
        {
            StemRole.Drums =>
                LerpGain(
                    -15.0f,
                    -3.5f,
                    pressure),
            StemRole.Bass =>
                LerpGain(
                    -13.0f,
                    -4.0f,
                    pressure),
            StemRole.Guitar =>
                LerpGain(
                    -10.0f,
                    -4.0f,
                    pressure),
            StemRole.Synth =>
                LerpGain(
                    -14.0f,
                    -5.0f,
                    pressure),
            StemRole.Other =>
                LerpGain(
                    -11.0f,
                    -6.0f,
                    pressure),
            StemRole.Backing =>
                Gain(-8.5f) *
                SmoothStep(
                    0.46f,
                    0.82f,
                    intensity),
            StemRole.Lead =>
                Gain(-6.5f) *
                SmoothStep(
                    0.68f,
                    0.96f,
                    intensity),
            StemRole.Percussion =>
                Gain(-6.0f) *
                SmoothStep(
                    0.4f,
                    0.9f,
                    intensity),
            StemRole.Brass =>
                Gain(-6.0f) *
                SmoothStep(
                    0.5f,
                    0.9f,
                    intensity),
            _ => 0.0f
        };
    }

    private StemGroup CreateStemGroup(
        string name,
        string directory,
        StemDefinition[] definitions)
    {
        StemGroup group = new()
        {
            Name = name,
            Synchronized =
                new AudioStreamSynchronized
                {
                    StreamCount =
                        definitions.Length
                }
        };
        for (int index = 0;
            index < definitions.Length;
            index++)
        {
            StemDefinition definition =
                definitions[index];
            string path =
                $"{directory}/" +
                definition.File;
            AudioStream source =
                GD.Load<AudioStream>(path);
            if (source == null)
            {
                GD.PushWarning(
                    $"Music stem missing: {path}");
                continue;
            }

            AudioStream stream =
                (AudioStream)source.Duplicate();
            if (stream is
                AudioStreamOggVorbis ogg)
            {
                ogg.Loop = false;
            }
            group.Synchronized
                .SetSyncStream(
                    index,
                    stream);
            group.Synchronized
                .SetSyncStreamVolume(
                    index,
                    SilentDb);
            group.Layers.Add(
                new StemLayer
                {
                    Index = index,
                    Stream = stream,
                    Role = definition.Role,
                    Gain = 0.0f
                });
            if (group.Length <= 0.0f)
            {
                group.Length =
                    (float)stream.GetLength();
            }
        }
        group.Player =
            new AudioStreamPlayer
            {
                Name = name,
                Stream =
                    group.Synchronized,
                Bus = "Music",
                VolumeDb = 0.0f,
                PitchScale = 1.0f,
                ProcessMode =
                    ProcessModeEnum.Always
            };
        AddChild(group.Player);
        return group;
    }

    private static void StartGroup(
        StemGroup group,
        bool loop = false)
    {
        if (group == null)
        {
            return;
        }
        foreach (StemLayer layer in
            group.Layers)
        {
            if (layer.Stream is
                AudioStreamOggVorbis ogg)
            {
                ogg.Loop = loop;
            }
            layer.Gain = 0.0f;
            group.Synchronized
                .SetSyncStreamVolume(
                    layer.Index,
                    SilentDb);
        }
        group.Player.PitchScale = 1.0f;
        group.Player.Play();
    }

    private static void StopGroup(
        StemGroup group)
    {
        if (group == null)
        {
            return;
        }
        group.Player.Stop();
        foreach (StemLayer layer in
            group.Layers)
        {
            group.Synchronized
                .SetSyncStreamVolume(
                    layer.Index,
                    SilentDb);
            layer.Gain = 0.0f;
        }
    }

    private void ConfigureLoopingStream()
    {
        if (Stream == null)
        {
            Stream =
                GD.Load<AudioStream>(
                    "res://Music/PATHTRAVOR ogg/PATHTRAVOR.ogg");
        }
        if (Stream == null)
        {
            return;
        }
        Stream =
            (AudioStream)Stream.Duplicate();
        if (Stream is
            AudioStreamOggVorbis ogg)
        {
            ogg.Loop = true;
        }
    }

    private static float MoveFader(
        float current,
        float target,
        float deltaSeconds,
        float riseDuration,
        float fallDuration)
    {
        float duration =
            target > current
                ? riseDuration
                : fallDuration;
        return Mathf.MoveToward(
            current,
            target,
            deltaSeconds /
                Mathf.Max(
                    0.05f,
                    duration));
    }

    private static float SmoothStep(
        float from,
        float to,
        float value)
    {
        float t =
            Mathf.Clamp(
                Mathf.InverseLerp(
                    from,
                    to,
                    value),
                0.0f,
                1.0f);
        return t * t *
            (3.0f - 2.0f * t);
    }

    private static float LerpGain(
        float lowDb,
        float highDb,
        float weight)
    {
        return Mathf.Lerp(
            Gain(lowDb),
            Gain(highDb),
            Mathf.Clamp(
                weight,
                0.0f,
                1.0f));
    }

    private static float Gain(float decibels)
    {
        return Mathf.DbToLinear(
            decibels);
    }

    private static float GainToDb(float gain)
    {
        return gain <= 0.0001f
            ? SilentDb
            : Mathf.LinearToDb(
                gain);
    }

    public static void EnsureMusicBus(
        AudioStreamPlayer player = null)
    {
        int musicBus =
            AudioServer.GetBusIndex("Music");
        if (musicBus < 0)
        {
            AudioServer.AddBus(-1);
            musicBus =
                AudioServer.BusCount - 1;
            AudioServer.SetBusName(
                musicBus,
                "Music");
        }

        if (player != null)
        {
            player.Bus = "Music";
        }
    }

    public static void ApplySavedVolumes()
    {
        ConfigFile settings = new();
        Error result =
            settings.Load(SettingsPath);
        float master = 100.0f;
        float music = 80.0f;
        if (result == Error.Ok)
        {
            master =
                (float)System.Math.Clamp(
                    settings.GetValue(
                        AudioSection,
                        "master_volume",
                        100.0).AsDouble(),
                    0.0,
                    100.0);
            music =
                (float)System.Math.Clamp(
                    settings.GetValue(
                        AudioSection,
                        "music_volume",
                        80.0).AsDouble(),
                    0.0,
                    100.0);
        }

        SetBusVolume(
            "Master",
            master);
        SetBusVolume(
            "Music",
            music);
    }

    public static void SetBusVolume(
        string busName,
        float percentage)
    {
        int busIndex =
            AudioServer.GetBusIndex(
                busName);
        if (busIndex < 0)
        {
            return;
        }

        float linearVolume =
            Mathf.Clamp(
                percentage / 100.0f,
                0.0f,
                1.0f);
        bool muted =
            linearVolume <= 0.001f;
        AudioServer.SetBusMute(
            busIndex,
            muted);
        AudioServer.SetBusVolumeDb(
            busIndex,
            muted
                ? SilentDb
                : Mathf.LinearToDb(
                    linearVolume));
    }
}
