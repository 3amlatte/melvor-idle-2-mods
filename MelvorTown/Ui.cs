using Godot;

namespace MelvorTown;

internal static class Ui
{
    public static readonly Color Page = new("14120f");
    public static readonly Color Panel = new("1c1a16");
    public static readonly Color PanelLight = new("26231d");
    public static readonly Color Border = new("3b352b");
    public static readonly Color Text = new("e2dbcd");
    public static readonly Color Muted = new("9c9486");
    public static readonly Color Accent = new("edbf59");
    public static readonly Color Good = new("7bbf6a");
    public static readonly Color Bad = new("c9695a");

    public static StyleBoxFlat Box(Color bg, Color border, int radius = 4, int width = 1)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(radius);
        box.ContentMarginLeft = 10;
        box.ContentMarginRight = 10;
        box.ContentMarginTop = 6;
        box.ContentMarginBottom = 6;
        return box;
    }

    public static Label Label(string text, int size = 14, Color? color = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Text);
        return label;
    }

    public static Button Button(string text, Action pressed, int size = 14)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeStyleboxOverride("normal", Box(PanelLight, Border, 4));
        button.AddThemeStyleboxOverride("hover", Box(new Color("34302a"), Accent, 4));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("3d382f"), Accent, 4));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("201d19"), new Color("2b2721"), 4));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Accent);
        button.AddThemeColorOverride("font_disabled_color", new Color("6d665b"));
        button.Pressed += pressed;
        return button;
    }

    public static Texture2D? Texture(string directory, string name)
    {
        foreach (string extension in new[] { ".png", ".jpg", ".webp" })
        {
            string path = Path.Combine(directory, "art", name + extension);
            if (!File.Exists(path)) continue;
            try
            {
                Image? image = Image.LoadFromFile(path);
                return image == null ? null : ImageTexture.CreateFromImage(image);
            }
            catch { return null; }
        }
        return null;
    }

    public static TextureRect Icon(Texture2D? texture, float size)
    {
        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }
}
