using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using DontAbandonYourFriends.Services;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace DontAbandonYourFriends.UI;

/// <summary>Full-screen modal for multiplayer save archive management.</summary>
public partial class DontAbandonYourFriendsScreen : CanvasLayer
{
    public const string SharedScreenNodeName = "DontAbandonYourFriends_Screen";

    private const int LayerZ = 320;
    private static readonly Color Dim = new(0.05f, 0.04f, 0.03f, 0.92f);
    private static readonly Color Accent = new(0.78f, 0.64f, 0.15f, 1f);

    private const int ModalPanelWidth = 920;
    private const int ModalPanelHeight = 560;
    private const int ScrollAreaMinHeight = 420;
    private const int StatusAreaMinHeight = 52;

    /// <summary>Minimum width for live + archive run cards (matches modal inner width: panel margins are 16px each side).</summary>
    private const int RunCardMinWidth = ModalPanelWidth - 32;

    /// <summary>Started / Last played / Run time — same size on live and archive rows (MegaLabel auto-size off).</summary>
    private const int RunTimingFontSize = 12;

    /// <summary>Act / Floor / Ascension line (live top row + archive second line).</summary>
    private const int RunMetaFontSize = 14;

    private const int ActiveSaveBadgeFontSize = 10;

    /// <summary>Cached resources for UI performance.</summary>
    private static readonly Color TextMutedColor = new(0.75f, 0.72f, 0.68f, 1f);
    private static readonly Color TextMetaColor = new(0.82f, 0.78f, 0.72f, 1f);
    private static readonly Color ActiveBadgeColor = new(0.55f, 0.72f, 0.48f, 1f);

