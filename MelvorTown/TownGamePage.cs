using GamesByMalcs.Melvor2.Components.MasteryGuild;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Components.Statistics;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.GameMenus.Footer;
using GamesByMalcs.Melvor2.GameMenus.Header;
using GamesByMalcs.Melvor2.GameMenus.Pages;
using GamesByMalcs.Melvor2.GameMenus.Widgets;
using Godot;

namespace MelvorTown;

/// <summary>
/// Makes the town a real game page: the town tab swaps the main view exactly like
/// Bank or Shop, instead of drawing an overlay on top of the current page.
/// </summary>
internal sealed class TownGamePage : GamePage
{
    static readonly DataNamespace TownNamespace = new("MelvorTown", "Melvor Town", true);

    readonly TownActivePage _active;

    public TownGamePage(TownPanel panel, Texture2D? icon, Texture2D? ground)
        : base(TownNamespace, "Town")
    {
        _active = new TownActivePage(panel, icon, ground);
    }

    public override ActiveGamePage GetActivePage() => _active;
}

internal sealed class TownActivePage : ActiveGamePage
{
    readonly TownPanel _panel;
    readonly Texture2D? _icon;
    readonly Texture2D? _ground;

    public TownActivePage(TownPanel panel, Texture2D? icon, Texture2D? ground)
    {
        _panel = panel;
        _icon = icon;
        _ground = ground;
    }

    public override bool HasStatistics => false;

    public override bool HasTutorial => false;

    protected override void SetMainView(GameView gameView)
    {
        if (_panel.GetParent() != null && _panel.GetParent() != gameView.EntityViewContainer)
            _panel.GetParent().RemoveChild(_panel);

        if (_panel.GetParent() != gameView.EntityViewContainer)
            gameView.EntityViewContainer.AddChild(_panel);

        _panel.Open();
    }

    protected override void CleanupMainView(GameView gameView)
    {
        if (_panel.GetParent() == gameView.EntityViewContainer)
            gameView.EntityViewContainer.RemoveChild(_panel);

        _panel.Close();
    }

    protected override void SetHeader(GameHeader header)
    {
        header.SetTitle("Melvor Town");
        if (_icon != null) header.SetPageIcon(_icon);
        header.MilestonesButtonDisabled = true;
        header.MasteryButtonDisabled = true;
        header.StatisticsButtonDisabled = true;
        header.TutorialButtonDisabled = true;
    }

    protected override void CleanupHeader(GameHeader header) { }

    protected override void SetBackground(GameView gameView)
    {
        if (_ground != null) gameView.Background.RequestStaticBackground(_ground);
    }

    protected override void CleanupBackground(GameView gameView)
    {
        gameView.Background.RequestEmptyBackground();
    }

    protected override void SetWidgets(WidgetsView widgets) { }

    protected override void CleanupWidgets(WidgetsView widgets) { }

    protected override void SetFooter(GameFooter footer) { }

    protected override void CleanupFooter(GameFooter footer) { }

    protected override void SetMilestones(MilestonesMenu milestones) { }

    protected override void CleanupMilestones(MilestonesMenu milestones) { }

    protected override void SetMasteryGuild(MasteryGuildMenu masteryGuild) { }

    protected override void CleanupMasteryGuild(MasteryGuildMenu masteryGuild) { }

    public override void SetStatistics(StatisticsMenu statistics) { }

    public override void OpenTutorial() { }

    public override void CloseExclusiveModals() { }
}
