using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace DontAbandonYourFriends.Services;

internal sealed class MultiplayerSaveArchiveService
{
    private const string LogTag = "[DontAbandonYourFriends]";

    /// <summary>Account-global path used before per-profile archives (migration source only).</summary>
    private const string GlobalArchiveRoot = "mods/dont_abandon_your_friends/archives";

    /// <summary>Previous mod archive path (Keep Your Saves); migration source only.</summary>
    private const string LegacyArchiveRelativeRoot = "mods/keep_your_saves/archives";

    private readonly ISaveStore _store;

    public MultiplayerSaveArchiveService(ISaveStore store)
    {
        _store = store;
    }

    private static void LogFlow(string operation, string message)
    {
        GD.Print($"{LogTag} [{operation}] {message}");
    }

    private static void LogFlowErr(string operation, string message)
    {
        GD.PrintErr($"{LogTag} [{operation}] {message}");
    }

    /// <summary>
    /// Same scoping as <see cref="RunSaveManager.GetRunSavePath"/>: under
    /// <see cref="UserDataPathProvider.GetProfileDir"/> so each profile has its own archive (ISaveStore root is account-wide).
    /// </summary>
    private static string ArchiveRootForProfile(int profileId)
    {
        string dir = UserDataPathProvider.GetProfileDir(profileId);
        return NormalizeStorePath(Path.Combine(dir, "mods", "dont_abandon_your_friends", "archives"));
    }

    /// <summary>
    /// <see cref="ISaveStore"/> / Steam use forward-slash paths (see game INFO logs); Windows
    /// <see cref="Path.Combine"/> yields backslashes — mismatched keys break FileExists/Delete vs writes.
    /// </summary>
    private static string NormalizeStorePath(string path) => path.Replace('\\', '/');

    /// <inheritdoc cref="RunSaveManager.GetRunSavePath"/>
    private static string NormalizedRunSavePath(int profileId, string fileName) =>
        NormalizeStorePath(RunSaveManager.GetRunSavePath(profileId, fileName));

    private string ArchiveRoot => ArchiveRootForProfile(SaveManager.Instance.CurrentProfileId);

    private string IndexPath => $"{ArchiveRoot}/index.json";

    private string EntryStoragePath(string storageFileName) => $"{ArchiveRoot}/{storageFileName}";

    public async Task EnsureLayoutAsync()
    {
        try
        {
            if (!_store.DirectoryExists(ArchiveRoot))
            {
                try
                {
                    _store.CreateDirectory(ArchiveRoot);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DontAbandonYourFriends] CreateDirectory failed ({ArchiveRoot}): {ex.Message}");
                    return;
                }
            }

            await MigrateGlobalArchiveToProfileIfNeededAsync();
            await MigrateLegacyKeepYourSavesArchiveIfNeededAsync();

            if (!_store.FileExists(IndexPath))
            {
                string? idxErr = await WriteIndexAsync(new ArchiveIndexDocument());
                if (idxErr != null)
                {
                    GD.PrintErr($"[DontAbandonYourFriends] Could not create archive index: {idxErr}");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] EnsureLayoutAsync: {ex.Message}");
        }
    }

    /// <summary>Migrate from account-global mods/dont_abandon_your_friends/archives (pre–per-profile layout).</summary>
    private async Task MigrateGlobalArchiveToProfileIfNeededAsync()
    {
        if (_store.FileExists(IndexPath))
        {
            return;
        }

        string globalIndex = $"{GlobalArchiveRoot}/index.json";
        if (!_store.FileExists(globalIndex))
        {
            return;
        }

        try
        {
            await CopyArchiveTreeAsync(GlobalArchiveRoot, ArchiveRoot);
            GD.Print($"[DontAbandonYourFriends] Migrated archive from {GlobalArchiveRoot} to profile-scoped {ArchiveRoot}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Global-to-profile archive migration failed: {ex.Message}");
        }
    }

