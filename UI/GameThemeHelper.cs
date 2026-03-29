using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace DontAbandonYourFriends.UI;

/// <summary>Loads the same Kreon themes as the game UI and applies them to controls.</summary>
internal static class GameThemeHelper
{
    private static readonly string[] SharedThemePaths =
    {
        "res://themes/kreon_regular_shared.tres",
        "res://themes/kreon_bold_shared.tres",
    };

    /// <summary>Kreon bold first — matches main menu button typography.</summary>
    private static readonly string[] MenuThemePaths =
    {
        "res://themes/kreon_bold_shared.tres",
        "res://themes/kreon_regular_shared.tres",
    };

    /// <summary>
    /// Resolves a theme usable for main-menu style text. Mod init often runs before <see cref="ResourceLoader.Exists"/>
    /// returns true; we also fall back to the project theme, ThemeDB, or a theme borrowed from the main menu scene.
    /// </summary>
    public static Theme? ResolveSharedTheme(Node? sceneRoot)
    {
        Theme? t = TryLoadSharedThemeFromPaths();
        if (t != null)
        {
            return t;
        }

        try
        {
            t = ThemeDB.GetProjectTheme();
            if (t != null)
            {
                return t;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] ThemeDB project theme: {ex.Message}");
        }

        try
        {
            t = ThemeDB.GetDefaultTheme();
            if (t != null)
            {
                return t;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] ThemeDB default theme: {ex.Message}");
        }

        return TryBorrowThemeFromMainMenu(sceneRoot);
    }

    /// <summary>Theme for main-menu-style text: Kreon bold first, then shared fallbacks.</summary>
    public static Theme? ResolveMenuTheme(Node? sceneRoot)
    {
        Theme? t = TryLoadMenuThemeFromPaths();
        if (t != null)
        {
            return t;
        }

        try
        {
            t = ThemeDB.GetProjectTheme();
            if (t != null)
            {
                return t;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] ThemeDB project theme (menu): {ex.Message}");
        }

        try
        {
            t = ThemeDB.GetDefaultTheme();
            if (t != null)
            {
                return t;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] ThemeDB default theme (menu): {ex.Message}");
        }

        return TryBorrowThemeFromMainMenu(sceneRoot);
    }

