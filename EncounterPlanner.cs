using Godot;
using System.Collections.Generic;

public enum MobWavePattern
{
    Relay,
    SpearLesson,
    NitroChase,
    ShepherdFork,
    TwinRelay,
    ShellPayoff,
    ComboRoute,
    Column,
    Solo,
    Row,
    Echelon,
    Crossing,
    Scatter,
    SplitColumns,
    Chevron,
    BaitAndPounce
}

public enum ArenaWaveStyle
{
    Crossfire,
    Pincer,
    Pinwheel,
    SpiralRelay,
    AmbushRing
}

public readonly struct ArenaTacticalRead
{
    public ArenaTacticalRead(
        float radialRatio,
        float speedRatio,
        float turnAmount,
        float turnSign)
    {
        RadialRatio = radialRatio;
        SpeedRatio = speedRatio;
        TurnAmount = turnAmount;
        TurnSign = turnSign;
    }

    public float RadialRatio { get; }
    public float SpeedRatio { get; }
    public float TurnAmount { get; }
    public float TurnSign { get; }
}

public readonly struct ArenaWavePlan
{
    public ArenaWavePlan(
        int displayWave,
        int totalWaves,
        int enemyBudget,
        int pulseCount,
        ArenaWaveStyle style,
        float safeAngle,
        float baseAngle,
        float turnSign)
    {
        DisplayWave = displayWave;
        TotalWaves = totalWaves;
        EnemyBudget = enemyBudget;
        PulseCount = pulseCount;
        Style = style;
        SafeAngle = safeAngle;
        BaseAngle = baseAngle;
        TurnSign = turnSign;
    }

    public int DisplayWave { get; }
    public int TotalWaves { get; }
    public int EnemyBudget { get; }
    public int PulseCount { get; }
    public ArenaWaveStyle Style { get; }
    public float SafeAngle { get; }
    public float BaseAngle { get; }
    public float TurnSign { get; }
}

/// <summary>
/// Scene-free encounter policy. The explicit seed isolates wave planning from
/// rendering and world-generation random calls and makes fingerprints stable.
/// </summary>
public sealed class EncounterPlanner
{
    public const int PlanningVersion = 1;
    public const int SpawnLaneCount = 5;

    private readonly EncounterSettings _settings;
    private readonly RandomNumberGenerator _random = new();
    private readonly List<MobWavePattern> _waveDeck = new();
    private readonly List<int> _spawnLaneDeck = new();
    private MobWavePattern _lastWavePattern =
        MobWavePattern.Solo;
    private ArenaWaveStyle _lastArenaWaveStyle =
        ArenaWaveStyle.AmbushRing;
    private int _lastSpawnLane = -1;

    public EncounterPlanner(
        EncounterSettings settings,
        ulong seed)
    {
        _settings = settings ??
            throw new System.ArgumentNullException(
                nameof(settings));
        Seed = seed;
        _random.Seed = seed;
    }

    public ulong Seed { get; }

    public void ResetTravelDeck()
    {
        _waveDeck.Clear();
    }

    public int GetNextSpawnLane()
    {
        if (_spawnLaneDeck.Count == 0)
        {
            RefillSpawnLaneDeck();
        }

        int nextIndex = _spawnLaneDeck.Count - 1;
        int lane = _spawnLaneDeck[nextIndex];
        _spawnLaneDeck.RemoveAt(nextIndex);
        _lastSpawnLane = lane;
        return lane;
    }

    public MobWavePattern ChooseMobWavePattern(
        int runScore,
        RunRouteStyle routeStyle)
    {
        if (_waveDeck.Count == 0)
        {
            RefillWaveDeck(
                runScore,
                routeStyle);
        }

        int lastIndex = _waveDeck.Count - 1;
        if (_waveDeck[lastIndex] ==
                _lastWavePattern &&
            lastIndex > 0)
        {
            for (int index = 0;
                index < lastIndex;
                index++)
            {
                if (_waveDeck[index] ==
                    _lastWavePattern)
                {
                    continue;
                }

                (_waveDeck[index],
                    _waveDeck[lastIndex]) =
                    (_waveDeck[lastIndex],
                        _waveDeck[index]);
                break;
            }
        }

        MobWavePattern pattern =
            _waveDeck[lastIndex];
        _waveDeck.RemoveAt(lastIndex);
        _lastWavePattern = pattern;
        return pattern;
    }

