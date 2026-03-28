using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace DontAbandonYourFriends.UI;

/// <summary>Main-menu button that opens the multiplayer save archive UI.</summary>
public partial class DontAbandonYourFriendsMenuButton : CanvasLayer
{
    private const int LayerZ = 0;

    private Control? _menuRoot;
    private TextureRect? _reticleLeft;
    private TextureRect? _reticleRight;
    private Tween? _reticleTween;
    private DontAbandonYourFriendsScreen _screen = null!;
    private bool _lastMenuContext;

    public override async void _Ready()
    {
        Layer = LayerZ;
        ProcessMode = ProcessModeEnum.Always;

        var root = GetTree()?.Root;
        var existing = root?.FindChild(DontAbandonYourFriendsScreen.SharedScreenNodeName, true, false) as DontAbandonYourFriendsScreen;
        if (existing != null)
        {
            _screen = existing;
        }
        else
        {
            _screen = new DontAbandonYourFriendsScreen { Name = DontAbandonYourFriendsScreen.SharedScreenNodeName };
            root?.AddChild(_screen);
            _screen.Visible = false;
        }

        await ToSignal(GetTree()!, SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree()!, SceneTree.SignalName.ProcessFrame);

        // Safety check to ensure we haven't been destroyed while awaiting
        if (!GodotObject.IsInstanceValid(this)) return; 

        Theme? theme = GameThemeHelper.ResolveMenuTheme(GetTree()?.Root);
        if (theme != null)
        {
            _menuRoot = BuildMainMenuStyleRow(theme);
        }
        else
        {
            _menuRoot = BuildFallbackBottomRow();
            GD.PrintErr("[DontAbandonYourFriends] Menu button: no theme resolved — using plain Button fallback.");
        }

        _menuRoot.Visible = false;
        AddChild(_menuRoot);
        Callable.From(() => MainMenuUiHelper.TryBindMainMenuReticleTextures(_reticleLeft, _reticleRight, GetTree()?.Root)).CallDeferred();
    }

    private Control BuildMainMenuStyleRow(Theme theme)
    {
        // 1. Replaced manual layout math with a proper anchored MarginContainer
        var marginWrap = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        marginWrap.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        marginWrap.GrowHorizontal = Control.GrowDirection.Begin; // Ensures it grows to the left, not off-screen
        marginWrap.GrowVertical = Control.GrowDirection.Begin;
        marginWrap.AddThemeConstantOverride("margin_right", 40);
        marginWrap.AddThemeConstantOverride("margin_bottom", 40);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 4);
        marginWrap.AddChild(row);

        _reticleLeft = CreateReticleTextureRect();
        _reticleRight = CreateReticleTextureRect();

        NMainMenuTextButton menuBtn = MainMenuUiHelper.CreateMainMenuTextButton(
            theme,
            "Don't Abandon Your Friends",
            OpenArchiveScreen,
            new Vector2(380, 44));
        menuBtn.Focused += OnMenuButtonFocused;
        menuBtn.Unfocused += OnMenuButtonUnfocused;

        row.AddChild(_reticleLeft);
        row.AddChild(menuBtn);
        row.AddChild(_reticleRight);

        return marginWrap;
    }

    private Control BuildFallbackBottomRow()
    {
        var marginWrap = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        marginWrap.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        marginWrap.GrowHorizontal = Control.GrowDirection.Begin;
        marginWrap.GrowVertical = Control.GrowDirection.Begin;
        marginWrap.AddThemeConstantOverride("margin_right", 40);
        marginWrap.AddThemeConstantOverride("margin_bottom", 40);

        var fallback = new Button
        {
            Text = "Don't Abandon Your Friends",
            Flat = false,
            CustomMinimumSize = new Vector2(380, 44),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        fallback.AddThemeFontSizeOverride("font_size", MainMenuUiHelper.MenuButtonLabelFontSize);
        fallback.Pressed += OpenArchiveScreen;
        marginWrap.AddChild(fallback);

        return marginWrap;
    }

    private static TextureRect CreateReticleTextureRect()
    {
        return new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = StsColors.transparentWhite,
            CustomMinimumSize = new Vector2(28, 36),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.Keep,
        };
    }

    private void OnMenuButtonFocused(NClickableControl _)
    {
        if (_reticleLeft == null || _reticleRight == null) return;
        _reticleTween?.Kill();
        _reticleTween = CreateTween().SetParallel();
        _reticleTween.TweenProperty(_reticleLeft, "modulate", StsColors.gold, 0.05).From(StsColors.transparentWhite);
        _reticleTween.TweenProperty(_reticleRight, "modulate", StsColors.gold, 0.05).From(StsColors.transparentWhite);
    }

    private void OnMenuButtonUnfocused(NClickableControl _)
    {
        if (_reticleLeft == null || _reticleRight == null) return;
        _reticleTween?.Kill();
        _reticleTween = CreateTween().SetParallel();
        _reticleTween.TweenProperty(_reticleLeft, "modulate", StsColors.transparentWhite, 0.25);
        _reticleTween.TweenProperty(_reticleRight, "modulate", StsColors.transparentWhite, 0.25);
    }

    private void OpenArchiveScreen()
    {
        try { _screen.Open(); }
        catch (Exception ex) { GD.PrintErr($"[DontAbandonYourFriendsMenuButton] Open failed: {ex.Message}"); }
    }

    public override void _Process(double delta)
    {
        if (_menuRoot == null) return;

        bool showButton = IsMainMenuContext(out NMainMenu? activeMainMenu)
            && !IsMainMenuTransitionActive();

        if (showButton != _lastMenuContext)
        {
            _lastMenuContext = showButton;
            _menuRoot.Visible = showButton;
        }

        if (showButton && activeMainMenu != null)
        {
            _menuRoot.Modulate = new Color(1, 1, 1, activeMainMenu.Modulate.A);
        }
    }

    /// <summary>True while <see cref="NTransition.FadeIn"/> / other transitions own the screen (matches vanilla menu obscured state).</summary>
    private static bool IsMainMenuTransitionActive()
    {
        var transition = NGame.Instance?.Transition;
        return transition != null && transition.InTransition;
    }

    private bool IsMainMenuContext(out NMainMenu? activeMainMenu)
    {
        activeMainMenu = null;
        try
        {
            if (NMapScreen.Instance?.IsOpen == true) return false;
            
            // Rely on the global manager rather than searching the tree
            var currentScreen = ActiveScreenContext.Instance?.GetCurrentScreen();
            if (currentScreen is NMainMenu mm && mm.IsVisibleInTree())
            {
                // Extra safety check in case a run is secretly active
                bool hasRunState = RunManager.Instance?.DebugOnlyGetState() != null;
                if (!hasRunState)
                {
                    activeMainMenu = mm;
                    return true;
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}