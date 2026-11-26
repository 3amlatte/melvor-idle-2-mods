using System;
using Godot;
using MelvorModLoader;

namespace MasteryEta;

/// <summary>
/// The mastery notification row. It is placed next to the game's own action tracker bar
/// (above the footer on a normal window, or just below it if the bar is at the top).
/// </summary>
internal sealed class MasteryBar
{
    static readonly Color Accent = new(0.929f, 0.749f, 0.349f);
    static readonly Color Panel = new(0.071f, 0.078f, 0.110f);
    static readonly Color Muted = new(0.690f, 0.690f, 0.690f);
    static readonly Color Text = new(1f, 1f, 1f);
    static readonly Color Track = new(0.16f, 0.17f, 0.22f);

    const float Height = 72f;

    readonly CanvasLayer _layer;
    readonly PanelContainer _panel;
    readonly TextureRect _icon;
    readonly Label _task;
    readonly Label _eta;
    readonly ProgressBar _progress;

    public MasteryBar(ModContext ctx)
    {
        _layer = new CanvasLayer { Layer = 200, Visible = false };

        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = Panel, BorderColor = Accent };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(8);
        _panel.AddThemeStyleboxOverride("panel", style);
        _layer.AddChild(_panel);

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        column.AddThemeConstantOverride("separation", 3);
        _panel.AddChild(column);

        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 6);
        column.AddChild(row);

        _icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(_icon);

        var title = new Label { Text = "Mastery", MouseFilter = Control.MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", Accent);
        row.AddChild(title);

        _task = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _task.AddThemeFontSizeOverride("font_size", 13);
        _task.AddThemeColorOverride("font_color", Text);
        row.AddChild(_task);

        _eta = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(120, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _eta.AddThemeFontSizeOverride("font_size", 13);
        _eta.AddThemeColorOverride("font_color", Accent);
        row.AddChild(_eta);

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            Step = 0.001,
            CustomMinimumSize = new Vector2(0, 7),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var trackStyle = new StyleBoxFlat { BgColor = Track };
        trackStyle.SetCornerRadiusAll(3);
        var fillStyle = new StyleBoxFlat { BgColor = Accent };
        fillStyle.SetCornerRadiusAll(3);
        _progress.AddThemeStyleboxOverride("background", trackStyle);
        _progress.AddThemeStyleboxOverride("fill", fillStyle);
        column.AddChild(_progress);

        var outline = new Label
        {
            Text = "Mastery tasks are long-term skill goals that earn Mastery Guild points and ranks.",
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        outline.AddThemeFontSizeOverride("font_size", 9);
        outline.AddThemeColorOverride("font_color", Muted);
        column.AddChild(outline);

        ctx.Tree.Root.AddChild(_layer);
    }

    public void Show(Control? footer, Texture2D? icon, string task, double current, double max, string eta)
    {
        Vector2 viewport = _panel.GetViewportRect().Size;
        if (viewport.X < 200) { Hide(); return; }

        float width;
        float x;
        float y;

        Rect2 rect = footer != null && GodotObject.IsInstanceValid(footer)
            ? footer.GetGlobalRect()
            : new Rect2();

        if (rect.Size.X >= 200)
        {
            width = Math.Clamp(rect.Size.X - 16f, 460f, viewport.X - 16f);
            x = Math.Clamp(rect.Position.X + 8f, 8f, Math.Max(8f, viewport.X - width - 8f));
            bool footerAtBottom = rect.Position.Y > viewport.Y * 0.5f;
            y = footerAtBottom ? rect.Position.Y - Height - 6f : rect.End.Y + 6f;
            y = Math.Clamp(y, 8f, Math.Max(8f, viewport.Y - Height - 8f));
        }
        else
        {
            // No tracker footer yet: float above the bottom edge until it appears.
            width = Math.Min(760f, viewport.X - 32f);
            x = (viewport.X - width) / 2f;
            y = Math.Max(8f, viewport.Y - Height - 150f);
        }

        _panel.Position = new Vector2(x, y);
        _panel.Size = new Vector2(width, Height);

        _icon.Texture = icon;
        _task.Text = task;
        _eta.Text = eta;
        _progress.Value = max > 0 ? Math.Clamp(current / max, 0, 1) : 0;

        _layer.Visible = true;
    }

    public void Hide()
    {
        _layer.Visible = false;
    }
}