    private static StyleBoxFlat? _archiveRowStyle;
    private static StyleBoxFlat ArchiveRowStyle => _archiveRowStyle ??= new StyleBoxFlat
    {
        BgColor = new Color(0.18f, 0.15f, 0.12f, 1f),
        BorderColor = new Color(Accent.R * 0.6f, Accent.G * 0.6f, Accent.B * 0.6f, 1f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 8, ContentMarginTop = 6, ContentMarginRight = 8, ContentMarginBottom = 6,
    };

    private static StyleBoxFlat? _liveRowStyle;
    private static StyleBoxFlat LiveRowStyle => _liveRowStyle ??= new StyleBoxFlat
    {
        BgColor = new Color(0.14f, 0.18f, 0.14f, 1f),
        BorderColor = new Color(Accent.R * 0.7f, Accent.G * 0.85f, Accent.B * 0.35f, 1f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 8, ContentMarginTop = 6, ContentMarginRight = 8, ContentMarginBottom = 6,
    };


    private Control _rootHost = null!;
    private ColorRect _backdrop = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _rootBox = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _listBox = null!;
    private Label _status = null!;
    private Theme? _theme;

    /// <summary>Adapter to unify Live and Archive preview data for the UI builder.</summary>
    private readonly record struct SavePreviewAdapter(
        string ActDisplay,
        int? Floor,
        string Ascension,
        string? LastPlayedLine,
        string? RunStartedLine,
        string? RunDurationLine,
        IEnumerable<PlayerRowPreview> Players
    );

    public override void _Ready()
    {
        Layer = LayerZ;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _theme = GameThemeHelper.ResolveMenuTheme(GetTree()?.Root);

        // CanvasLayer is not a Control; use a full-viewport root so anchors, centering, and hit-testing behave.
        _rootHost = new Control();
        _rootHost.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _rootHost.OffsetRight = 0;
        _rootHost.OffsetBottom = 0;
        AddChild(_rootHost);

        _backdrop = new ColorRect
        {
            Color = Dim,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _backdrop.OffsetRight = 0;
        _backdrop.OffsetBottom = 0;
        _rootHost.AddChild(_backdrop);
        _backdrop.GuiInput += OnBackdropGuiInput;

        // --- Replace the old panelWrap math with this ---
        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centerContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        _rootHost.AddChild(centerContainer);

        _panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(ModalPanelWidth, ModalPanelHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            ClipContents = true,
        };
        centerContainer.AddChild(_panel);
        MainMenuUiHelper.ApplyModalPanelStyle(_panel);

        _rootBox = new VBoxContainer();
        _rootBox.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(_rootBox);

        var header = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        header.AddThemeConstantOverride("separation", 12);
        _rootBox.AddChild(header);

        if (_theme != null)
        {
            MegaLabel title = MainMenuUiHelper.CreateMegaLabel(
                _theme,
                "Don't Abandon Your Friends",
                26,
                HorizontalAlignment.Left,
                Accent);
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            header.AddChild(title);

            //NMainMenuTextButton unloadBtn = MainMenuUiHelper.CreateMainMenuTextButton(
            //    _theme,
            //    "Unload",
            //    () => { _ = OnUnloadPressedAsync(); });
            //header.AddChild(unloadBtn);

            NMainMenuTextButton closeBtn = MainMenuUiHelper.CreateMainMenuTextButton(
                _theme,
                "Close",
                () => Close());
            header.AddChild(closeBtn);
        }
        else
        {
            var title = new Label
            {
                Text = "Don't Abandon Your Friends",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            title.AddThemeFontSizeOverride("font_size", 26);
            title.AddThemeColorOverride("font_color", Accent);
            header.AddChild(title);

            //var unloadBtn = new Button { Text = "Unload" };
            //StyleButton(unloadBtn);
            //unloadBtn.Pressed += () => { _ = OnUnloadPressedAsync(); };
            //header.AddChild(unloadBtn);

            var closeBtn = new Button { Text = "Close" };
            StyleButton(closeBtn);
            closeBtn.Pressed += Close;
            header.AddChild(closeBtn);
        }

        _scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, ScrollAreaMinHeight),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _rootBox.AddChild(_scroll);

        _listBox = new VBoxContainer();
        _listBox.AddThemeConstantOverride("separation", 10);
        _listBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _scroll.AddChild(_listBox);

        _status = new Label { Text = "" };
        _status.AddThemeFontSizeOverride("font_size", 14);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(0, StatusAreaMinHeight);
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _rootBox.AddChild(_status);

        if (_theme != null)
        {
            _rootHost.Theme = _theme;
            _panel.Theme = _theme;
            GameThemeHelper.ApplyLabelThemeItemsFromTheme(_status, _theme);
            GameThemeHelper.EnsureMainMenuLabelOutline(_status);
            _status.AddThemeFontSizeOverride(LabelThemeKeys.FontSize, 14);
            _status.AddThemeColorOverride(LabelThemeKeys.FontColor, new Color(0.75f, 0.72f, 0.68f));
        }

    }

    private static void ApplyRunCardWidth(PanelContainer frame)
    {
        frame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        frame.CustomMinimumSize = new Vector2(RunCardMinWidth, 0);
    }

    /// <summary>
    /// Wraps <see cref="MainMenuUiHelper.CreateMegaLabel"/> with <c>AutoSizeEnabled = false</c> so archive vs live rows do not scale fonts differently.
    /// </summary>
    private static MegaLabel CreateRunMegaLabel(Theme theme, string text, int fontSize, HorizontalAlignment align, Color color)
    {
        MegaLabel label = MainMenuUiHelper.CreateMegaLabel(theme, text, fontSize, align, color);
        label.AutoSizeEnabled = false;
        return label;
    }

    private static void StyleButton(Button b, Theme? theme = null, Vector2? minSize = null, int fontSize = MainMenuUiHelper.MenuButtonLabelFontSize)
    {
        b.CustomMinimumSize = minSize ?? MainMenuUiHelper.DefaultMenuButtonMinSize;
        b.AddThemeFontSizeOverride("font_size", fontSize);
        if (theme != null) GameThemeHelper.ApplyButtonFontFromTheme(b, theme);
    }

    private void OnBackdropGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            Close();
        }
    }

    public void Open()
    {
        try
        {
            if (!Visible)
            {
                Visible = true;
                _rootHost.Modulate = new Color(1, 1, 1, 0); 
                Tween tween = CreateTween();
                tween.TweenProperty(_rootHost, "modulate", new Color(1, 1, 1, 1), 0.2f)
                     .SetTrans(Tween.TransitionType.Sine)
                     .SetEase(Tween.EaseType.Out);
                
                _ = RefreshListAsync(showLoadingText: true);
            }
            else
            {
                _ = RefreshListAsync(showLoadingText: false);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Open: {ex.Message}");
        }
    }

    public void Close()
    {
        // Fade out, then hide
        Tween tween = CreateTween();
        tween.TweenProperty(_rootHost, "modulate", new Color(1, 1, 1, 0), 0.15f)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.In);
             
        tween.Finished += () => Visible = false;
    }

    private async Task RefreshListAsync(bool showLoadingText = true)
    {
        if (showLoadingText)
        {
            _status.Text = "Loading…";
        }

        try
        {
            ISaveStore? store = SaveStoreAccessor.TryGetSaveStore();
            if (store == null)
            {
                _status.Text = "Could not access save store.";
                return;
            }

            var svc = new MultiplayerSaveArchiveService(store);

            // 1. Fetch the data from the disk FIRST.
            // Do NOT touch the UI while this async operation is running.
            ArchiveListResult list = await svc.ListArchivesAsync();

            // 2. Data is ready! Now we clear and rebuild in the exact same frame.
            // Godot will process the QueueFree and AddChild operations before the next draw call, completely eliminating the flicker.
            foreach (Node n in _listBox.GetChildren())
            {
                n.QueueFree();
            }

            if (list.Live.HasSave)
            {
                var p = list.Live.Preview;
                var unifiedPreview = new SavePreviewAdapter(
                    p.ActDisplay.ToString(), 
                    p.Floor, 
                    p.Ascension.ToString(), 
                    p.LastPlayedLine, 
                    p.RunStartedLine, 
                    p.RunDurationLine, 
                    p.Players
                );
                _listBox.AddChild(BuildSaveRow(svc, unifiedPreview, null, isLive: true));
            }
            var sortedArchives = list.Archives.OrderByDescending(ArchiveRowNewestFirstKey);

            foreach (ArchiveListRow row in sortedArchives)
            {
                var p = row.Preview;
                var unifiedPreview = new SavePreviewAdapter(
                    p.ActDisplay.ToString(), 
                    p.Floor, 
                    p.Ascension.ToString(), 
                    p.LastPlayedLine, 
                    p.RunStartedLine, 
                    p.RunDurationLine, 
                    p.Players
                );
                _listBox.AddChild(BuildSaveRow(svc, unifiedPreview, row.Entry, isLive: false));
            }

            string baseStatus = list.Archives.Count == 0
                ? "No archived saves yet. Loading a different backup will auto-save your current multiplayer run here first."
                : $"{list.Archives.Count} save(s) in archive. Loading a run backs up the live slot if it differs.";
            
            if (list.Live.HasSave)
            {
                baseStatus += " Live slot has an active multiplayer run.";
            }

            _status.Text = baseStatus;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] RefreshListAsync: {ex.Message}");
            _status.Text = "Failed to load list.";
        }
    }

