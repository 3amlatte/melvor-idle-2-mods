using GamesByMalcs.Melvor2.GameMenus;
using Godot;
using MelvorModLoader;

namespace MelvorTown;

public sealed class Mod : IMod
{
    ModContext _ctx = null!;
    TownState _state = null!;
    TownPanel _panel = null!;
    TownGamePage _townPage = null!;
    Texture2D? _tabIcon;
    Button? _button;
    StyleBox? _tabNormalBox;
    long _lastMs;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _state = TownState.Load(ctx.Directory);
        _tabIcon = Ui.Texture(ctx.Directory, "tab");
        _panel = new TownPanel(ctx, _state);
        _townPage = new TownGamePage(_panel, _tabIcon, _panel.Ground);

        ctx.AddWindow("Melvor Town", OpenTownPage);
        ctx.Frame += OnFrame;
        _lastMs = (long)Time.GetTicksMsec();
        ctx.Log("Loaded.");
    }

    void OnFrame()
    {
        long now = (long)Time.GetTicksMsec();
        double dt = (now - _lastMs) / 1000.0;
        _lastMs = now;
        _state.Tick(dt);

        if (++_frame % 30 == 0) PlaceButton();
    }

    void OpenTownPage()
    {
        GameView? view = GameAccess.FindGameView();
        if (view == null) return;
        view.SetCurrentPage(_townPage);
    }

    void PlaceButton()
    {
        Node root = _ctx.Tree.Root;
        Control? bank = root.FindChild("BankNavigationButton", true, false) as Control;
        if (bank?.GetParent() is not BoxContainer box)
            return;

        if (_button != null && GodotObject.IsInstanceValid(_button) && _button.IsInsideTree())
            return;

        // Match the Shop/Bank tab: same size, icon size and exact label style.
        Control? shop = root.FindChild("ShopNavigationButton", true, false) as Control;
        Control? donor = shop != null && shop.GetParent() == box ? shop : bank;
        string donorName = donor == shop ? "Shop" : "Bank";

        Vector2 tabSize = donor.Size;
        if (tabSize.X < 20 || tabSize.Y < 20) tabSize = new Vector2(80, 56);

        Label? nameLabel = null;
        foreach (Label candidate in AllLabels(donor))
        {
            if (candidate.Text.Trim().Equals(donorName, StringComparison.OrdinalIgnoreCase))
            {
                nameLabel = candidate;
                break;
            }
        }

        Vector2 iconSize = new(40, 40);
        TextureRect? donorIcon = LargestTexture(donor);
        if (donorIcon != null && donorIcon.Size.X > 8)
            iconSize = donorIcon.Size;

        var button = new Button
        {
            Name = "TownNavigationButton",
            Flat = false,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(72, 0),
            SizeFlagsVertical = bank.SizeFlagsVertical,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = "Melvor Town\nGrow a small medieval town with population and food.",
        };
        // Copy the real tab backgrounds so the tab reads like Bank/Shop.
        StyleBox? normalBox = donor.GetThemeStylebox("normal");
        StyleBox? hoverBox = donor.GetThemeStylebox("hover");
        StyleBox? pressedBox = donor.GetThemeStylebox("pressed");
        StyleBox? hoverPressedBox = donor.GetThemeStylebox("hover_pressed");
        StyleBox? focusBox = donor.GetThemeStylebox("focus");

        _tabNormalBox = normalBox;

        if (normalBox != null) button.AddThemeStyleboxOverride("normal", normalBox);
        if (hoverBox != null) button.AddThemeStyleboxOverride("hover", hoverBox);
        if (pressedBox != null) button.AddThemeStyleboxOverride("pressed", pressedBox);
        if (hoverPressedBox != null) button.AddThemeStyleboxOverride("hover_pressed", hoverPressedBox);
        button.AddThemeStyleboxOverride("focus", focusBox ?? new StyleBoxEmpty());

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        column.AddThemeConstantOverride("separation", 0);
        column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var icon = new TextureRect
        {
            Texture = _tabIcon,
            CustomMinimumSize = iconSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        column.AddChild(icon);

        var label = new Label
        {
            Text = "Town",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (nameLabel?.LabelSettings != null)
        {
            label.LabelSettings = nameLabel.LabelSettings;
            label.Text = "Town";
        }
        else
        {
            if (nameLabel != null)
            {
                label.AddThemeFontSizeOverride("font_size", nameLabel.GetThemeFontSize("font_size"));
                label.AddThemeColorOverride("font_color", nameLabel.GetThemeColor("font_color"));
            }
            else
            {
                label.AddThemeFontSizeOverride("font_size", 16);
            }
        }
        column.AddChild(label);

        button.AddChild(column);

        button.Pressed += OpenTownPage;
        box.AddChild(button);
        box.MoveChild(button, bank.GetIndex());
        _button = button;
    }

    static TextureRect? LargestTexture(Node node)
    {
        TextureRect? best = node is TextureRect rect && rect.Texture != null ? rect : null;
        float area = best == null ? 0 : best.Size.X * best.Size.Y;

        foreach (Node child in node.GetChildren())
        {
            TextureRect? candidate = LargestTexture(child);
            if (candidate == null) continue;
            float candidateArea = candidate.Size.X * candidate.Size.Y;
            if (candidateArea > area)
            {
                area = candidateArea;
                best = candidate;
            }
        }
        return best;
    }

    static IEnumerable<Label> AllLabels(Node node)
    {
        if (node is Label label) yield return label;
        foreach (Node child in node.GetChildren())
            foreach (Label inner in AllLabels(child))
                yield return inner;
    }


}
