using Godot;

public partial class CustomizationProfile : Node
{
    public enum SkinId
    {
        Classic,
        Tide,
        Verdant,
        Neon
    }

    public enum RefugeStyleId
    {
        Ember,
        Tideglass,
        Verdant,
        Neon
    }

    [Signal]
    public delegate void ProfileChangedEventHandler();

    private const string SavePath = "user://customization.cfg";
    private const string ProgressSection = "progress";
    private const string SelectionSection = "customization";
    private const int TideKillRequirement = 100;
    private const int TidePerfectDodgeRequirement = 12;
    private const int VerdantArenaRequirement = 8;
    private const int VerdantComboRequirement = 10;
    private const int NeonScoreRequirement = 750;
    private const int NeonComboRequirement = 18;
    private const int NeonPerfectDodgeRequirement = 24;

    private static readonly string[] SkinNames =
    {
        "REDLINE",
        "SKYLINE",
        "GRAPHITE",
        "SUNLINE"
    };

    private static readonly Color[] SkinColors =
    {
        new(0.94f, 0.95f, 0.93f),
        new(0.91f, 0.95f, 0.96f),
        new(0.95f, 0.94f, 0.9f),
        new(0.96f, 0.95f, 0.9f)
    };

    private static readonly Color[] SkinSecondaryColors =
    {
        new(0.9f, 0.08f, 0.045f),
        new(0.0f, 0.58f, 0.74f),
        new(0.08f, 0.11f, 0.12f),
        new(0.95f, 0.56f, 0.02f)
    };

    private static readonly Color[] SkinAccentColors =
    {
        new(0.07f, 0.1f, 0.11f),
        new(0.08f, 0.11f, 0.12f),
        new(0.92f, 0.1f, 0.055f),
        new(0.08f, 0.11f, 0.12f)
    };

    private static readonly string[] RefugeStyleNames =
    {
        "EMBER REST",
        "TIDEGLASS",
        "VERDANT NEST",
        "NEON NIGHT"
    };

    private static readonly Color[] RefugeStyleColors =
    {
        new(1.0f, 0.34f, 0.055f),
        new(0.08f, 0.72f, 1.0f),
        new(0.15f, 0.78f, 0.4f),
        new(0.92f, 0.16f, 0.72f)
    };

    private static readonly string[] RefugeStyleDetails =
    {
        "Ribbed metal walls, a low workshop bench and field storage.",
        "Glass wall fins, a clean console lounge and suspended displays.",
        "Living wall frames, planters and a softer recovery corner.",
        "Segmented light walls, modular seats and a projection table."
    };

