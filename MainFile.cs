using System;
using System.Reflection;
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
            var asm = typeof(MainFile).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string modVer = info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "unknown";

            var godot = Engine.GetVersionInfo();
            string godotVer = $"{godot["major"]}.{godot["minor"]}.{godot["patch"]}";

            Logger.Info($"Don't Abandon Your Friends: mod loading (version {modVer}, Godot {godotVer}).");
        }
        catch (Exception ex)
        {
            Logger.Info($"Don't Abandon Your Friends: mod loading (version unknown: {ex.Message}).");
        }

        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.CallDeferred("add_child", new DontAbandonYourFriendsMenuButton());
        Logger.Info("Don't Abandon Your Friends: main menu button added.");
    }
}
