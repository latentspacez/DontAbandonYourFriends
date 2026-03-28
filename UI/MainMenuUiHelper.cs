using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace DontAbandonYourFriends.UI;

/// <summary>Shared Kreon / main-menu text button setup (MegaLabel + NMainMenuTextButton).</summary>
internal static partial class MainMenuUiHelper
{

    // --- CACHED RESOURCES ---
    private static StyleBoxFlat? _modalPanelStyle;
    public static StyleBoxFlat ModalPanelStyle => _modalPanelStyle ??= new StyleBoxFlat
    {
        BgColor = ModalPanelBg,
        BorderColor = ModalAccent,
        BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        ContentMarginLeft = 16, ContentMarginTop = 16, ContentMarginRight = 16, ContentMarginBottom = 16,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
    };

    private static StyleBoxFlat? _dangerNormalStyle;
    private static StyleBoxFlat? _dangerHoverStyle;

    private static StyleBoxFlat GetDangerStyle(string state)
    {
        if (state == "hover")
        {
            return _dangerHoverStyle ??= new StyleBoxFlat
            {
                BgColor = new Color(DangerBg.R * 1.15f, DangerBg.G * 1.1f, DangerBg.B * 1.1f, 1f),
                BorderColor = DangerBorder,
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                ContentMarginLeft = 8, ContentMarginTop = 4, ContentMarginRight = 8, ContentMarginBottom = 4,
            };
        }
        
        return _dangerNormalStyle ??= new StyleBoxFlat
        {
            BgColor = DangerBg,
            BorderColor = DangerBorder,
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 8, ContentMarginTop = 4, ContentMarginRight = 8, ContentMarginBottom = 4,
        };
    }

    public static void ApplyModalPanelStyle(PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", ModalPanelStyle);
    }

    /// <summary>Single label font size for every <see cref="NMainMenuTextButton"/> in this mod (widen <paramref name="minSize"/> if text is long).</summary>
    public const int MenuButtonLabelFontSize = 18;

    /// <summary>Default minimum size for menu-style actions — same height everywhere; increase width when the label needs it.</summary>
    public static readonly Vector2 DefaultMenuButtonMinSize = new(120, 44);
    public static readonly Vector2 SmallMenuButtonMinSize = new(96, 32);

    private static readonly Color ModalPanelBg = new(0.12f, 0.1f, 0.09f, 0.98f);
    private static readonly Color ModalAccent = new(0.78f, 0.64f, 0.15f, 1f);
    private static readonly Color DangerBg = new(0.42f, 0.1f, 0.09f, 1f);
    private static readonly Color DangerBorder = new(0.85f, 0.25f, 0.22f, 1f);

    private static readonly string[] ReticleTextureGuessPaths =
    {
        "res://images/packed/main_menu/menu_reticle.png",
        "res://images/ui/main_menu/menu_reticle.png",
        "res://images/packed/main_menu/button_reticle.png",
    };