    /// <summary>Migrate from mods/keep_your_saves/archives if profile archive still empty.</summary>
    private async Task MigrateLegacyKeepYourSavesArchiveIfNeededAsync()
    {
        if (_store.FileExists(IndexPath))
        {
            return;
        }

        string legacyIndexPath = $"{LegacyArchiveRelativeRoot}/index.json";
        if (!_store.FileExists(legacyIndexPath))
        {
            return;
        }

        try
        {
            await CopyArchiveTreeAsync(LegacyArchiveRelativeRoot, ArchiveRoot);
            GD.Print($"[DontAbandonYourFriends] Migrated archive from {LegacyArchiveRelativeRoot} to {ArchiveRoot}.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Legacy KeepYourSaves archive migration failed: {ex.Message}");
        }
    }

    private async Task CopyArchiveTreeAsync(string sourceRoot, string destRoot)
    {
        string sourceIndex = $"{sourceRoot}/index.json";
        string? legacyIndexText = await _store.ReadFileAsync(sourceIndex);
        if (string.IsNullOrWhiteSpace(legacyIndexText))
        {
            return;
        }

        ArchiveIndexDocument? legacyDoc = JsonSerializer.Deserialize<ArchiveIndexDocument>(legacyIndexText, DontAbandonJson.Indented);
        if (legacyDoc?.Entries == null || legacyDoc.Entries.Count == 0)
        {
            return;
        }

        if (!_store.DirectoryExists(destRoot))
        {
            _store.CreateDirectory(destRoot);
        }

        foreach (ArchiveIndexEntry e in legacyDoc.Entries)
        {
            string legacyFile = $"{sourceRoot}/{e.StorageFileName}";
            string dest = $"{destRoot}/{e.StorageFileName}";
            if (!_store.FileExists(legacyFile) || _store.FileExists(dest))
            {
                continue;
            }

            string? body = _store.ReadFile(legacyFile);
            if (string.IsNullOrEmpty(body))
            {
                continue;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(body);
            string? err = await SaveStoreWriteHelper.WriteBytesAsync(_store, dest, bytes);
            if (err != null)
            {
                GD.PrintErr($"[DontAbandonYourFriends] Migration copy failed: {err}");
            }
        }

        string? idxErr = await SaveStoreWriteHelper.WriteUtf8Async(_store, $"{destRoot}/index.json", legacyIndexText);
        if (idxErr != null)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Migration index write failed: {idxErr}");
        }
    }

    public async Task<ArchiveListResult> ListArchivesAsync()
    {
        await EnsureLayoutAsync();
        await EnsureLiveSlotMetadataAsync();

        LiveMultiplayerSlotInfo live = await BuildLiveSlotInfoAsync();

        ArchiveIndexDocument index = await ReadIndexAsync();
        var rows = new List<ArchiveListRow>();
        foreach (ArchiveIndexEntry e in index.Entries)
        {
            string path = EntryStoragePath(e.StorageFileName);
            string? json = _store.ReadFile(path);
            MultiplayerSaveMetaDocument? meta = await ReadOrMigrateArchiveMetaAsync(e.StorageFileName, json);
            RunSavePreview preview = string.IsNullOrEmpty(json)
                ? RunSavePreviewFactory.EmptyPreview()
                : RunSavePreviewFactory.Build(json, meta);
            rows.Add(new ArchiveListRow { Entry = e, Preview = preview });
        }

        return new ArchiveListResult { Live = live, Archives = rows };
    }

