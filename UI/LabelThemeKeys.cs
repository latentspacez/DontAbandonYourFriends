using Godot;
#if VERSION_0_99_1
using MegaCrit.Sts2.addons.mega_text;
#endif

namespace DontAbandonYourFriends.UI;

/// <summary>
/// v0.101.0+ no longer exposes Label theme keys on <c>ThemeConstants.Label</c>; use Godot’s built-in names.
/// <c>Release-Sts2_0_99_1</c> with a v0.99.1 <c>sts2.dll</c> can still map through <c>ThemeConstants.Label</c>.
/// Default <c>Release</c>/<c>Debug</c> and <c>Release-Sts2_0_101_0</c> use literals so a beta-only install compiles.
/// </summary>
internal static class LabelThemeKeys
{
#if VERSION_0_101_0
    public static readonly StringName Font = "font";
    public static readonly StringName FontSize = "font_size";
    public static readonly StringName FontColor = "font_color";
    public static readonly StringName FontOutlineColor = "font_outline_color";
    public static readonly StringName FontShadowColor = "font_shadow_color";
    public static readonly StringName OutlineSize = "outline_size";
    public static readonly StringName LineSpacing = "line_spacing";
#elif VERSION_0_99_1
    public static readonly StringName Font = ThemeConstants.Label.font;
    public static readonly StringName FontSize = ThemeConstants.Label.fontSize;
    public static readonly StringName FontColor = ThemeConstants.Label.fontColor;
    public static readonly StringName FontOutlineColor = ThemeConstants.Label.fontOutlineColor;
    public static readonly StringName FontShadowColor = ThemeConstants.Label.fontShadowColor;
    public static readonly StringName OutlineSize = ThemeConstants.Label.outlineSize;
    public static readonly StringName LineSpacing = ThemeConstants.Label.lineSpacing;
#else
    public static readonly StringName Font = "font";
    public static readonly StringName FontSize = "font_size";
    public static readonly StringName FontColor = "font_color";
    public static readonly StringName FontOutlineColor = "font_outline_color";
    public static readonly StringName FontShadowColor = "font_shadow_color";
    public static readonly StringName OutlineSize = "outline_size";
    public static readonly StringName LineSpacing = "line_spacing";
#endif
}