    /// <summary>Uses raw run <c>save_time</c> from preview when present; otherwise archive index <c>createdUtc</c>.</summary>
    private static long ArchiveRowNewestFirstKey(ArchiveListRow row)
    {
        long t = row.Preview.SaveTime;
        if (t > 0)
        {
            // Match game JSON: seconds vs milliseconds (see RunSavePreviewFactory.TryParseUnixish).
            return t > 10_000_000_000L ? t : t * 1000L;
        }

        if (DateTime.TryParse(row.Entry.CreatedUtc, null, DateTimeStyles.RoundtripKind, out DateTime createdUtc))
        {
            return new DateTimeOffset(createdUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();
        }

        return 0;
    }

    private Control CreateTextElement(string text, int fontSize, HorizontalAlignment align, Color color, bool expandFill = false)
    {
        if (_theme != null)
        {
            var mega = CreateRunMegaLabel(_theme, text, fontSize, align, color);
            if (expandFill) mega.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return mega;
        }

        var lbl = new Label 
        { 
            Text = text, 
            HorizontalAlignment = align,
            SizeFlagsHorizontal = expandFill ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill
        };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }


    private Control BuildSaveRow(MultiplayerSaveArchiveService svc, SavePreviewAdapter preview, ArchiveIndexEntry? archiveEntry, bool isLive)
    {
        var frame = new PanelContainer();
        // Dynamically apply the correct cached style
        frame.AddThemeStyleboxOverride("panel", isLive ? LiveRowStyle : ArchiveRowStyle);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        frame.AddChild(col);

        // --- ZONE 1: HEADER ---
        var headerRow = new HBoxContainer();
        col.AddChild(headerRow);

        var headerLeft = new VBoxContainer();
        headerLeft.AddThemeConstantOverride("separation", -2);
        string floorText = preview.Floor.HasValue ? preview.Floor.Value.ToString() : "—";
        headerLeft.AddChild(CreateTextElement($"Act {preview.ActDisplay} · Floor {floorText}", 20, HorizontalAlignment.Left, StsColors.cream));
        headerLeft.AddChild(CreateTextElement($"Ascension {preview.Ascension}", 12, HorizontalAlignment.Left, TextMutedColor));
        headerRow.AddChild(headerLeft);

        headerRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var headerRight = new VBoxContainer();
        headerRight.AddThemeConstantOverride("separation", 0);
        
        // Live saves prefer LastPlayed but fallback to RunStarted. Archives just show LastPlayed.
        string? timeLine = isLive 
            ? (!string.IsNullOrEmpty(preview.LastPlayedLine) ? preview.LastPlayedLine : preview.RunStartedLine) 
            : preview.LastPlayedLine;
            
        if (!string.IsNullOrEmpty(timeLine))
        {
            headerRight.AddChild(CreateTextElement(timeLine, 12, HorizontalAlignment.Right, TextMutedColor));
        }
        if (!string.IsNullOrEmpty(preview.RunDurationLine))
        {
            headerRight.AddChild(CreateTextElement(preview.RunDurationLine, 11, HorizontalAlignment.Right, TextMutedColor));
        }
        headerRow.AddChild(headerRight);

        // --- DIVIDER ---
        var divider = new HSeparator();
        divider.Modulate = new Color(1, 1, 1, 0.15f);
        col.AddChild(divider);

        // --- ZONE 2: BODY (Players) ---
        var rosterMargin = new MarginContainer();
        rosterMargin.AddThemeConstantOverride("margin_left", 16);
        rosterMargin.AddThemeConstantOverride("margin_top", 4);
        rosterMargin.AddThemeConstantOverride("margin_bottom", 4);
        col.AddChild(rosterMargin);

        var playersRow = new HBoxContainer();
        playersRow.AddThemeConstantOverride("separation", 24);
        rosterMargin.AddChild(playersRow);

        foreach (var p in preview.Players)
        {
            playersRow.AddChild(BuildPlayerStrip(p));
        }

        // --- ZONE 3: FOOTER (Actions) ---
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 0);
        col.AddChild(actions);

        if (isLive)
        {
            var badgeMargin = new MarginContainer();
            badgeMargin.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            badgeMargin.AddThemeConstantOverride("margin_bottom", 4); // Adjusted for the shorter button!
            badgeMargin.AddChild(CreateTextElement("ACTIVE SAVE", 12, HorizontalAlignment.Left, ActiveBadgeColor));
            actions.AddChild(badgeMargin);

            actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            if (_theme != null) {
                actions.AddChild(MainMenuUiHelper.CreateMainMenuTextButton(_theme, "Unload", () => { _ = OnUnloadPressedAsync(); }, minSize: MainMenuUiHelper.SmallMenuButtonMinSize, fontSize: 14));
            } else {
                var unloadBtn = new Button { Text = "Unload" };
                StyleButton(unloadBtn, minSize: MainMenuUiHelper.SmallMenuButtonMinSize, fontSize: 14);
                unloadBtn.Pressed += () => { _ = OnUnloadPressedAsync(); };
                actions.AddChild(unloadBtn);
            }
        }
        else if (archiveEntry != null)
        {
            if (_theme != null) {
                actions.AddChild(MainMenuUiHelper.CreateMainMenuDangerTextButton(_theme, "Delete", () => OnDeletePressed(svc, archiveEntry), minSize: MainMenuUiHelper.SmallMenuButtonMinSize, fontSize: 14));
            } else {
                var d = new Button { Text = "Delete" };
                MainMenuUiHelper.ApplyDangerButtonStyle(d, null, minSize: MainMenuUiHelper.SmallMenuButtonMinSize, fontSize: 14);
                d.Pressed += () => OnDeletePressed(svc, archiveEntry);
                actions.AddChild(d);
            }

            actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            if (_theme != null) {
                actions.AddChild(MainMenuUiHelper.CreateMainMenuTextButton(_theme, "Load", () => { _ = OnLoadPressed(svc, archiveEntry); }, minSize: MainMenuUiHelper.SmallMenuButtonMinSize, fontSize: 14));
            } else {
                var l = new Button { Text = "Load" };
                StyleButton(l, minSize: MainMenuUiHelper.SmallMenuButtonMinSize, fontSize: 14);
                l.Pressed += () => { _ = OnLoadPressed(svc, archiveEntry); };
                actions.AddChild(l);
            }
        }

        ApplyRunCardWidth(frame);
        return frame;
    }