    /// <summary>
    /// Writes an archived run into the live multiplayer slot. If the live slot already holds a different run,
    /// that file is appended to the mod archive first (same as a manual import) so it can be restored later.
    /// On success, the loaded archive row and its files are removed — the live slot is the active copy; keeping
    /// both would duplicate the same run (e.g. after a later Unload).
    /// Operations are sequential to avoid overlapping reads/writes on the live file.
    /// </summary>
    public async Task<string?> LoadArchiveIntoGameAsync(ArchiveIndexEntry entry)
    {
        const string op = "Load";
        await EnsureLayoutAsync();
        int profileId = SaveManager.Instance.CurrentProfileId;
        string mpPath = NormalizedRunSavePath(profileId, RunSaveManager.multiplayerRunSaveFileName);
        string archivePath = EntryStoragePath(entry.StorageFileName);
        LogFlow(
            op,
            $"start profileId={profileId} entryId={entry.Id} createdUtc={entry.CreatedUtc} archiveFile={entry.StorageFileName} livePath={mpPath} archivePath={archivePath}");

        string? archiveJson = _store.ReadFile(archivePath);
        if (string.IsNullOrWhiteSpace(archiveJson))
        {
            LogFlowErr(op, "aborted: archive JSON missing or empty after read.");
            return "Archive file is missing or empty.";
        }

        LogFlow(op, $"read archive JSON: {archiveJson.Length} chars (content not logged).");

        string? liveJson = _store.ReadFile(mpPath);
        bool liveExisted = !string.IsNullOrWhiteSpace(liveJson);
        if (!liveExisted)
        {
            LogFlow(op, "no existing live MP save file (or empty) before write.");
        }
        else
        {
            LogFlow(op, $"existing live save: {liveJson!.Length} chars; same as archive={string.Equals(liveJson, archiveJson, StringComparison.Ordinal)}");
        }

        if (!string.IsNullOrWhiteSpace(liveJson) && !string.Equals(liveJson, archiveJson, StringComparison.Ordinal))
        {
            LogFlow(op, "live run differs from selected archive — appending live run to mod archive first (auto-backup).");
            MultiplayerSaveMetaDocument? liveMetaBefore = await TryReadMetaDocumentAsync(
                NormalizedRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName));
            string? backupErr = await AppendArchiveEntryAsync(liveJson, liveMetaBefore);
            if (backupErr != null)
            {
                LogFlowErr(op, $"auto-backup failed: {backupErr}");
                return backupErr;
            }

            LogFlow(op, "auto-backup row appended OK.");
        }
        else
        {
            LogFlow(op, "skipped auto-backup (no live file, or identical to archive).");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(archiveJson);
        string? writeErr = await SaveStoreWriteHelper.WriteBytesAsync(_store, mpPath, bytes);
        if (writeErr != null)
        {
            LogFlowErr(op, $"WriteBytesAsync live MP save failed: {writeErr}");
            return $"Could not write live multiplayer save: {writeErr}";
        }

        LogFlow(op, $"wrote live MP save: {bytes.Length} bytes to {mpPath}");

        // Push MP save to cloud before any other await — SyncCloudToLocal deletes local when cloud has no copy;
        // a gap before the full reconcile at the end of this method could wipe the file we just wrote.
        await TryReconcileCloudForMpSaveOnlyAsync(mpPath, op);

        string liveMetaPath = NormalizedRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName);
        string archiveMetaPath = EntryStoragePath(MetaFileNameForSave(entry.StorageFileName));
        if (_store.FileExists(archiveMetaPath))
        {
            LogFlow(op, $"live meta: copying from archive sidecar path={archiveMetaPath}");
            string? metaText = _store.ReadFile(archiveMetaPath);
            if (!string.IsNullOrEmpty(metaText))
            {
                string? metaErr = await SaveStoreWriteHelper.WriteUtf8Async(_store, liveMetaPath, metaText);
                if (metaErr != null)
                {
                    LogFlowErr(op, $"WriteUtf8Async live meta (from archive) failed: {metaErr}");
                }
                else
                {
                    LogFlow(op, $"wrote live meta sidecar: {metaText.Length} chars to {liveMetaPath}");
                }
            }
            else
            {
                LogFlow(op, "archive meta file exists but read returned empty — skipping live meta write.");
            }
        }
        else
        {
            LogFlow(op, "no archive meta sidecar — building live meta from run JSON.");
            MultiplayerSaveMetaDocument metaDoc = MultiplayerSaveMetadataFactory.BuildFromRunJson(archiveJson, null);
            string? metaErr = await SaveStoreWriteHelper.WriteUtf8Async(
                _store,
                liveMetaPath,
                JsonSerializer.Serialize(metaDoc, DontAbandonJson.Indented));
            if (metaErr != null)
            {
                LogFlowErr(op, $"WriteUtf8Async live meta (built) failed: {metaErr}");
            }
            else
            {
                LogFlow(op, $"wrote built live meta sidecar to {liveMetaPath}");
            }
        }

