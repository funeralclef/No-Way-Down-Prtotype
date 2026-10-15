using Godot;

/// <summary>
/// Typed view over the authored refuge interface. It owns the stable control
/// hierarchy and icon setup; Refuge owns gameplay state and dynamic layout.
/// </summary>
[GlobalClass]
public partial class RefugeInterface : Control
{
    public ColorRect FocusVeil { get; private set; }
    public PanelContainer StationPanel { get; private set; }
    public ScrollContainer StationScroll { get; private set; }
    public MarginContainer StationMargin { get; private set; }
    public VBoxContainer StationContent { get; private set; }
    public Label StationEyebrow { get; private set; }
    public Label StationTitle { get; private set; }
    public Label StationDescription { get; private set; }
    public Label SelectionName { get; private set; }
    public Label SelectionDetail { get; private set; }
    public Button PreviousButton { get; private set; }
    public Button PrimaryButton { get; private set; }
    public Button NextButton { get; private set; }
    public Button WardrobeButton { get; private set; }
    public Button RefugeButton { get; private set; }
    public Button LoadoutButton { get; private set; }
    public Button DescentButton { get; private set; }
    public Button InfoButton { get; private set; }
    public Button SettingsButton { get; private set; }
    public Button ToggleInterfaceButton { get; private set; }
    public Button MobilePreviousButton { get; private set; }
    public Button MobileNextButton { get; private set; }
    public ColorRect Fade { get; private set; }
    public Control SettingsHost { get; private set; }

    public Button[] StationButtons =>
        new[]
        {
            WardrobeButton,
            RefugeButton,
            LoadoutButton,
            DescentButton
        };

    public override void _Ready()
    {
        FocusVeil = GetNode<ColorRect>(
            "HubFocusVeil");
        StationPanel = GetNode<PanelContainer>(
            "StationPanel");
        StationScroll = GetNode<ScrollContainer>(
            "StationPanel/StationScroll");
        StationMargin = GetNode<MarginContainer>(
            "StationPanel/StationScroll/StationMargin");
        StationContent = GetNode<VBoxContainer>(
            "StationPanel/StationScroll/StationMargin/StationContent");
        StationEyebrow = StationContent.GetNode<Label>(
            "StationEyebrow");
        StationTitle = StationContent.GetNode<Label>(
            "StationTitle");
        StationDescription = StationContent.GetNode<Label>(
            "StationDescription");
        SelectionName = StationContent.GetNode<Label>(
            "SelectionName");
        SelectionDetail = StationContent.GetNode<Label>(
            "SelectionDetail");
        PreviousButton = StationContent.GetNode<Button>(
            "Actions/Previous");
        PrimaryButton = StationContent.GetNode<Button>(
            "Actions/Primary");
        NextButton = StationContent.GetNode<Button>(
            "Actions/Next");
        WardrobeButton = GetNode<Button>(
            "WorldNavigation/Wardrobe");
        RefugeButton = GetNode<Button>(
            "WorldNavigation/Refuge");
        LoadoutButton = GetNode<Button>(
            "WorldNavigation/Loadout");
        DescentButton = GetNode<Button>(
            "WorldNavigation/Descent");
        InfoButton = GetNode<Button>("WorldNavigation/Info");
        SettingsButton = GetNode<Button>(
            "WorldNavigation/Settings");
        ToggleInterfaceButton = GetNode<Button>(
            "WorldNavigation/ToggleInterface");
        MobilePreviousButton = GetNode<Button>(
            "MobilePreviousStation");
        MobileNextButton = GetNode<Button>(
            "MobileNextStation");
        Fade = GetNode<ColorRect>("Fade");
        SettingsHost = GetNode<Control>(
            "SettingsHost");

        ApplyIcons();
        HideScrollChrome();
    }

    public bool HasValidContract()
    {
        if (!IsInstanceValid(FocusVeil) ||
            !IsInstanceValid(StationPanel) ||
            !IsInstanceValid(StationScroll) ||
            !IsInstanceValid(StationMargin) ||
            !IsInstanceValid(StationContent) ||
            !IsInstanceValid(StationEyebrow) ||
            !IsInstanceValid(StationTitle) ||
            !IsInstanceValid(StationDescription) ||
            !IsInstanceValid(SelectionName) ||
            !IsInstanceValid(SelectionDetail) ||
            !IsInstanceValid(PreviousButton) ||
            !IsInstanceValid(PrimaryButton) ||
            !IsInstanceValid(NextButton) ||
            !IsInstanceValid(WardrobeButton) ||
            !IsInstanceValid(RefugeButton) ||
            !IsInstanceValid(LoadoutButton) ||
            !IsInstanceValid(DescentButton) ||
            !IsInstanceValid(SettingsButton) ||
            !IsInstanceValid(ToggleInterfaceButton) ||
            !IsInstanceValid(MobilePreviousButton) ||
            !IsInstanceValid(MobileNextButton) ||
            !IsInstanceValid(Fade) ||
            !IsInstanceValid(SettingsHost) || !IsInstanceValid(InfoButton))
        {
            return false;
        }

        Button[] stations = StationButtons;
        return stations.Length == 4 &&
            stations[0].Name == "Wardrobe" &&
            stations[1].Name == "Refuge" &&
            stations[2].Name == "Loadout" &&
            stations[3].Name == "Descent";
    }

    private void ApplyIcons()
    {
        UiIconLibrary.Apply(InfoButton, UiIconLibrary.Info, true, 22);
        UiIconLibrary.Apply(
            PreviousButton,
            UiIconLibrary.Previous,
            true,
            21);
        UiIconLibrary.Apply(
            NextButton,
            UiIconLibrary.Next,
            true,
            21);
        UiIconLibrary.Apply(
            SettingsButton,
            UiIconLibrary.Settings,
            true,
            24);
        UiIconLibrary.Apply(
            ToggleInterfaceButton,
            UiIconLibrary.Visible,
            true,
            24);
        UiIconLibrary.Apply(
            MobilePreviousButton,
            UiIconLibrary.Previous,
            true,
            30);
        UiIconLibrary.Apply(
            MobileNextButton,
            UiIconLibrary.Next,
            true,
            30);
    }

    private void HideScrollChrome()
    {
        ScrollBar[] bars =
        {
            StationScroll.GetVScrollBar(),
            StationScroll.GetHScrollBar()
        };
        foreach (ScrollBar bar in bars)
        {
            bar.Modulate =
                new Color(
                    1.0f,
                    1.0f,
                    1.0f,
                    0.0f);
            bar.MouseFilter =
                MouseFilterEnum.Ignore;
            bar.FocusMode =
                FocusModeEnum.None;
        }
    }
}