    /// <summary>Bind hover reticle art: prefer live NMainMenu nodes, then try common resource paths.</summary>
    public static void TryBindMainMenuReticleTextures(TextureRect? left, TextureRect? right, Node? sceneRoot)
    {
        if (left == null || right == null)
        {
            return;
        }

        bool gotLeft = false;
        bool gotRight = false;

        try
        {
            Node? mm = sceneRoot?.FindChild("NMainMenu", true, false)
                ?? sceneRoot?.FindChild("MainMenu", true, false);
            if (mm != null)
            {
                Node? nLeft = mm.GetNodeOrNull("%ButtonReticleLeft");
                Node? nRight = mm.GetNodeOrNull("%ButtonReticleRight");
                TextureRect? srcLeft = nLeft as TextureRect ?? FindFirstTextureRect(nLeft);
                TextureRect? srcRight = nRight as TextureRect ?? FindFirstTextureRect(nRight);

                if (srcLeft?.Texture != null)
                {
                    left.Texture = srcLeft.Texture;
                    if (srcLeft.Size.X > 2f && srcLeft.Size.Y > 2f)
                    {
                        left.CustomMinimumSize = srcLeft.Size;
                    }

                    gotLeft = true;
                }

                if (srcRight?.Texture != null)
                {
                    right.Texture = srcRight.Texture;
                    if (srcRight.Size.X > 2f && srcRight.Size.Y > 2f)
                    {
                        right.CustomMinimumSize = srcRight.Size;
                    }

                    gotRight = true;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Reticle steal from NMainMenu: {ex.Message}");
        }

        if (!gotLeft || !gotRight)
        {
            foreach (string path in ReticleTextureGuessPaths)
            {
                try
                {
                    if (!ResourceLoader.Exists(path))
                    {
                        continue;
                    }

                    Texture2D? tex = ResourceLoader.Load<Texture2D>(path);
                    if (tex == null)
                    {
                        continue;
                    }

                    if (!gotLeft)
                    {
                        left.Texture = tex;
                        gotLeft = true;
                    }

                    if (!gotRight)
                    {
                        right.Texture = tex;
                        gotRight = true;
                    }

                    if (gotLeft && gotRight)
                    {
                        break;
                    }
                }
                catch
                {
                    // try next path
                }
            }
        }

        if (left.Texture != null
            && right.Texture != null
            && ReferenceEquals(left.Texture, right.Texture))
        {
            right.FlipH = true;
        }
    }

    private static TextureRect? FindFirstTextureRect(Node? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is TextureRect tr)
        {
            return tr;
        }

        foreach (Node c in node.GetChildren())
        {
            TextureRect? found = FindFirstTextureRect(c);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// A reusable CanvasLayer that automatically traps the Escape key and triggers a callback.
    /// Perfect for dynamic modals and overlays to prevent the main game from catching ESC.
    /// </summary>
    internal partial class EscapableCanvasLayer : CanvasLayer
    {
        /// <summary>The action to execute when the user presses Escape.</summary>
        public Action? OnEscapeAction { get; set; }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
            {
                // 1. Consume the event immediately
                GetViewport().SetInputAsHandled();
                
                // 2. Trigger whatever close logic this specific UI needs
                OnEscapeAction?.Invoke();
            }
        }
    }

    public static MegaLabel CreateMegaLabel(
        Theme theme,
        string text,
        int fontSize,
        HorizontalAlignment hAlign = HorizontalAlignment.Left,
        Color? selfModulate = null)
    {
        var megaLabel = new MegaLabel
        {
            Text = text,
            HorizontalAlignment = hAlign,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        megaLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        megaLabel.OffsetRight = 0;
        megaLabel.OffsetBottom = 0;
        GameThemeHelper.ApplyLabelThemeItemsFromTheme(megaLabel, theme);
        GameThemeHelper.EnsureMainMenuLabelOutline(megaLabel);
        megaLabel.AddThemeFontSizeOverride(ThemeConstants.Label.fontSize, fontSize);
        megaLabel.SelfModulate = selfModulate ?? StsColors.cream;
        return megaLabel;
    }

    public static NMainMenuTextButton CreateMainMenuTextButton(
        Theme theme,
        string text,
        Action? released = null,
        Vector2? minSize = null,
        int fontSize = MenuButtonLabelFontSize) // <-- Added fontSize
    {
        Vector2 size = minSize ?? DefaultMenuButtonMinSize;
        var menuBtn = new NMainMenuTextButton
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = size,
            Theme = theme,
        };
        MegaLabel megaLabel = CreateMegaLabel(theme, text, fontSize, HorizontalAlignment.Center);
        menuBtn.AddChild(megaLabel);
        if (released != null) menuBtn.Released += _ => released();
        Callable.From(() => RefreshLabelPivot(menuBtn)).CallDeferred();
        return menuBtn;
    }

    public static NMainMenuTextButton CreateMainMenuDangerTextButton(
        Theme theme,
        string text,
        Action? released = null,
        Vector2? minSize = null,
        int fontSize = MenuButtonLabelFontSize)
    {
        Vector2 size = minSize ?? DefaultMenuButtonMinSize;
        var menuBtn = new NMainMenuTextButton
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = size,
            Theme = theme,
        };
        
        // Reverted back to the original pinkish-white color
        MegaLabel megaLabel = CreateMegaLabel(theme, text, fontSize, HorizontalAlignment.Center, new Color(1f, 0.92f, 0.92f));
        menuBtn.AddChild(megaLabel);
        
        foreach (string state in new[] { "normal", "hover", "pressed" })
        {
            menuBtn.AddThemeStyleboxOverride(state, GetDangerStyle(state));
        }

        if (released != null) menuBtn.Released += _ => released();
        Callable.From(() => RefreshLabelPivot(menuBtn)).CallDeferred();
        return menuBtn;
    }

    public static void RefreshLabelPivot(NMainMenuTextButton menuBtn)
    {
        try
        {
            if (menuBtn.label != null)
            {
                menuBtn.label.PivotOffset = menuBtn.label.Size * 0.5f;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Label pivot: {ex.Message}");
        }
    }

    public static void StyleLabelWithGameFont(Label label, Theme theme, int fontSize, Color fontColor)
    {
        GameThemeHelper.ApplyLabelThemeItemsFromTheme(label, theme);
        GameThemeHelper.EnsureMainMenuLabelOutline(label);
        label.AddThemeFontSizeOverride(ThemeConstants.Label.fontSize, fontSize);
        label.AddThemeColorOverride(ThemeConstants.Label.fontColor, fontColor);
    }



    /// <summary>Red destructive <see cref="Button"/> with Kreon font from theme.</summary>
    public static Button CreateDangerButton(Theme? theme, string text, Action? onPressed = null)
    {
        var b = new Button { Text = text };
        ApplyDangerButtonStyle(b, theme);
        if (onPressed != null)
        {
            b.Pressed += () => onPressed();
        }

        return b;
    }

    public static void ApplyDangerButtonStyle(Button b, Theme? theme, Vector2? minSize = null, int fontSize = MenuButtonLabelFontSize)
    {
        b.CustomMinimumSize = minSize ?? DefaultMenuButtonMinSize;
        b.AddThemeFontSizeOverride("font_size", fontSize);
        
        b.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.92f)); 
        
        if (theme != null) GameThemeHelper.ApplyButtonFontFromTheme(b, theme);

        foreach (string state in new[] { "normal", "hover", "pressed" })
        {
            b.AddThemeStyleboxOverride(state, GetDangerStyle(state));
        }
    }


