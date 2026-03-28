# Don't Abandon Your Friends

**Slay the Spire 2** mod · **Author:** latentspacez · **Mod version:** see `Directory.Build.props` (`<Version>`) and the `version` field in `DontAbandonYourFriends.json` (kept in sync on build).

Adds a **multiplayer run archive** under your profile save data: when you switch which backup is active, the mod can **preserve** prior co-op runs, let you **load** one back into the live multiplayer slot, **unload** the current run into the archive, or **delete** stored backups. Intended for players who share or rotate co-op saves without losing progress.

## Origin Story

`Alice`: Hey, I am finally back from work, let's continue our run!
`Bob`: Oh, thats unfortunate... I started a new one.. Sorry
`Alice`: WHAT?
`Bob`: I didn't know if we would ever finish it.
`Alice`: It.. was.. yesterday..
`Bob`: :(

---

## What it does (players)

- **Load** — Writes an archived run into the live **`current_run_mp.save`** slot. If the live file is a **different** run, it is **copied into the mod archive first**, then the chosen archive is installed and **removed from the archive list** (the live file is the single active copy; avoids duplicates).
- **List** — Shows archived runs with previews: act, floor, ascension, start / last played / run duration, character icons, HP, deck size, relic count (from save JSON).
- **Unload** — Copies the live multiplayer run into the mod archive (when it has data), then clears the live slot using the game APIs. Does **not** remove existing archive rows.
- **Delete** — Removes an archive entry and its files (with confirmation).

Writes use the game’s **`ISaveStore`** so behavior stays consistent with **Steam Cloud** where applicable.

---

## Installing

1. Copy the mod folder (containing **`DontAbandonYourFriends.dll`**, **`DontAbandonYourFriends.json`**, and optional **`.pck`**) into the game’s **`mods/DontAbandonYourFriends/`** directory (see Slay the Spire 2 modding docs for the exact layout on your platform).
2. Enable the mod in the in-game mod manager. The manifest **`description`** includes **author** and **mod version** for quick identification.

**Build from source:** `dotnet build DontAbandonYourFriends.csproj -c Release`  
Output is under **`publish/`**; the project can also copy artifacts into your local game `mods` folder if **`Sts2Path`** / Steam paths are configured (see `DontAbandonYourFriends.csproj`).

---

## Versioning, author, and in-game description

- **Single source of truth for mod version:** `Directory.Build.props` → `<Version>`.
- **Assembly / logging:** `ModVersionInfo.GetInformationalVersion()` (see `ModVersionInfo.cs`).
- **Manifest:** `DontAbandonYourFriends.json` — `version` is rewritten to **`v$(Version)`** on build; the **`description`** line that starts with **`Author: latentspacez · v…`** is updated to match **`$(Version)`** by the same MSBuild step so the mod browser text stays aligned.
- **NuGet / package metadata:** `DontAbandonYourFriends.csproj` — `Authors`, `PackageId`, and `Description` (includes version via `$(Version)`).

---

## Code architecture (developers)

| Area | Role |
|------|------|
| **`MainFile.cs`** | `[ModInitializer]` entry: logs version, runs **`GameCompatibility.IsSupportedGameBuild`**, then adds **`DontAbandonYourFriendsMenuButton`** to the scene tree. Sets **`MainFile.IsModEnabled`** false and exits on unsupported **`sts2.dll`**. |
| **`UI/DontAbandonYourFriendsMenuButton.cs`** | Main-menu entry that opens the archive UI when appropriate. |
| **`UI/DontAbandonYourFriendsScreen.cs`** | Full-screen modal: list of live + archived runs, **Load** / **Unload** / **Delete**, previews, guards for visibility and duplicate open. |
| **`UI/MainMenuUiHelper.cs`** | Shared helpers for menu placement / theming where used. |
| **`Services/MultiplayerSaveArchiveService.cs`** | Archive index (`index.json`), read/write under **`{profile}/mods/dont_abandon_your_friends/archives/`**, load/unload/delete, live slot coordination. |
| **`Services/RunSavePreviewFactory.cs`** | Parses multiplayer save JSON via **`SaveManager.FromJson<SerializableRun>`** and **`RunState`** for UI previews. |
| **`Services/SaveStoreAccessor.cs`** | Reflection to obtain **`ISaveStore`** from **`SaveManager`**. |
| **`GameCompatibility.cs`** / **`GameCompatibilityConstants.cs`** | **`sts2.dll`** file version gate. |

---

## License

See **`LICENSE.txt`** (if present) or the **`PackageLicenseExpression`** in the project file (MIT).