    public int TotalKills { get; private set; }
    public int ArenasCleared { get; private set; }
    public int BestScore { get; private set; }
    public int LongestCombo { get; private set; }
    public int PerfectDodges { get; private set; }
    public int SelectedSkin { get; private set; }
    public int SelectedRefugeStyle { get; private set; }
    public bool HasPendingSave => _saveDirty;
    public static int SkinCount => SkinNames.Length;
    public static int RefugeStyleCount =>
        RefugeStyleNames.Length;
    private bool _saveDirty;
    private int _unlockedSkinMask = 1;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        LoadProfile();
    }

    public override void _ExitTree()
    {
        FlushPendingSave();
    }

    public override void _Notification(int what)
    {
        if (what ==
                NotificationWMCloseRequest ||
            what ==
                NotificationApplicationPaused)
        {
            FlushPendingSave();
        }
    }

    public static string GetSkinName(int skinIndex)
    {
        return SkinNames[Mathf.Clamp(skinIndex, 0, SkinNames.Length - 1)];
    }

    public static Color GetSkinColor(int skinIndex)
    {
        return SkinColors[Mathf.Clamp(skinIndex, 0, SkinColors.Length - 1)];
    }

    public static Color GetSkinSecondaryColor(
        int skinIndex)
    {
        return SkinSecondaryColors[
            Mathf.Clamp(
                skinIndex,
                0,
                SkinSecondaryColors.Length - 1)];
    }

    public static Color GetSkinAccentColor(
        int skinIndex)
    {
        return SkinAccentColors[
            Mathf.Clamp(
                skinIndex,
                0,
                SkinAccentColors.Length - 1)];
    }

    public static string GetRefugeStyleName(int styleIndex)
    {
        return RefugeStyleNames[
            Mathf.Clamp(
                styleIndex,
                0,
                RefugeStyleNames.Length - 1)];
    }

    public static Color GetRefugeStyleColor(int styleIndex)
    {
        return RefugeStyleColors[
            Mathf.Clamp(
                styleIndex,
                0,
                RefugeStyleColors.Length - 1)];
    }

    public static string GetRefugeStyleDetail(
        int styleIndex)
    {
        return RefugeStyleDetails[
            Mathf.Clamp(
                styleIndex,
                0,
                RefugeStyleDetails.Length - 1)];
    }

    public bool IsSkinUnlocked(int skinIndex)
    {
        int clamped =
            Mathf.Clamp(
                skinIndex,
                0,
                SkinCount - 1);
        if ((_unlockedSkinMask &
                (1 << clamped)) != 0)
        {
            return true;
        }

        return MeetsSkinContract(clamped);
    }

    private bool MeetsSkinContract(
        int skinIndex)
    {
        return skinIndex switch
        {
            (int)SkinId.Classic => true,
            (int)SkinId.Tide =>
                TotalKills >= TideKillRequirement &&
                PerfectDodges >= TidePerfectDodgeRequirement,
            (int)SkinId.Verdant =>
                ArenasCleared >= VerdantArenaRequirement &&
                LongestCombo >= VerdantComboRequirement,
            (int)SkinId.Neon =>
                BestScore >= NeonScoreRequirement &&
                LongestCombo >= NeonComboRequirement &&
                PerfectDodges >= NeonPerfectDodgeRequirement,
            _ => false
        };
    }

    public string GetUnlockProgressText(int skinIndex)
    {
        return skinIndex switch
        {
            (int)SkinId.Classic => "AVAILABLE FROM THE START",
            (int)SkinId.Tide =>
                $"UNDERTOW  •  KILLS {Mathf.Min(TotalKills, TideKillRequirement)}/{TideKillRequirement}  •  PERFECT DODGES {Mathf.Min(PerfectDodges, TidePerfectDodgeRequirement)}/{TidePerfectDodgeRequirement}",
            (int)SkinId.Verdant =>
                $"APEX GROWTH  •  ARENAS {Mathf.Min(ArenasCleared, VerdantArenaRequirement)}/{VerdantArenaRequirement}  •  COMBO x{Mathf.Min(LongestCombo, VerdantComboRequirement)}/{VerdantComboRequirement}",
            (int)SkinId.Neon =>
                $"EVENT HORIZON  •  SCORE {Mathf.Min(BestScore, NeonScoreRequirement)}/{NeonScoreRequirement}  •  COMBO x{Mathf.Min(LongestCombo, NeonComboRequirement)}/{NeonComboRequirement}  •  PERFECT {Mathf.Min(PerfectDodges, NeonPerfectDodgeRequirement)}/{NeonPerfectDodgeRequirement}",
            _ => "LOCKED"
        };
    }

    public void RecordKill(int bestScore)
    {
        TotalKills++;
        BestScore = Mathf.Max(BestScore, bestScore);
        CaptureCompletedContracts();
        MarkDirtyAndNotify();
    }

    public void RecordArenaCleared(int bestScore)
    {
        ArenasCleared++;
        BestScore = Mathf.Max(BestScore, bestScore);
        CaptureCompletedContracts();
        MarkDirtyAndNotify();
    }

    public void RecordComboPeak(
        int chainCount,
        int bestScore)
    {
        int updatedCombo =
            Mathf.Max(
                LongestCombo,
                chainCount);
        int updatedScore =
            Mathf.Max(
                BestScore,
                bestScore);
        if (updatedCombo == LongestCombo &&
            updatedScore == BestScore)
        {
            return;
        }

        LongestCombo = updatedCombo;
        BestScore = updatedScore;
        CaptureCompletedContracts();
        MarkDirtyAndNotify();
    }

    public void RecordPerfectDodge(
        int bestScore)
    {
        PerfectDodges++;
        BestScore =
            Mathf.Max(
                BestScore,
                bestScore);
        CaptureCompletedContracts();
        MarkDirtyAndNotify();
    }

    public void SyncBestScore(int bestScore)
    {
        int synchronizedScore = Mathf.Max(BestScore, bestScore);
        if (synchronizedScore == BestScore)
        {
            return;
        }

        BestScore = synchronizedScore;
        CaptureCompletedContracts();
        MarkDirtyAndNotify();
    }

    public bool SelectSkin(int skinIndex)
    {
        if (!IsSkinUnlocked(skinIndex))
        {
            return false;
        }

        SelectedSkin = Mathf.Clamp(skinIndex, 0, SkinCount - 1);
        CaptureCompletedContracts();
        SaveAndNotify();

        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        ApplySelectedSkin(player);
        return true;
    }

    public void SelectRefugeStyle(int styleIndex)
    {
        int selected = Mathf.Clamp(
            styleIndex,
            0,
            RefugeStyleCount - 1);
        if (SelectedRefugeStyle == selected)
        {
            return;
        }

        SelectedRefugeStyle = selected;
        SaveAndNotify();
    }

    public void ApplySelectedSkin(Player player)
    {
        if (!IsInstanceValid(player))
        {
            return;
        }

        player.ApplySkin(
            SelectedSkin,
            GetSkinColor(SelectedSkin),
            GetSkinSecondaryColor(
                SelectedSkin),
            GetSkinAccentColor(
                SelectedSkin));
    }

    private void LoadProfile()
    {
        ConfigFile profile = new();
        Error loadResult = profile.Load(SavePath);
        if (loadResult == Error.Ok)
        {
            TotalKills = Mathf.Max(
                0,
                profile.GetValue(ProgressSection, "total_kills", 0).AsInt32());
            ArenasCleared = Mathf.Max(
                0,
                profile.GetValue(ProgressSection, "arenas_cleared", 0).AsInt32());
            BestScore = Mathf.Max(
                0,
                profile.GetValue(ProgressSection, "best_score", 0).AsInt32());
            LongestCombo = Mathf.Max(
                0,
                profile.GetValue(
                    ProgressSection,
                    "longest_combo",
                    0).AsInt32());
            PerfectDodges = Mathf.Max(
                0,
                profile.GetValue(
                    ProgressSection,
                    "perfect_dodges",
                    0).AsInt32());
            SelectedSkin = Mathf.Clamp(
                profile.GetValue(
                    SelectionSection,
                    "selected_skin",
                    (int)SkinId.Classic).AsInt32(),
                0,
                SkinCount - 1);
            SelectedRefugeStyle = Mathf.Clamp(
                profile.GetValue(
                    SelectionSection,
                    "refuge_style",
                    (int)RefugeStyleId.Ember).AsInt32(),
                0,
                RefugeStyleCount - 1);
            if (profile.HasSectionKey(
                SelectionSection,
                "unlocked_skin_mask"))
            {
                _unlockedSkinMask =
                    Mathf.Max(
                        1,
                        profile.GetValue(
                            SelectionSection,
                            "unlocked_skin_mask",
                            1).AsInt32());
            }
            else
            {
                // Migration: cosmetics earned under the original contracts
                // remain owned when the new mastery contracts arrive.
                _unlockedSkinMask = 1;
                if (TotalKills >= 25)
                {
                    _unlockedSkinMask |=
                        1 << (int)SkinId.Tide;
                }
                if (ArenasCleared >= 3)
                {
                    _unlockedSkinMask |=
                        1 << (int)SkinId.Verdant;
                }
                if (BestScore >= 100)
                {
                    _unlockedSkinMask |=
                        1 << (int)SkinId.Neon;
                }
                _unlockedSkinMask |=
                    1 << SelectedSkin;
                _saveDirty = true;
            }
        }
        else if (loadResult != Error.FileNotFound)
        {
            GD.PushWarning($"Could not load customization profile: {loadResult}");
        }

        if (!IsSkinUnlocked(SelectedSkin))
        {
            SelectedSkin = (int)SkinId.Classic;
        }
        CaptureCompletedContracts();
    }

    private void SaveAndNotify()
    {
        _saveDirty = true;
        FlushPendingSave();
        EmitSignal(SignalName.ProfileChanged);
    }

    private void MarkDirtyAndNotify()
    {
        _saveDirty = true;
        EmitSignal(SignalName.ProfileChanged);
    }

    public void FlushPendingSave()
    {
        if (!_saveDirty)
        {
            return;
        }

        ConfigFile profile = new();
        profile.SetValue(ProgressSection, "total_kills", TotalKills);
        profile.SetValue(ProgressSection, "arenas_cleared", ArenasCleared);
        profile.SetValue(ProgressSection, "best_score", BestScore);
        profile.SetValue(
            ProgressSection,
            "longest_combo",
            LongestCombo);
        profile.SetValue(
            ProgressSection,
            "perfect_dodges",
            PerfectDodges);
        profile.SetValue(SelectionSection, "selected_skin", SelectedSkin);
        profile.SetValue(
            SelectionSection,
            "unlocked_skin_mask",
            _unlockedSkinMask);
        profile.SetValue(
            SelectionSection,
            "refuge_style",
            SelectedRefugeStyle);
        Error saveResult = profile.Save(SavePath);
        if (saveResult != Error.Ok)
        {
            GD.PushWarning($"Could not save customization profile: {saveResult}");
            return;
        }
        _saveDirty = false;
    }

    private void CaptureCompletedContracts()
    {
        for (int skinIndex = 0;
            skinIndex < SkinCount;
            skinIndex++)
        {
            if (MeetsSkinContract(
                skinIndex))
            {
                _unlockedSkinMask |=
                    1 << skinIndex;
            }
        }
    }
}