    private Control BuildRow(MultiplayerSaveArchiveService svc, ArchiveListRow row)
    {
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", ArchiveRowStyle);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        frame.AddChild(col);

        // --- ZONE 1: HEADER ---
        var headerRow = new HBoxContainer();
        col.AddChild(headerRow);

        // Left Header: Act/Floor & Ascension
        var headerLeft = new VBoxContainer();
        headerLeft.AddThemeConstantOverride("separation", -2); // Tight vertical spacing
        string floorText = row.Preview.Floor.HasValue ? row.Preview.Floor.Value.ToString() : "—";
        headerLeft.AddChild(CreateTextElement($"Act {row.Preview.ActDisplay} · Floor {floorText}", 20, HorizontalAlignment.Left, StsColors.cream));
        headerLeft.AddChild(CreateTextElement($"Ascension {row.Preview.Ascension}", 12, HorizontalAlignment.Left, TextMutedColor));
        headerRow.AddChild(headerLeft);

        // Header Spring (pushes timing to the far right)
        headerRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // Right Header: Timing & Duration
        var headerRight = new VBoxContainer();
        headerRight.AddThemeConstantOverride("separation", 0);
        if (!string.IsNullOrEmpty(row.Preview.LastPlayedLine))
        {
            headerRight.AddChild(CreateTextElement(row.Preview.LastPlayedLine, 12, HorizontalAlignment.Right, TextMutedColor));
        }
        if (!string.IsNullOrEmpty(row.Preview.RunDurationLine))
        {
            // Smaller font for duration, right aligned underneath Last Played
            headerRight.AddChild(CreateTextElement(row.Preview.RunDurationLine, 11, HorizontalAlignment.Right, TextMutedColor)); 
        }
        headerRow.AddChild(headerRight);

        // --- DIVIDER ---
        var divider = new HSeparator();
        divider.Modulate = new Color(1, 1, 1, 0.15f); // 15% opacity white for a subtle line
        col.AddChild(divider);

        // --- ZONE 2: BODY (Players) ---
        var rosterMargin = new MarginContainer();
        rosterMargin.AddThemeConstantOverride("margin_left", 16); // Indent the roster!
        rosterMargin.AddThemeConstantOverride("margin_top", 4);
        rosterMargin.AddThemeConstantOverride("margin_bottom", 4);
        col.AddChild(rosterMargin);

        var playersRow = new HBoxContainer();
        playersRow.AddThemeConstantOverride("separation", 24); // Give players room to breathe
        rosterMargin.AddChild(playersRow);

        foreach (PlayerRowPreview p in row.Preview.Players)
        {
            playersRow.AddChild(BuildPlayerStrip(p));
        }

        // --- ZONE 3: FOOTER (Actions) ---
        var actions = new HBoxContainer(); // Default alignment spreads items from the left
        actions.AddThemeConstantOverride("separation", 0);
        col.AddChild(actions);

        Control loadBtn, delBtn;
        if (_theme != null)
        {
            loadBtn = MainMenuUiHelper.CreateMainMenuTextButton(_theme, "Load", () => { _ = OnLoadPressed(svc, row.Entry); });
            delBtn = MainMenuUiHelper.CreateMainMenuDangerTextButton(_theme, "Delete", () => OnDeletePressed(svc, row.Entry));
        }
        else
        {
            var l = new Button { Text = "Load" };
            StyleButton(l);
            l.Pressed += () => _ = OnLoadPressed(svc, row.Entry);
            loadBtn = l;

            var d = new Button { Text = "Delete" };
            MainMenuUiHelper.ApplyDangerButtonStyle(d, null);
            d.Pressed += () => OnDeletePressed(svc, row.Entry);
            delBtn = d;
        }

        // 1. Add Delete to the far left
        actions.AddChild(delBtn);

        // 2. Add a spring to push the next item to the far right
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // 3. Add Load to the far right
        actions.AddChild(loadBtn);

        ApplyRunCardWidth(frame);
        return frame;
    }