    public int GetWavePatternCount(
        MobWavePattern pattern,
        RunRouteStyle routeStyle,
        int overrunTier)
    {
        int authoredCount = pattern switch
        {
            MobWavePattern.Relay =>
                _random.RandiRange(6, 8),
            MobWavePattern.SpearLesson =>
                _random.RandiRange(6, 8),
            MobWavePattern.NitroChase =>
                _random.RandiRange(5, 7),
            MobWavePattern.ShepherdFork =>
                _random.RandiRange(7, 9),
            MobWavePattern.TwinRelay =>
                _random.RandiRange(8, 10),
            MobWavePattern.ShellPayoff =>
                _random.RandiRange(7, 9),
            MobWavePattern.ComboRoute =>
                _random.RandiRange(12, 14),
            MobWavePattern.Column =>
                _random.RandiRange(6, 8),
            MobWavePattern.Solo =>
                _random.Randf() < 0.68f ? 1 : 2,
            MobWavePattern.Row =>
                _random.RandiRange(3, 5),
            MobWavePattern.Echelon =>
                _random.RandiRange(3, 4),
            MobWavePattern.Crossing =>
                _random.Randf() < 0.7f ? 2 : 4,
            MobWavePattern.Scatter =>
                _random.RandiRange(3, 5),
            MobWavePattern.SplitColumns =>
                _random.Randf() < 0.72f ? 4 : 6,
            MobWavePattern.Chevron =>
                _random.Randf() < 0.65f ? 3 : 5,
            MobWavePattern.BaitAndPounce =>
                _random.RandiRange(10, 12),
            _ => 1
        };
        float routeScale = routeStyle switch
        {
            RunRouteStyle.Hunt => 1.08f,
            RunRouteStyle.Flow => 0.92f,
            RunRouteStyle.Endure => 0.82f,
            _ => 1.0f
        };
        int adjusted = Mathf.RoundToInt(
            authoredCount *
            routeScale *
            (1.0f +
                Mathf.Min(
                    0.18f,
                    overrunTier * 0.025f)));
        return Mathf.Max(
            Mathf.Min(
                authoredCount,
                GetMinimumPatternCount(pattern)),
            adjusted);
    }

    public int GetEffectiveActiveMobLimit(
        RunRouteStyle routeStyle,
        int overrunTier)
    {
        return Mathf.Max(
            6,
            _settings.MaxActiveMobs +
            Mathf.Min(6, overrunTier) +
            (routeStyle switch
            {
                RunRouteStyle.Hunt => 2,
                RunRouteStyle.Endure => -3,
                _ => 0
            }));
    }