        // SyncCloudToLocal deletes local MP save files when cloud has no copy (assumes stale local). If cloud
        // write failed or lagged after WriteBytesAsync, the next sync would remove current_run_mp.save — push
        // local to cloud explicitly (same OverwriteCloudWithLocal path as unload cleanup).
        await TryReconcileCloudForLiveMultiplayerPathsAsync(mpPath, liveMetaPath, op);

        LogFlow(op, $"removing loaded archive entry id={entry.Id} storageFile={entry.StorageFileName}");
        await DeleteAsync(entry);
        LogFlow(op, "finished OK (live slot populated; archive row removed).");
        return null;
    }

    /// <summary>
    /// Copies the live multiplayer run into the mod archive (same as auto-backup on Load), then clears the live slot via the game API.
    /// Does not remove existing archive rows — only <see cref="DeleteAsync"/> does that.
    /// </summary>
    public async Task<string?> UnloadLiveToArchiveAndClearAsync()
    {
        const string op = "Unload";
        await EnsureLayoutAsync();
        var sm = SaveManager.Instance;
        int profileId = sm.CurrentProfileId;
        string mpPath = NormalizedRunSavePath(profileId, RunSaveManager.multiplayerRunSaveFileName);
        string mpBackupPath = mpPath + ".backup";
        bool hasMain = _store.FileExists(mpPath);
        bool hasBk = _store.FileExists(mpBackupPath);
        bool hasFileOnStore = hasMain || hasBk;
        bool hasGameFlag = sm.HasMultiplayerRunSave;

        LogFlow(
            op,
            $"start profileId={profileId} livePath={mpPath} HasMultiplayerRunSave={hasGameFlag} storeHasMainFile={hasMain} storeHasBackupFile={hasBk}");

        if (!hasGameFlag && !hasFileOnStore)
        {
            LogFlow(op, "aborted: no multiplayer save on store and SaveManager reports none.");
            return "There is no multiplayer save for this profile to remove.";
        }

        if (!hasGameFlag && hasFileOnStore)
        {
            LogFlow(op, "note: SaveManager.HasMultiplayerRunSave is false but store still has a file — proceeding (possible desync).");
        }

        string? liveJson = _store.ReadFile(mpPath);
        string jsonSource = "main";
        if (string.IsNullOrWhiteSpace(liveJson) && _store.FileExists(mpBackupPath))
        {
            liveJson = _store.ReadFile(mpBackupPath);
            jsonSource = ".backup";
        }

        if (string.IsNullOrWhiteSpace(liveJson))
        {
            LogFlow(op, "live JSON empty from main and backup — will not append archive row before clear.");
        }
        else
        {
            LogFlow(op, $"read live run JSON from {jsonSource}: {liveJson.Length} chars (content not logged).");
        }

        string metaPath = NormalizedRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName);
        MultiplayerSaveMetaDocument? liveMeta = await TryReadMetaDocumentAsync(metaPath);
        LogFlow(op, $"live meta sidecar present (parsed)={liveMeta != null} path={metaPath}");

        if (!string.IsNullOrWhiteSpace(liveJson))
        {
            LogFlow(op, "appending live run to mod archive (Unload snapshot)…");
            string? backupErr = await AppendArchiveEntryAsync(liveJson, liveMeta);
            if (backupErr != null)
            {
                LogFlowErr(op, $"append to mod archive failed: {backupErr}");
                return backupErr;
            }

            LogFlow(op, "archive row appended OK.");
        }

        try
        {
            LogFlow(op, "calling SaveManager.DeleteCurrentMultiplayerRun()…");
            sm.DeleteCurrentMultiplayerRun();
            LogFlow(op, "DeleteCurrentMultiplayerRun returned (live current_run_mp.save[.backup] removed via game store).");
        }
        catch (Exception ex)
        {
            LogFlowErr(op, $"DeleteCurrentMultiplayerRun threw: {ex}");
            return ex.Message;
        }

        // DeleteCurrentMultiplayerRun uses Path.Combine (may differ from normalized keys). Never use
        // OverwriteCloudWithLocal here: if any stray local file exists under our normalized path, that API
        // re-uploads to Steam and restores the run. Only explicit DeleteFile + optional ForgetFile.
        TryDeleteAllLiveMultiplayerPathVariantsAfterUnload(profileId, op);
        TryForgetCloudLiveMultiplayerPathsAfterUnload(profileId, op);

        LogFlow(op, "finished OK (live slot cleared; archive contains snapshot if JSON was non-empty).");
        return null;
    }

    /// <summary>
    /// Deletes every known string key for live MP files (raw Path.Combine + slash-normalized). Game API may
    /// remove only one variant; strays would make HasMultiplayerRunSave true or confuse cloud sync.
    /// </summary>
    private void TryDeleteAllLiveMultiplayerPathVariantsAfterUnload(int profileId, string operation)
    {
        string rawMp = RunSaveManager.GetRunSavePath(profileId, RunSaveManager.multiplayerRunSaveFileName);
        string rawMeta = RunSaveManager.GetRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName);
        string nMp = NormalizeStorePath(rawMp);
        string nMeta = NormalizeStorePath(rawMeta);
        var paths = new[]
        {
            rawMp, rawMp + ".backup", rawMeta, rawMeta + ".backup",
            nMp, nMp + ".backup", nMeta, nMeta + ".backup",
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string p in paths)
        {
            if (!seen.Add(p))
            {
                continue;
            }

            TryDeleteFileForUnload(p, operation);
        }
    }

    private void TryDeleteFileForUnload(string path, string operation)
    {
        try
        {
            if (!_store.FileExists(path))
            {
                return;
            }

            LogFlow(operation, $"delete (unload purge): {path}");
            _store.DeleteFile(path);
        }
        catch (Exception ex)
        {
            LogFlowErr(operation, $"delete failed ({path}): {ex.Message}");
        }
    }

    /// <summary>Drop Steam cloud tracking for live MP paths so a stale remote cannot linger after deletes.</summary>
    private void TryForgetCloudLiveMultiplayerPathsAfterUnload(int profileId, string operation)
    {
        if (_store is not CloudSaveStore cloud)
        {
            return;
        }

        string rawMp = RunSaveManager.GetRunSavePath(profileId, RunSaveManager.multiplayerRunSaveFileName);
        string rawMeta = RunSaveManager.GetRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName);
        string nMp = NormalizeStorePath(rawMp);
        string nMeta = NormalizeStorePath(rawMeta);
        var paths = new[]
        {
            rawMp, rawMp + ".backup", rawMeta, rawMeta + ".backup",
            nMp, nMp + ".backup", nMeta, nMeta + ".backup",
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int ok = 0;
        foreach (string p in paths)
        {
            if (!seen.Add(p))
            {
                continue;
            }

            try
            {
                cloud.ForgetFile(p);
                ok++;
            }
            catch (Exception ex)
            {
                LogFlowErr(operation, $"ForgetFile failed ({p}): {ex.Message}");
            }
        }

        LogFlow(operation, $"ForgetFile (cloud): completed {ok}/{seen.Count} path variants (best-effort).");
    }

    /// <summary>Uploads only the live MP run file (+ .backup) to Steam so the next sync cannot delete them.</summary>
    private async Task TryReconcileCloudForMpSaveOnlyAsync(string mpPath, string operation)
    {
        if (_store is not CloudSaveStore cloud)
        {
            LogFlow(operation, $"cloud MP push skipped — ISaveStore is {_store.GetType().Name} (not CloudSaveStore).");
            return;
        }

        LogFlow(operation, $"cloud MP push (OverwriteCloudWithLocal): begin paths mp={mpPath}");
        try
        {
            await cloud.OverwriteCloudWithLocal(mpPath);
            await cloud.OverwriteCloudWithLocal(mpPath + ".backup");
            LogFlow(operation, "cloud MP push: OverwriteCloudWithLocal completed for .save and .backup.");
        }
        catch (Exception ex)
        {
            LogFlowErr(operation, $"cloud MP push failed: {ex.Message}");
        }
    }

    /// <summary>
    /// <b>Load only.</b> Uses <see cref="CloudSaveStore.OverwriteCloudWithLocal"/> to push local bytes to Steam.
    /// Do <b>not</b> call this after Unload: if any local file still exists under one path variant,
    /// <c>OverwriteCloudWithLocal</c> would re-upload and restore the run. Unload uses explicit deletes + ForgetFile instead.
    /// </summary>
    private async Task TryReconcileCloudForLiveMultiplayerPathsAsync(string mpPath, string metaPath, string operation)
    {
        if (_store is not CloudSaveStore cloud)
        {
            LogFlow(operation, $"cloud reconcile (4 paths) skipped — ISaveStore is {_store.GetType().Name}.");
            return;
        }

        LogFlow(
            operation,
            $"cloud reconcile (4 paths): begin — mp, mp.backup, meta, meta.backup (local drives upload or remote delete per OverwriteCloudWithLocal).");
        try
        {
            await cloud.OverwriteCloudWithLocal(mpPath);
            await cloud.OverwriteCloudWithLocal(mpPath + ".backup");
            await cloud.OverwriteCloudWithLocal(metaPath);
            await cloud.OverwriteCloudWithLocal(metaPath + ".backup");
            LogFlow(operation, "cloud reconcile (4 paths): completed.");
        }
        catch (Exception ex)
        {
            LogFlowErr(operation, $"cloud reconcile (4 paths) failed: {ex.Message}");
        }
    }

    /// <summary>Adds a new archive row with the given JSON (UTF-8 text).</summary>
    public async Task<string?> AppendArchiveEntryAsync(string json, MultiplayerSaveMetaDocument? previousMeta = null)
    {
        await EnsureLayoutAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            return "Nothing to archive.";
        }

        string id = Guid.NewGuid().ToString("N");
        string fileName = $"{id}.save";
        string storagePath = EntryStoragePath(fileName);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        string? wErr = await SaveStoreWriteHelper.WriteBytesAsync(_store, storagePath, bytes);
        if (wErr != null)
        {
            return $"Could not write archive file: {wErr}";
        }

        MultiplayerSaveMetaDocument metaDoc = MultiplayerSaveMetadataFactory.BuildFromRunJson(json, previousMeta);
        string metaPath = EntryStoragePath(MetaFileNameForSave(fileName));
        string? metaErr = await SaveStoreWriteHelper.WriteUtf8Async(
            _store,
            metaPath,
            JsonSerializer.Serialize(metaDoc, DontAbandonJson.Indented));
        if (metaErr != null)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Could not write archive meta sidecar: {metaErr}");
        }

        ArchiveIndexDocument index = await ReadIndexAsync();
        index.Entries.Add(new ArchiveIndexEntry
        {
            Id = id,
            StorageFileName = fileName,
            CreatedUtc = DateTime.UtcNow.ToString("O"),
        });
        return await WriteIndexAsync(index);
    }

    public async Task DeleteAsync(ArchiveIndexEntry entry)
    {
        await EnsureLayoutAsync();
        ArchiveIndexDocument index = await ReadIndexAsync();
        index.Entries.RemoveAll(x => x.Id == entry.Id);
        string? idxErr = await WriteIndexAsync(index);
        if (idxErr != null)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Delete: could not update index: {idxErr}");
            return;
        }
        try
        {
            string p = EntryStoragePath(entry.StorageFileName);
            if (_store.FileExists(p))
            {
                _store.DeleteFile(p);
            }

            string metaP = EntryStoragePath(MetaFileNameForSave(entry.StorageFileName));
            if (_store.FileExists(metaP))
            {
                _store.DeleteFile(metaP);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Delete file failed: {ex.Message}");
        }
    }

    private static string MetaFileNameForSave(string storageFileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(storageFileName);
        return $"{baseName}.meta.json";
    }

    private async Task EnsureLiveSlotMetadataAsync()
    {
        int profileId = SaveManager.Instance.CurrentProfileId;
        string mpPath = NormalizedRunSavePath(profileId, RunSaveManager.multiplayerRunSaveFileName);
        string metaPath = NormalizedRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName);
        if (!_store.FileExists(mpPath))
        {
            TryDeleteFile(metaPath);
            return;
        }

        string? json = _store.ReadFile(mpPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        MultiplayerSaveMetaDocument? prev = await TryReadMetaDocumentAsync(metaPath);
        MultiplayerSaveMetaDocument doc = MultiplayerSaveMetadataFactory.BuildFromRunJson(json, prev);
        string? err = await SaveStoreWriteHelper.WriteUtf8Async(
            _store,
            metaPath,
            JsonSerializer.Serialize(doc, DontAbandonJson.Indented));
        if (err != null)
        {
            GD.PrintErr($"[DontAbandonYourFriends] EnsureLiveSlotMetadataAsync: {err}");
        }
    }

    private async Task<LiveMultiplayerSlotInfo> BuildLiveSlotInfoAsync()
    {
        int profileId = SaveManager.Instance.CurrentProfileId;
        string mpPath = NormalizedRunSavePath(profileId, RunSaveManager.multiplayerRunSaveFileName);
        if (!_store.FileExists(mpPath))
        {
            return new LiveMultiplayerSlotInfo { HasSave = false, Preview = RunSavePreviewFactory.EmptyPreview() };
        }

        string? json = _store.ReadFile(mpPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new LiveMultiplayerSlotInfo { HasSave = false, Preview = RunSavePreviewFactory.EmptyPreview() };
        }

        string metaPath = NormalizedRunSavePath(profileId, MultiplayerSavePaths.LiveRunMetaFileName);
        MultiplayerSaveMetaDocument? meta = await TryReadMetaDocumentAsync(metaPath);
        RunSavePreview preview = RunSavePreviewFactory.Build(json, meta);
        return new LiveMultiplayerSlotInfo { HasSave = true, Preview = preview };
    }

    private async Task<MultiplayerSaveMetaDocument?> ReadOrMigrateArchiveMetaAsync(string storageFileName, string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        string metaPath = EntryStoragePath(MetaFileNameForSave(storageFileName));
        if (_store.FileExists(metaPath))
        {
            try
            {
                string? t = await _store.ReadFileAsync(metaPath);
                if (!string.IsNullOrWhiteSpace(t))
                {
                    MultiplayerSaveMetaDocument? existing = JsonSerializer.Deserialize<MultiplayerSaveMetaDocument>(t, DontAbandonJson.Indented);
                    if (existing != null)
                    {
                        return existing;
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DontAbandonYourFriends] Read archive meta: {ex.Message}");
            }
        }

        MultiplayerSaveMetaDocument built = MultiplayerSaveMetadataFactory.BuildFromRunJson(json, null);
        string? wErr = await SaveStoreWriteHelper.WriteUtf8Async(
            _store,
            metaPath,
            JsonSerializer.Serialize(built, DontAbandonJson.Indented));
        if (wErr != null)
        {
            GD.PrintErr($"[DontAbandonYourFriends] Migrate archive meta: {wErr}");
        }

        return built;
    }

    private async Task<MultiplayerSaveMetaDocument?> TryReadMetaDocumentAsync(string metaPath)
    {
        if (!_store.FileExists(metaPath))
        {
            return null;
        }

        try
        {
            string? t = await _store.ReadFileAsync(metaPath);
            if (string.IsNullOrWhiteSpace(t))
            {
                return null;
            }

            return JsonSerializer.Deserialize<MultiplayerSaveMetaDocument>(t, DontAbandonJson.Indented);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] TryReadMetaDocumentAsync: {ex.Message}");
            return null;
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (_store.FileExists(path))
            {
                _store.DeleteFile(path);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] TryDeleteFile ({path}): {ex.Message}");
        }
    }

    private async Task<ArchiveIndexDocument> ReadIndexAsync()
    {
        try
        {
            string? text = await _store.ReadFileAsync(IndexPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ArchiveIndexDocument();
            }

            ArchiveIndexDocument? doc = JsonSerializer.Deserialize<ArchiveIndexDocument>(text, DontAbandonJson.Indented);
            return doc ?? new ArchiveIndexDocument();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] ReadIndexAsync: {ex.Message}");
            return new ArchiveIndexDocument();
        }
    }

    private async Task<string?> WriteIndexAsync(ArchiveIndexDocument doc)
    {
        string json = JsonSerializer.Serialize(doc, DontAbandonJson.Indented);
        return await SaveStoreWriteHelper.WriteUtf8Async(_store, IndexPath, json);
    }
}