    private static Theme? TryLoadMenuThemeFromPaths()
    {
        try
        {
            foreach (string p in MenuThemePaths)
            {
                Theme? t = TryLoadThemeResource(p);
                if (t != null)
                {
                    return t;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Menu theme load: {ex.Message}");
        }

        return null;
    }

    private static Theme? TryLoadSharedThemeFromPaths()
    {
        try
        {
            foreach (string p in SharedThemePaths)
            {
                // Do not gate on Exists(); it can be false briefly at startup while Load still works.
                Theme? t = TryLoadThemeResource(p);
                if (t != null)
                {
                    return t;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Theme load: {ex.Message}");
        }

        return null;
    }

    /// <summary>Some game builds use the same path for a <see cref="FontVariation"/> instead of a <see cref="Theme"/>.</summary>
    private static Theme? TryLoadThemeResource(string path)
    {
        try
        {
            Resource? r = ResourceLoader.Load(path);
            if (r is Theme th)
            {
                return th;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Theme resource at {path}: {ex.Message}");
        }

        return null;
    }

    private static Theme? TryBorrowThemeFromMainMenu(Node? sceneRoot)
    {
        if (sceneRoot == null)
        {
            return null;
        }

        Node? mm = sceneRoot.FindChild("NMainMenu", true, false)
            ?? sceneRoot.FindChild("MainMenu", true, false);
        if (mm == null)
        {
            return null;
        }

        return FindThemeOnControlSubtree(mm);
    }

    private static Theme? FindThemeOnControlSubtree(Node node)
    {
        if (node is Control c && c.Theme != null)
        {
            return c.Theme;
        }

        foreach (Node child in node.GetChildren())
        {
            Theme? t = FindThemeOnControlSubtree(child);
            if (t != null)
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// Copies Label-type theme entries into explicit overrides so <see cref="MegaLabel"/> passes
    /// <c>MegaLabelHelper.AssertThemeFontOverride</c> when entering the tree.
    /// </summary>
    public static void ApplyLabelThemeItemsFromTheme(Label label, Theme theme)
    {
        const string labelType = "Label";

        if (theme.HasFont(LabelThemeKeys.Font, labelType))
        {
            Font? f = theme.GetFont(LabelThemeKeys.Font, labelType);
            if (f != null)
            {
                label.AddThemeFontOverride(LabelThemeKeys.Font, f);
            }
        }
        else if (theme.DefaultFont != null)
        {
            label.AddThemeFontOverride(LabelThemeKeys.Font, theme.DefaultFont);
        }

        if (theme.HasFontSize(LabelThemeKeys.FontSize, labelType))
        {
            label.AddThemeFontSizeOverride(LabelThemeKeys.FontSize, theme.GetFontSize(LabelThemeKeys.FontSize, labelType));
        }
        else if (theme.DefaultFontSize > 0)
        {
            label.AddThemeFontSizeOverride(LabelThemeKeys.FontSize, theme.DefaultFontSize);
        }

        if (theme.HasColor(LabelThemeKeys.FontColor, labelType))
        {
            label.AddThemeColorOverride(LabelThemeKeys.FontColor, theme.GetColor(LabelThemeKeys.FontColor, labelType));
        }

        if (theme.HasColor(LabelThemeKeys.FontOutlineColor, labelType))
        {
            label.AddThemeColorOverride(LabelThemeKeys.FontOutlineColor, theme.GetColor(LabelThemeKeys.FontOutlineColor, labelType));
        }

        if (theme.HasColor(LabelThemeKeys.FontShadowColor, labelType))
        {
            label.AddThemeColorOverride(LabelThemeKeys.FontShadowColor, theme.GetColor(LabelThemeKeys.FontShadowColor, labelType));
        }

        if (theme.HasConstant(LabelThemeKeys.OutlineSize, labelType))
        {
            label.AddThemeConstantOverride(LabelThemeKeys.OutlineSize, theme.GetConstant(LabelThemeKeys.OutlineSize, labelType));
        }

        if (theme.HasConstant(LabelThemeKeys.LineSpacing, labelType))
        {
            label.AddThemeConstantOverride(LabelThemeKeys.LineSpacing, theme.GetConstant(LabelThemeKeys.LineSpacing, labelType));
        }
    }

    /// <summary>
    /// Ensures the thick dark “stroke” around menu text (cream fill + charcoal outline) like vanilla main menu labels.
    /// </summary>
    public static void EnsureMainMenuLabelOutline(Label label)
    {
        label.AddThemeConstantOverride(LabelThemeKeys.OutlineSize, 14);
        label.AddThemeColorOverride(LabelThemeKeys.FontOutlineColor, new Color(0.11f, 0.11f, 0.12f, 1f));
    }

    public static void ApplyButtonFontFromTheme(Button button, Theme theme)
    {
        const string buttonType = "Button";
        if (theme.HasFont("font", buttonType))
        {
            Font? f = theme.GetFont("font", buttonType);
            if (f != null)
            {
                button.AddThemeFontOverride("font", f);
            }
        }

        if (theme.HasFontSize("font_size", buttonType))
        {
            button.AddThemeFontSizeOverride("font_size", theme.GetFontSize("font_size", buttonType));
        }
    }

    /// <summary>Applies Kreon / project font to a <see cref="LineEdit"/> from a loaded <see cref="Theme"/>.</summary>
    public static void ApplyLineEditFontFromTheme(LineEdit lineEdit, Theme theme)
    {
        const string lineEditType = "LineEdit";
        if (theme.HasFont("font", lineEditType))
        {
            Font? f = theme.GetFont("font", lineEditType);
            if (f != null)
            {
                lineEdit.AddThemeFontOverride("font", f);
            }
        }
        else if (theme.DefaultFont != null)
        {
            lineEdit.AddThemeFontOverride("font", theme.DefaultFont);
        }

        if (theme.HasFontSize("font_size", lineEditType))
        {
            lineEdit.AddThemeFontSizeOverride("font_size", theme.GetFontSize("font_size", lineEditType));
        }
        else if (theme.DefaultFontSize > 0)
        {
            lineEdit.AddThemeFontSizeOverride("font_size", theme.DefaultFontSize);
        }

        if (theme.HasColor("font_color", lineEditType))
        {
            lineEdit.AddThemeColorOverride("font_color", theme.GetColor("font_color", lineEditType));
        }
    }
}