    public float GetNextWaveDelay(
        ref int regularWavesSinceBreather,
        int currentLocation,
        int activeMobs,
        RunRouteStyle routeStyle,
        int overrunTier)
    {
        int wavesBeforeBreather =
            routeStyle switch
            {
                RunRouteStyle.Hunt => 5,
                RunRouteStyle.Endure => 3,
                _ => 4
            };
        if (regularWavesSinceBreather >=
            wavesBeforeBreather)
        {
            regularWavesSinceBreather = 0;
            float breatherScale =
                routeStyle == RunRouteStyle.Endure
                    ? 1.12f
                    : 1.0f;
            return _random.RandfRange(
                    1.75f,
                    2.15f) *
                breatherScale;
        }

        float progress = Mathf.Clamp(
            currentLocation / 10.0f,
            0.0f,
            1.0f);
        float minimum = Mathf.Max(
            0.68f,
            Mathf.Min(
                _settings.MinWaveInterval,
                _settings.MaxWaveInterval) -
            progress * 0.1f);
        float maximum = Mathf.Max(
            minimum,
            Mathf.Max(
                _settings.MinWaveInterval,
                _settings.MaxWaveInterval) -
            progress * 0.08f);
        float densityAdjustment =
            activeMobs <= 5
                ? -0.12f
                : activeMobs >= 14
                    ? 0.2f
                    : 0.0f;
        minimum = Mathf.Max(
            0.62f,
            minimum + densityAdjustment);
        maximum = Mathf.Max(
            minimum,
            maximum + densityAdjustment);
        float routeIntervalScale =
            routeStyle switch
            {
                RunRouteStyle.Hunt => 0.88f,
                RunRouteStyle.Flow => 1.05f,
                RunRouteStyle.Endure => 1.12f,
                _ => 1.0f
            };
        minimum = Mathf.Max(
            0.58f,
            minimum * routeIntervalScale);
        maximum = Mathf.Max(
            minimum,
            maximum * routeIntervalScale);
        float overrunIntervalScale =
            1.0f -
            Mathf.Min(
                0.22f,
                overrunTier * 0.025f);
        minimum = Mathf.Max(
            0.52f,
            minimum * overrunIntervalScale);
        maximum = Mathf.Max(
            minimum,
            maximum * overrunIntervalScale);
        return _random.RandfRange(
            minimum,
            maximum);
    }

    public ArenaWavePlan CreateArenaWavePlan(
        int wavesSpawned,
        int totalWaves,
        int overrunTier,
        Vector3 playerPosition,
        Vector3 playerVelocity,
        Vector3 arenaCenter,
        ArenaTacticalRead tacticalRead)
    {
        ArenaWaveStyle style =
            ChooseArenaWaveStyle(
                wavesSpawned,
                tacticalRead);
        int overrunBudget =
            Mathf.Min(10, overrunTier * 2);
        int enemyBudget = Mathf.Clamp(
            _settings.ArenaBaseEnemies +
                wavesSpawned *
                _settings.ArenaEnemiesPerWave +
                overrunBudget,
            4,
            Mathf.Max(
                4,
                _settings.ArenaMaxEnemies +
                overrunBudget));
        int pulseCount = Mathf.Clamp(
            Mathf.CeilToInt(
                enemyBudget / 6.0f),
            2,
            4);

        Vector3 playerHeading = new(
            playerVelocity.X,
            0.0f,
            playerVelocity.Z);
        if (playerHeading.LengthSquared() < 2.0f)
        {
            playerHeading =
                arenaCenter - playerPosition;
            playerHeading.Y = 0.0f;
        }
        if (playerHeading.IsZeroApprox())
        {
            playerHeading = Vector3.Forward;
        }

        playerHeading = playerHeading.Normalized();
        float safeAngle = Mathf.Atan2(
            playerHeading.Z,
            playerHeading.X);
        float baseAngle = safeAngle +
            (style switch
            {
                ArenaWaveStyle.Crossfire =>
                    Mathf.Pi * 0.5f,
                ArenaWaveStyle.Pincer =>
                    Mathf.Pi,
                ArenaWaveStyle.Pinwheel =>
                    Mathf.Pi * 0.25f,
                ArenaWaveStyle.SpiralRelay =>
                    -Mathf.Pi * 0.33f,
                _ => 0.0f
            });
        float turnSign =
            Mathf.Abs(tacticalRead.TurnSign) > 0.08f
                ? -Mathf.Sign(
                    tacticalRead.TurnSign)
                : ((wavesSpawned & 1) == 0
                    ? 1.0f
                    : -1.0f);

        return new ArenaWavePlan(
            wavesSpawned + 1,
            totalWaves,
            enemyBudget,
            pulseCount,
            style,
            safeAngle,
            baseAngle,
            turnSign);
    }

