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
    bool _tabActive;
    long _lastMs;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _state = TownState.Load(ctx.Directory);
        _tabIcon = Ui.Texture(ctx.Directory, "tab");
        _panel = new TownPanel(ctx, _state);
        _townPage = new TownGamePage(_panel, _tabIcon);

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

        if (++_frame % 30 == 0)
        {
            PlaceButton();
            UpdateTabState();
        }
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

        // Clone the Shop/Bank tab's visual tree so size, font and colour match exactly.
        Control? shop = root.FindChild("ShopNavigationButton", true, false) as Control;
        Control? donor = shop != null && shop.GetParent() == box ? shop : bank;
        Control? template = donor.GetChildren().OfType<Control>().FirstOrDefault(c => c.GetChildCount() > 0);

        Vector2 tabSize = donor.Size;
        if (tabSize.X < 20 || tabSize.Y < 20) tabSize = new Vector2(80, 56);

        var button = new Button
        {
            Name = "TownNavigationButton",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = tabSize,
            SizeFlagsVertical = bank.SizeFlagsVertical,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = "Melvor Town\nGrow a small medieval town with population and food.",
        };
        var hover = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.07f) };
        hover.SetCornerRadiusAll(4);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);

        if (template != null)
        {
            var visual = (Control)template.Duplicate();
            button.AddChild(visual);

            TextureRect? icon = LargestTexture(visual);
            if (icon != null) icon.Texture = _tabIcon;

            string donorName = donor == shop ? "Shop" : "Bank";
            bool named = false;
            foreach (Label label in AllLabels(visual))
            {
                if (!named && label.Text.Trim().Equals(donorName, StringComparison.OrdinalIgnoreCase))
                {
                    label.Text = "Town";
                    named = true;
                }
                else if (!named && label.Text.Trim().Length > 0)
                {
                    label.Text = "Town";
                    named = true;
                }
                else
                {
                    label.Visible = false;
                }
            }
        }
        else
        {
            var column = new VBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Center,
            };
            column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            column.AddChild(new TextureRect
            {
                Texture = _tabIcon,
                CustomMinimumSize = new Vector2(40, 40),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            var label = new Label
            {
                Text = "Town",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 16);
            column.AddChild(label);
            button.AddChild(column);
        }

        button.Pressed += OpenTownPage;
        box.AddChild(button);
        box.MoveChild(button, bank.GetIndex());
        _button = button;
        _tabActive = false;
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

    void UpdateTabState()
    {
        bool active = _panel.GetParent() != null;
        if (active == _tabActive) return;
        _tabActive = active;

        if (_button == null || !GodotObject.IsInstanceValid(_button)) return;

        if (active)
        {
            var style = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.09f) };
            style.SetCornerRadiusAll(4);
            _button.AddThemeStyleboxOverride("normal", style);
        }
        else
        {
            _button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        }
    }
}