    private Control BuildLiveRow(LiveMultiplayerSlotInfo live)
    {
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", LiveRowStyle);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        frame.AddChild(col);

        // --- ZONE 1: HEADER ---
        var headerRow = new HBoxContainer();
        col.AddChild(headerRow);

        // Left Header
        var headerLeft = new VBoxContainer();
        headerLeft.AddThemeConstantOverride("separation", -2);
        string floorText = live.Preview.Floor.HasValue ? live.Preview.Floor.Value.ToString() : "—";
        headerLeft.AddChild(CreateTextElement($"Act {live.Preview.ActDisplay} · Floor {floorText}", 20, HorizontalAlignment.Left, StsColors.cream));
        headerLeft.AddChild(CreateTextElement($"Ascension {live.Preview.Ascension}", 12, HorizontalAlignment.Left, TextMutedColor));
        headerRow.AddChild(headerLeft);

        headerRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // Right Header: Badge & Timing
        var headerRight = new VBoxContainer();
        headerRight.AddThemeConstantOverride("separation", 0);
        //headerRight.AddChild(CreateTextElement("ACTIVE SAVE", 11, HorizontalAlignment.Right, ActiveBadgeColor));
        
        // Use RunStartedLine or LastPlayedLine depending on what's available
        string? timeLine = !string.IsNullOrEmpty(live.Preview.LastPlayedLine) ? live.Preview.LastPlayedLine : live.Preview.RunStartedLine;
        if (!string.IsNullOrEmpty(timeLine))
        {
            headerRight.AddChild(CreateTextElement(timeLine, 12, HorizontalAlignment.Right, TextMutedColor));
        }
        if (!string.IsNullOrEmpty(live.Preview.RunDurationLine))
        {
            headerRight.AddChild(CreateTextElement(live.Preview.RunDurationLine, 11, HorizontalAlignment.Right, TextMutedColor));
        }
        headerRow.AddChild(headerRight);

        // --- DIVIDER ---
        var divider = new HSeparator();
        divider.Modulate = new Color(1, 1, 1, 0.15f);
        col.AddChild(divider);

        // --- ZONE 2: BODY (Players) ---
        var rosterMargin = new MarginContainer();
        rosterMargin.AddThemeConstantOverride("margin_left", 16);
        rosterMargin.AddThemeConstantOverride("margin_top", 4);
        rosterMargin.AddThemeConstantOverride("margin_bottom", 4);
        col.AddChild(rosterMargin);

        var playersRow = new HBoxContainer();
        playersRow.AddThemeConstantOverride("separation", 24);
        rosterMargin.AddChild(playersRow);

        foreach (PlayerRowPreview p in live.Preview.Players)
        {
            playersRow.AddChild(BuildPlayerStrip(p));
        }

        // --- ZONE 3: FOOTER (Actions & Badge) ---
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 0);
        col.AddChild(actions);