    public static float GetArenaPulseInterval(
        ArenaWaveStyle style,
        int overrunTier)
    {
        float authored = style switch
        {
            ArenaWaveStyle.AmbushRing => 0.94f,
            ArenaWaveStyle.SpiralRelay => 0.78f,
            ArenaWaveStyle.Pinwheel => 0.84f,
            _ => 0.86f
        };
        float pressureScale =
            1.0f -
            Mathf.Min(
                0.2f,
                overrunTier * 0.025f);
        return Mathf.Max(
            0.64f,
            authored * pressureScale);
    }

    private ArenaWaveStyle ChooseArenaWaveStyle(
        int wavesSpawned,
        ArenaTacticalRead tacticalRead)
    {
        ArenaWaveStyle[] candidates =
        {
            ArenaWaveStyle.Crossfire,
            ArenaWaveStyle.Pincer,
            ArenaWaveStyle.Pinwheel,
            ArenaWaveStyle.SpiralRelay,
            ArenaWaveStyle.AmbushRing
        };
        ArenaWaveStyle bestStyle =
            ArenaWaveStyle.Crossfire;
        float bestScore = float.MinValue;
        foreach (ArenaWaveStyle style in
            candidates)
        {
            if (style == _lastArenaWaveStyle)
            {
                continue;
            }

            float score = style switch
            {
                ArenaWaveStyle.Crossfire =>
                    0.72f +
                    (1.0f - tacticalRead.TurnAmount) *
                    tacticalRead.SpeedRatio * 1.35f,
                ArenaWaveStyle.Pincer =>
                    0.78f +
                    tacticalRead.RadialRatio * 1.6f +
                    tacticalRead.SpeedRatio * 0.12f,
                ArenaWaveStyle.Pinwheel =>
                    0.75f +
                    tacticalRead.TurnAmount * 1.25f +
                    (1.0f - tacticalRead.RadialRatio) *
                    0.3f,
                ArenaWaveStyle.SpiralRelay =>
                    0.72f +
                    tacticalRead.SpeedRatio * 0.82f +
                    (1.0f - tacticalRead.RadialRatio) *
                    0.42f,
                _ =>
                    0.68f +
                    (1.0f - tacticalRead.SpeedRatio) *
                    1.2f +
                    tacticalRead.RadialRatio * 0.22f
            };
            int styleIndex = (int)style;
            if ((wavesSpawned + styleIndex) %
                candidates.Length == 0)
            {
                score += 0.16f;
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestStyle = style;
            }
        }

        _lastArenaWaveStyle = bestStyle;
        return bestStyle;
    }

    private void RefillSpawnLaneDeck()
    {
        for (int lane = 0;
            lane < SpawnLaneCount;
            lane++)
        {
            _spawnLaneDeck.Add(lane);
        }
        Shuffle(_spawnLaneDeck);
        int nextIndex =
            _spawnLaneDeck.Count - 1;
        if (_spawnLaneDeck[nextIndex] ==
                _lastSpawnLane &&
            nextIndex > 0)
        {
            int swapIndex =
                _random.RandiRange(
                    0,
                    nextIndex - 1);
            (_spawnLaneDeck[nextIndex],
                _spawnLaneDeck[swapIndex]) =
                (_spawnLaneDeck[swapIndex],
                    _spawnLaneDeck[nextIndex]);
        }
    }