    /// <summary>Body copy in styled confirmation <see cref="MegaLabel"/> — larger than button labels for readability.</summary>
    private const int ConfirmationBodyFontSize = 28;

    /// <summary>Single-line compact prompts (e.g. delete confirm).</summary>
    private const int CompactConfirmationBodyFontSize = 22;

    /// <summary>
    /// Creates an animated, full-screen confirmation overlay. 
    /// Add this directly to the SceneTree Root.
    /// </summary>
    public static CanvasLayer CreateConfirmationOverlay(
        Theme theme,
        string title,
        string body,
        string warningText, // <-- New parameter for the red text
        string primaryText,
        string secondaryText,
        bool primaryIsDanger,
        Func<Task> onPrimaryAsync,
        Action? onSecondary = null)
    {
        // 1. The CanvasLayer guarantees this sits above absolutely everything else in the game
        var canvasLayer = new EscapableCanvasLayer
        {
            Layer = 2000, 
            ProcessMode = Node.ProcessModeEnum.Always
        };

        // 2. Full-screen blocker container
        var overlay = new Control();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvasLayer.AddChild(overlay);

        // 3. The Backdrop (MouseFilter.Stop is critical here to block clicks from passing through)
        var backdrop = new ColorRect 
        { 
            Color = new Color(0, 0, 0, 0.85f),
            MouseFilter = Control.MouseFilterEnum.Stop 
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        // 4. The Panel
        var panel = new PanelContainer { Theme = theme };
        ApplyModalPanelStyle(panel);
        panel.CustomMinimumSize = new Vector2(480, 0); 
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        
        vbox.AddThemeConstantOverride("separation", 24);
        margin.AddChild(vbox);

        // Inside the VBoxContainer (Typography section):
        vbox.AddChild(CreateMegaLabel(theme, title, 24, HorizontalAlignment.Center, StsColors.cream));
        
        MegaLabel bodyLabel = CreateMegaLabel(theme, body, 16, HorizontalAlignment.Center, new Color(0.88f, 0.85f, 0.8f));
        bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bodyLabel.AutoSizeEnabled = false;
        vbox.AddChild(bodyLabel);

        // Append the red warning text directly below the main body
        if (!string.IsNullOrEmpty(warningText))
        {
            MegaLabel warningLabel = CreateMegaLabel(theme, warningText, 16, HorizontalAlignment.Center, new Color(0.95f, 0.35f, 0.35f));
            warningLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            warningLabel.AutoSizeEnabled = false;
            vbox.AddChild(warningLabel);
        }

        // 6. Actions
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 24);
        vbox.AddChild(actions);

        void CloseModal()
        {
            if (!GodotObject.IsInstanceValid(canvasLayer)) return;
            Tween outTween = overlay.CreateTween();
            outTween.TweenProperty(overlay, "modulate", new Color(1, 1, 1, 0), 0.15f)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            
            // Free the entire CanvasLayer once the animation finishes
            outTween.Finished += () => canvasLayer.QueueFree(); 
        }

        canvasLayer.OnEscapeAction = () => {
            onSecondary?.Invoke();
            CloseModal();
        };

        NMainMenuTextButton cancelBtn = CreateMainMenuTextButton(theme, secondaryText, () => {
            onSecondary?.Invoke();
            CloseModal();
        });
        actions.AddChild(cancelBtn);

        NMainMenuTextButton primaryBtn = primaryIsDanger 
            ? CreateMainMenuDangerTextButton(theme, primaryText) 
            : CreateMainMenuTextButton(theme, primaryText);

        primaryBtn.Released += async _ => {
            CloseModal(); 
            try { await onPrimaryAsync(); }
            catch (Exception ex) { GD.PrintErr($"[MainMenuUiHelper] Confirm primary: {ex.Message}"); }
        };
        actions.AddChild(primaryBtn);

        // 7. Fade In
        overlay.Modulate = new Color(1, 1, 1, 0);
        Tween inTween = overlay.CreateTween();
        inTween.TweenProperty(overlay, "modulate", new Color(1, 1, 1, 1), 0.2f)
               .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        return canvasLayer;
    }

