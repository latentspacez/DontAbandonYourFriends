using System;
using Godot;
using DontAbandonYourFriends.UI;
using MegaCrit.Sts2.Core.Modding;

namespace DontAbandonYourFriends;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "DontAbandonYourFriends";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static bool IsModEnabled { get; private set; }

    public static void Initialize()
    {
        IsModEnabled = true;

        try
        {
            string modVer = ModVersionInfo.GetInformationalVersion();
            Logger.Info($"Don't Abandon Your Friends: mod loading (version {modVer}, Godot {GameCompatibility.GetGodotVersionSummary()}).");
        }
        catch (Exception ex)
        {
            Logger.Info($"Don't Abandon Your Friends: mod loading (version unknown: {ex.Message}).");
        }

        if (!GameCompatibility.IsSupportedGameBuild(out string compatDetail))
        {
            IsModEnabled = false;
            Logger.Error($"Don't Abandon Your Friends: incompatible game build — mod disabled. {compatDetail}");
            GD.PrintErr($"[{ModId}] Incompatible game build — mod disabled. {compatDetail}");
            return;
        }

        Logger.Info($"Don't Abandon Your Friends: game compatibility OK. {compatDetail}");

        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.CallDeferred("add_child", new DontAbandonYourFriendsMenuButton());
        Logger.Info("Don't Abandon Your Friends: main menu button added.");
    }
}
