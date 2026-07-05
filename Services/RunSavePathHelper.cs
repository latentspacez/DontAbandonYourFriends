using System.IO;
using MegaCrit.Sts2.Core.Saves;

namespace DontAbandonYourFriends.Services;

/// <summary>
/// Profile-scoped run save paths under <see cref="ISaveStore"/> without calling
/// <c>RunSaveManager.GetRunSavePath</c>. That API gained a third parameter in StS2 v0.108.0 and
/// removed the two-parameter overload, which breaks a single DLL compiled against v0.107.x.
/// </summary>
internal static class RunSavePathHelper
{
    public static string GetProfileSavePath(int profileId, string fileName) =>
        Path.Combine(
            UserDataPathProvider.GetProfileDir(profileId),
            UserDataPathProvider.SavesDir,
            fileName);
}