    private static void WinFree(Window win)
    {
        if (GodotObject.IsInstanceValid(win))
        {
            win.QueueFree();
        }
    }

    /// <summary>
    /// Vanilla <see cref="NMultiplayerSubmenu"/> only refreshes Host vs Load in <c>_Ready</c>, not when the MP
    /// save slot changes on disk. Call after our Load/Unload so button visibility matches <see cref="MegaCrit.Sts2.Core.Saves.SaveManager.HasMultiplayerRunSave"/>.
    /// </summary>
    public static void TryRefreshMultiplayerSubmenuButtons(Node? sceneRoot)
    {
        if (sceneRoot == null)
        {
            return;
        }

        try
        {
            NMultiplayerSubmenu? sub = FindDescendantMultiplayerSubmenu(sceneRoot);
            if (sub == null || !GodotObject.IsInstanceValid(sub))
            {
                return;
            }

            sub.Call("UpdateButtons");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] TryRefreshMultiplayerSubmenuButtons: {ex.Message}");
        }
    }

    private static NMultiplayerSubmenu? FindDescendantMultiplayerSubmenu(Node node)
    {
        if (node is NMultiplayerSubmenu mp)
        {
            return mp;
        }

        foreach (Node child in node.GetChildren())
        {
            NMultiplayerSubmenu? found = FindDescendantMultiplayerSubmenu(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
