using System;
using Godot;
using MelvorModLoader;

namespace MasteryEta;

/// <summary>
/// Compact mastery readout that sits inside the game's action footer, to the right of the
/// item-depletion timer. It uses the footer's small icon and text sizing rather than a
/// separate panel.
/// </summary>
internal sealed class MasteryBar
{
    static readonly Color Accent = new(0.929f, 0.749f, 0.349f);
    static readonly Color Text = new(0.92f, 0.92f, 0.94f);
    static readonly Color Track = new(0.18f, 0.19f, 0.24f);

    const float Height = 30f;

    readonly CanvasLayer _layer;
    readonly PanelContainer _panel;
    readonly TextureRect _masteryIcon;
    readonly TextureRect _itemIcon;
    readonly Label _task;
    readonly Label _eta;
    readonly ProgressBar _progress;

    public MasteryBar(ModContext ctx)
    {
        _layer = new CanvasLayer { Layer = 200, Visible = false };

        _panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = "Mastery tasks are long-term skill goals that earn Mastery Guild points and ranks."
        };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _layer.AddChild(_panel);

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        _panel.AddChild(column);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 5);
        column.AddChild(row);

        _masteryIcon = Icon();
        row.AddChild(_masteryIcon);

        var title = new Label
        {
            Text = "Mastery",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 11);
        title.AddThemeColorOverride("font_color", Accent);
        row.AddChild(title);

        _itemIcon = Icon();
        row.AddChild(_itemIcon);

        _task = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _task.AddThemeFontSizeOverride("font_size", 11);
        _task.AddThemeColorOverride("font_color", Text);
        row.AddChild(_task);

        _eta = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(92, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _eta.AddThemeFontSizeOverride("font_size", 11);
        _eta.AddThemeColorOverride("font_color", Accent);
        row.AddChild(_eta);

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            Step = 0.001,
            CustomMinimumSize = new Vector2(0, 3),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var trackStyle = new StyleBoxFlat { BgColor = Track };
        trackStyle.SetCornerRadiusAll(1);
        var fillStyle = new StyleBoxFlat { BgColor = Accent };
        fillStyle.SetCornerRadiusAll(1);
        _progress.AddThemeStyleboxOverride("background", trackStyle);
        _progress.AddThemeStyleboxOverride("fill", fillStyle);
        column.AddChild(_progress);

        ctx.Tree.Root.AddChild(_layer);
    }

    static TextureRect Icon() => new()
    {
        CustomMinimumSize = new Vector2(16, 16),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public void Show(Control? footer, Texture2D? masteryIcon, Texture2D? itemIcon,
        string task, double current, double max, string eta)
    {
        Vector2 viewport = _panel.GetViewportRect().Size;
        if (viewport.X < 200) { Hide(); return; }

        Rect2 footerRect = footer != null && GodotObject.IsInstanceValid(footer)
            ? footer.GetGlobalRect()
            : new Rect2();

        float x, y, width;

        Control? anchor = footer != null && GodotObject.IsInstanceValid(footer)
            ? FindDepletionLabel(footer)
            : null;

        if (anchor != null && footerRect.Size.X >= 200)
        {
            Rect2 a = anchor.GetGlobalRect();
            x = a.End.X + 18f;
            y = a.Position.Y + 2f;
            width = footerRect.End.X - 104f - x;
            width = Math.Clamp(width, 190f, 360f);
        }
        else if (footerRect.Size.X >= 200)
        {
            width = 340f;
            x = footerRect.End.X - width - 100f;
            y = footerRect.End.Y - Height - 4f;
        }
        else
        {
            width = Math.Min(360f, viewport.X - 32f);
            x = (viewport.X - width) / 2f;
            y = viewport.Y - Height - 150f;
        }

        x = Math.Clamp(x, 8f, Math.Max(8f, viewport.X - width - 8f));
        y = Math.Clamp(y, 8f, Math.Max(8f, viewport.Y - Height - 8f));

        _panel.Position = new Vector2(x, y);
        _panel.Size = new Vector2(width, Height);

        _masteryIcon.Texture = masteryIcon;
        _itemIcon.Texture = itemIcon;
        _task.Text = task;
        _eta.Text = eta;
        _progress.Value = max > 0 ? Math.Clamp(current / max, 0, 1) : 0;

        _layer.Visible = true;
    }

    public void Hide()
    {
        _layer.Visible = false;
    }

    static Control? FindDepletionLabel(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Label label && label.Text.Contains("depleted", StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }
            if (child is RichTextLabel rich && rich.Text.Contains("depleted", StringComparison.OrdinalIgnoreCase))
            {
                return rich;
            }

            Control? found = FindDepletionLabel(child);
            if (found != null) return found;
        }
        return null;
    }
}