    private void RefillWaveDeck(
        int runScore,
        RunRouteStyle routeStyle)
    {
        switch (routeStyle)
        {
            case RunRouteStyle.Hunt:
                _waveDeck.AddRange(new[]
                {
                    MobWavePattern.Relay,
                    MobWavePattern.Relay,
                    MobWavePattern.Relay,
                    MobWavePattern.ComboRoute,
                    MobWavePattern.ComboRoute,
                    MobWavePattern.TwinRelay,
                    MobWavePattern.TwinRelay,
                    MobWavePattern.SpearLesson,
                    MobWavePattern.NitroChase
                });
                if (runScore >= 120)
                {
                    _waveDeck.Add(
                        MobWavePattern.ShellPayoff);
                }
                break;
            case RunRouteStyle.Flow:
                _waveDeck.AddRange(new[]
                {
                    MobWavePattern.NitroChase,
                    MobWavePattern.NitroChase,
                    MobWavePattern.NitroChase,
                    MobWavePattern.Relay,
                    MobWavePattern.Relay,
                    MobWavePattern.Relay,
                    MobWavePattern.ComboRoute,
                    MobWavePattern.Solo
                });
                if (runScore >= 55)
                {
                    _waveDeck.Add(
                        MobWavePattern.TwinRelay);
                }
                break;
            case RunRouteStyle.Endure:
                _waveDeck.AddRange(new[]
                {
                    MobWavePattern.SpearLesson,
                    MobWavePattern.SpearLesson,
                    MobWavePattern.SpearLesson,
                    MobWavePattern.Relay,
                    MobWavePattern.BaitAndPounce
                });
                _waveDeck.Add(
                    runScore >= 100
                        ? MobWavePattern.ShepherdFork
                        : MobWavePattern.SpearLesson);
                _waveDeck.Add(
                    runScore >= 180
                        ? MobWavePattern.ShellPayoff
                        : MobWavePattern.Relay);
                break;
            default:
                _waveDeck.AddRange(new[]
                {
                    MobWavePattern.Relay,
                    MobWavePattern.Relay,
                    MobWavePattern.SpearLesson,
                    MobWavePattern.SpearLesson
                });
                if (runScore >= 35)
                {
                    _waveDeck.AddRange(new[]
                    {
                        MobWavePattern.NitroChase,
                        MobWavePattern.NitroChase
                    });
                }
                if (runScore >= 55)
                {
                    _waveDeck.AddRange(new[]
                    {
                        MobWavePattern.TwinRelay,
                        MobWavePattern.TwinRelay
                    });
                }
                if (runScore >= 100)
                {
                    _waveDeck.AddRange(new[]
                    {
                        MobWavePattern.ShepherdFork,
                        MobWavePattern.ShepherdFork
                    });
                }
                if (runScore >= 180)
                {
                    _waveDeck.AddRange(new[]
                    {
                        MobWavePattern.ShellPayoff,
                        MobWavePattern.ShellPayoff
                    });
                }
                break;
        }

        Shuffle(_waveDeck);
        int nextIndex = _waveDeck.Count - 1;
        if (_waveDeck[nextIndex] ==
            _lastWavePattern)
        {
            for (int index = 0;
                index < nextIndex;
                index++)
            {
                if (_waveDeck[index] ==
                    _lastWavePattern)
                {
                    continue;
                }
                (_waveDeck[index],
                    _waveDeck[nextIndex]) =
                    (_waveDeck[nextIndex],
                        _waveDeck[index]);
                break;
            }
        }
    }

    private void Shuffle<T>(List<T> values)
    {
        for (int index = values.Count - 1;
            index > 0;
            index--)
        {
            int swapIndex =
                _random.RandiRange(0, index);
            (values[index], values[swapIndex]) =
                (values[swapIndex], values[index]);
        }
    }

    private static int GetMinimumPatternCount(
        MobWavePattern pattern)
    {
        return pattern switch
        {
            MobWavePattern.Relay => 6,
            MobWavePattern.SpearLesson => 6,
            MobWavePattern.NitroChase => 5,
            MobWavePattern.ShepherdFork => 7,
            MobWavePattern.TwinRelay => 8,
            MobWavePattern.ShellPayoff => 7,
            MobWavePattern.ComboRoute => 12,
            MobWavePattern.Column => 6,
            MobWavePattern.Row => 3,
            MobWavePattern.Echelon => 3,
            MobWavePattern.Crossing => 2,
            MobWavePattern.Scatter => 2,
            MobWavePattern.SplitColumns => 4,
            MobWavePattern.Chevron => 3,
            MobWavePattern.BaitAndPounce => 10,
            _ => 1
        };
    }
}
