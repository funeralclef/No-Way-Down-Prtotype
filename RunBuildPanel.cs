using Godot;
using System;
using System.Linq;

/// <summary>Read-only build cards and explicit, atomic merge actions.</summary>
public partial class RunBuildPanel : GameMenuPanel
{
    private RunProgression _progression;
    private Action _synchronize;
    private bool _recipes;
    private bool _archive;
    private GridContainer _grid;
    protected override string PanelTitle => _archive ? "MODULE ARCHIVE" : "YOUR BUILD";
    protected override string PanelSubtitle => "EQUIPMENT  /  MODULES & EVOLUTIONS";
    public void Initialize(RunProgression progression, Action synchronize, bool archive = false)
    {
        _archive = archive;
        _progression = progression;
        _synchronize = synchronize;
    }

    protected override void Rebuild()
    {
        ClearContent();
        if (_progression == null) return;
        var tabs = new HBoxContainer();
        var active = BuffUi.Button(_archive ? "ARCHIVE" : "ACTIVE BUFFS");
        active.CustomMinimumSize = new Vector2(60, 44);
        int readyCount = RunBuffCatalog.Recipes.Count(recipe => _progression.CanMerge(recipe.Evolution));
        var merge = BuffUi.Button(readyCount > 0 ? $"MERGE ({readyCount})" : "MERGE RECIPES");
        active.SizeFlagsHorizontal = merge.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tabs.AddChild(active); tabs.AddChild(merge); Content.AddChild(tabs);
        active.Pressed += () => ShowTab(false);
        merge.Pressed += () => ShowTab(true);
        Content.AddChild(BuffUi.Label(_recipes
            ? "Trade two catalyst ranks for an evolution. The device stays installed."
            : _archive ? "Plan your equipment. Each cleared arena grants a device and its matching catalyst." : "Installed devices change how you play. Catalysts power your evolution recipes.", 14, BuffUi.Muted));
        _grid = new GridContainer { Columns = GetViewport().GetVisibleRect().Size.X >= 900 ? 2 : 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 10);
        Content.AddChild(_grid);
        if (_recipes)
        {
            foreach (var recipe in RunBuffCatalog.Recipes.OrderByDescending(recipe =>
                _progression.CanMerge(recipe.Evolution) ? 2 : _progression.HasUpgrade(recipe.Evolution) ? 0 : 1)) AddRecipe(recipe);
        }
        else
        {
            int count = 0;
            foreach (var definition in RunProgression.Catalog.OrderByDescending(item => item.Tier))
            {
                int rank = _progression.GetRank(definition.Id);
                if (_archive && definition.Tier == RunRewardTier.Impulse && !RunBuffCatalog.Recipes.Any(recipe => recipe.Catalyst == definition.Id)) continue;
                if (rank <= 0 && !_archive) continue;
                count++;
                AddBuff(definition, rank);
            }
            if (count == 0) Content.AddChild(BuffUi.Label("Your build starts after the first arena. Clear it to install your first device.\nOpen MERGE RECIPES to plan an evolution.", 16));
        }
    }

    protected override void OnPanelResized()
    {
        if (IsInstanceValid(_grid)) _grid.Columns = GetViewport().GetVisibleRect().Size.X >= 900 ? 2 : 1;
    }

    private void ShowTab(bool recipes)
    {
        _recipes = recipes;
        RefreshContent();
        Content.GetChild<HBoxContainer>(0).GetChild<Button>(recipes ? 1 : 0).GrabFocus();
    }

    private VBoxContainer Card(RunUpgradeId id, string title, string eyebrow)
    {
        Color accent = RunBuffCatalog.Accent(id);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", BuffUi.Surface(accent));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _grid.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);
        var header = new HBoxContainer();
        header.AddChild(UiIconLibrary.Create(RunBuffCatalog.Icon(id), 28, accent));
        header.AddChild(BuffUi.Label(title, 17));
        column.AddChild(header);
        column.AddChild(BuffUi.Label(eyebrow, 12, accent));
        return column;
    }

    private void AddBuff(RunUpgradeDefinition definition, int rank)
    {
        var column = Card(definition.Id, definition.Name,
            $"{(definition.Tier == RunRewardTier.Impulse ? "CATALYST" : definition.Tier.ToString().ToUpperInvariant())}   {RunBuffCatalog.RankPips(definition.Id, rank)}   {rank}/{definition.MaxRank}");
        column.AddChild(BuffUi.Label(RunBuffCatalog.Effect(definition.Id, Mathf.Max(1, rank)), 14, BuffUi.Muted));
        var skill = RunSkillCatalog.Get(definition.Id);
        if (skill != null)
        {
            column.AddChild(BuffUi.Label("CAST · " + skill.Name + " · E / controller X", 12, BuffUi.Accent));
            column.AddChild(BuffUi.Label(skill.Instruction, 13, BuffUi.Muted));
            var runtime = (GetTree().GetFirstNodeInGroup("player") as Player)?.RunSkills;
            if (!_archive && rank > 0 && runtime != null)
            {
                var equip = BuffUi.Button(runtime.Equipped == definition.Id ? "EQUIPPED SKILL" : "EQUIP SKILL");
                equip.Disabled = runtime.Equipped == definition.Id;
                equip.Pressed += () => { runtime.Equip(definition.Id); RefreshContent(); };
                column.AddChild(equip);
            }
        }
        foreach (var recipe in RunBuffCatalog.Recipes)
        {
            if (recipe.Device == definition.Id || recipe.Catalyst == definition.Id)
                column.AddChild(BuffUi.Label($"MERGE → {RunProgression.GetDefinition(recipe.Evolution).Name}", 12, RunBuffCatalog.Accent(recipe.Evolution)));
        }
    }

    private void AddRecipe(RunMergeRecipe recipe)
    {
        var definition = RunProgression.GetDefinition(recipe.Evolution);
        bool owned = _progression.HasUpgrade(recipe.Evolution);
        bool ready = _progression.CanMerge(recipe.Evolution);
        var column = Card(recipe.Evolution, definition.Name, owned ? "EVOLVED / COMPLETE" : ready ? "MERGE READY" : "COLLECT THE INGREDIENTS");
        column.AddChild(BuffUi.Label(definition.Description, 14, BuffUi.Muted));
        column.AddChild(BuffUi.Label(
            $"{RunProgression.GetDefinition(recipe.Device).Name}  {(_progression.HasUpgrade(recipe.Device) ? "✓" : "missing")}\n" +
            $"{RunProgression.GetDefinition(recipe.Catalyst).Name}  {_progression.GetRank(recipe.Catalyst)}/2 ranks · consumes 2", 13, BuffUi.Accent));
        var button = BuffUi.Button(owned ? "EVOLUTION INSTALLED" : ready ? "MERGE → " + definition.Name : "INGREDIENTS REQUIRED");
        button.Name = "Merge" + recipe.Evolution;
        button.Disabled = !ready;
        column.AddChild(button);
        button.Pressed += () =>
        {
            if (!_progression.TryMerge(recipe.Evolution)) return;
            _synchronize?.Invoke();
            ShowTab(true);
        };
    }
}