        // 1. Wrap the badge in a MarginContainer to nudge it precisely into place
        var badgeMargin = new MarginContainer();
        badgeMargin.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd; // Push to the absolute bottom
        badgeMargin.AddThemeConstantOverride("margin_bottom", 8); // Nudge it up 8px to align with button text
        
        Control activeBadge = CreateTextElement("ACTIVE SAVE", 12, HorizontalAlignment.Left, ActiveBadgeColor);
        badgeMargin.AddChild(activeBadge);
        actions.AddChild(badgeMargin);

        // 2. Add a spring to push the Unload button to the far right
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // 3. Add the Unload button
        if (_theme != null)
        {
            NMainMenuTextButton unloadBtn = MainMenuUiHelper.CreateMainMenuTextButton(
                _theme, 
                "Unload", 
                () => { _ = OnUnloadPressedAsync(); });
            actions.AddChild(unloadBtn);
        }
        else
        {
            var unloadBtn = new Button { Text = "Unload" };
            StyleButton(unloadBtn);
            unloadBtn.Pressed += () => { _ = OnUnloadPressedAsync(); };
            actions.AddChild(unloadBtn);
        }

        ApplyRunCardWidth(frame);
        return frame;
    }

    private Control BuildPlayerStrip(PlayerRowPreview p)
    {
        var box = new VBoxContainer();
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        box.AddChild(row);

        var texRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        try
        {
            string id = p.CharacterIdEntry.Trim().ToLowerInvariant();
            string path = ImageHelper.GetImagePath($"ui/top_panel/character_icon_{id}.png");
            if (PreloadManager.Cache.GetTexture2D(path) is { } tex)
            {
                texRect.Texture = tex;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Icon: {ex.Message}");
        }

        row.AddChild(texRect);

        var textCol = new VBoxContainer();
        textCol.AddThemeConstantOverride("separation", 0); // Tighten the gap between name and stats
        row.AddChild(textCol);
        
        string who = !string.IsNullOrWhiteSpace(p.DisplayName) 
            ? p.DisplayName!
            : (p.PlayerIndex > 0 ? $"Player {p.PlayerIndex}" : "Player");

        // --- Update font size to 14 ---
        Control nameLabel = CreateTextElement(who, 14, HorizontalAlignment.Left, StsColors.cream, expandFill: true);
        nameLabel.CustomMinimumSize = new Vector2(170, 0);
        
        if (nameLabel is MegaLabel megaName) megaName.AutoSizeEnabled = false;
        if (nameLabel is Label lblName)
        {
            lblName.AutowrapMode = TextServer.AutowrapMode.Off;
            lblName.ClipContents = true;
            lblName.MaxLinesVisible = 1;
        }
        textCol.AddChild(nameLabel);

        // --- Update font size to 11 ---
        string statsText = $"HP {p.CurrentHp}/{p.MaxHp} · {p.DeckCount} cards · {p.RelicCount} relics";
        Control statsLabel = CreateTextElement(statsText, 11, HorizontalAlignment.Left, TextMutedColor);
        
        if (statsLabel is MegaLabel megaStats) megaStats.AutoSizeEnabled = false;
        if (statsLabel is Label lblStats)
        {
            lblStats.AutowrapMode = TextServer.AutowrapMode.Off;
            lblStats.ClipContents = true;
        }
        textCol.AddChild(statsLabel);

        return box;
    }

    private async Task OnUnloadPressedAsync()
    {
        try
        {
            GD.Print("[DontAbandonYourFriends] [UI] Unload: button pressed — starting UnloadLiveToArchiveAndClearAsync.");
            ISaveStore? store = SaveStoreAccessor.TryGetSaveStore();
            if (store == null)
            {
                GD.PrintErr("[DontAbandonYourFriends] [UI] Unload: SaveStoreAccessor returned null.");
                _status.Text = "Could not access save store.";
                return;
            }

            _status.Text = "Archiving live run, then clearing slot…";
            var svc = new MultiplayerSaveArchiveService(store);
            string? err = await svc.UnloadLiveToArchiveAndClearAsync();
            _status.Text = err ?? "Live run archived. Slot cleared — you can start a new multiplayer run or Load a backup.";
            if (err == null)
            {
                await RefreshListAsync();
                await TryNotifyVanillaMultiplayerSubmenuAfterSaveStateChangeAsync();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] [UI] Unload: exception — {ex}");
            _status.Text = "Unload failed.";
        }
    }

    private async Task OnLoadPressed(MultiplayerSaveArchiveService svc, ArchiveIndexEntry entry)
    {
        try
        {
            _status.Text = "Backing up live run if needed, then writing save and syncing cloud…";
            string? err = await svc.LoadArchiveIntoGameAsync(entry);
            
            if (err != null)
            {
                _status.Text = err;
                return;
            }

            _status.Text = "Loaded into the multiplayer run save file (Steam Cloud updated). That archive entry was removed so the run is not duplicated — use Continue or the multiplayer menu as usual.";
            
            // Pass false to avoid flashing "Loading..." and destroying our success message
            await RefreshListAsync(showLoadingText: false); 
            await TryNotifyVanillaMultiplayerSubmenuAfterSaveStateChangeAsync();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] [UI] Load: exception — {ex}");
            _status.Text = "Load failed.";
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Only listen for Escape if our screen is actually open
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            // 1. Consume the event IMMEDIATELY at the front of the pipeline
            GetViewport().SetInputAsHandled();
            
            // 2. Trigger your close logic
            Close();
        }
    }

    /// <summary>
    /// Let the save store settle, then refresh vanilla multiplayer Host/Load buttons — they only update in <c>_Ready</c> otherwise.
    /// </summary>
    private async Task TryNotifyVanillaMultiplayerSubmenuAfterSaveStateChangeAsync()
    {
        SceneTree? tree = GetTree();
        if (tree == null)
        {
            return;
        }

        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        MainMenuUiHelper.TryRefreshMultiplayerSubmenuButtons(tree.Root);
    }

    private void OnDeletePressed(MultiplayerSaveArchiveService svc, ArchiveIndexEntry entry)
    {
        try
        {
            Func<Task> executeDeleteAsync = async () =>
            {
                try
                {
                    await svc.DeleteAsync(entry);
                    if (GodotObject.IsInstanceValid(this) && Visible)
                    {
                        await RefreshListAsync(showLoadingText: false);
                    }
                }
                catch (Exception ex) { GD.PrintErr($"[DontAbandonYourFriends] Delete execution failed: {ex.Message}"); }
            };

            if (_theme != null)
            {
                CanvasLayer overlay = MainMenuUiHelper.CreateConfirmationOverlay(
                    _theme,
                    "Delete Archive",
                    "Are you sure you want to permanently delete this archived run?",
                    "This cannot be undone.", // <-- This line is now explicitly passed as the red warning!
                    "Delete",
                    "Cancel",
                    primaryIsDanger: true,
                    onPrimaryAsync: executeDeleteAsync
                );
                
                GetTree()?.Root.AddChild(overlay);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Delete confirm setup: {ex.Message}");
        }
    }
}
