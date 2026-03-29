# Don't Abandon Your Friends

**Author:** latentspacez  
**Mod version:** v0.1.0  
**Slay the Spire 2 Compatibility:** v0.99.1, v0.101.0   

Adds a **multiplayer run archive** under your profile save data: when you switch which backup is active, the mod can **preserve** prior co-op runs, let you **load** one back into the live multiplayer slot, **unload** the current run into the archive, or **delete** stored backups. Intended for players who share or rotate co-op saves without losing progress.

## Origin Story

> `Alice`: Hey, I am finally back from work, let's continue our run!  
> `Bob`: Oh, thats unfortunate... I started a new one.. Sorry  
> `Alice`: WHAT?  
> `Bob`: I didn't know if we would ever finish it.  
> `Alice`: It.. was.. yesterday..  
> `Bob`: :(  

---

## What it does (players)

- **Load** — Writes an archived run into the live **`current_run_mp.save`** slot. If the live file is a **different** run, it is **copied into the mod archive first**, then the chosen archive is installed and **removed from the archive list** (the live file is the single active copy; avoids duplicates).
- **List** — Shows archived runs with previews: act, floor, ascension, start / last played / run duration, character icons, HP, deck size, relic count (from save JSON).
- **Unload** — Copies the live multiplayer run into the mod archive (when it has data), then clears the live slot using the game APIs. Does **not** remove existing archive rows.
- **Delete** — Removes an archive entry and its files (with confirmation).

Writes use the game’s **`ISaveStore`** so behavior stays consistent with **Steam Cloud** where applicable.

---

## Installing

1. Download the latest release from [github](https://github.com/latentspacez/DontAbandonYourFriends/releases) or [nexusmods](https://www.nexusmods.com/slaythespire2/mods/312?tab=files).
2. Copy the mod folder (containing **`DontAbandonYourFriends.dll`**, **`DontAbandonYourFriends.json`**) into the game’s **`mods/DontAbandonYourFriends/`** directory under `SteamLibrary\steamapps\common\Slay the Spire 2` (create the mod directory if it does not exist yet).
3. Enable the mod in the in-game mod manager. The manifest **`description`** includes **author** and **mod version** for quick identification.
4. If this is your first mod, copy your ProfileN/save to \AppData\Roaming\SlayTheSpire2\steam

Pre-built releases ship the usual **`DontAbandonYourFriends.dll`**. If you compile yourself, see **[Building](#building)** for default and StS2-specific outputs.

---

## Building

### GameVersions layout (recommended)

Keep a **sibling** folder **`GameVersions`** (default: `../GameVersions` next to this repo) with one subfolder per supported game build, aligned with MCP **`decompiled_cache`** names (e.g. **`v0_99_1`**, **`v0_101_0`**). In each folder, copy **`sts2.dll`**, **`Steamworks.NET.dll`**, and **`release_info.json`** from that game install (add more DLLs later if needed).

| MSBuild property | Purpose |
|------------------|---------|
| **`GameVersionsRoot`** | Root path containing **`v0_…`** folders (override if your tree is not `../GameVersions`). |
| **`GameVersionFolder`** | **Usually omitted.** When unset, MSBuild reads **`$(Sts2Path)/release_info.json`**, maps **`version`** to a folder name (**`v0.101.0`** → **`v0_101_0`**), and uses it **only if** **`GameVersions/<that folder>/sts2.dll`** exists. That picks compile-time **`sts2.dll`**, **`#if VERSION_*`**, and **`publish/<folder>/`**. Set explicitly to compile against a **different** game API than your install (e.g. build for **`v0_99_1`** while Steam has **`v0_101_0`**). |
| **`DeployGameVersionFolder`** | **Usually omitted.** Defaults to **`GameVersionFolder`** when that is set. If **`GameVersionFolder`** is empty (legacy mode), deploy can still resolve from **`release_info.json`** via the PowerShell step in **`Directory.Build.targets`**. Override only for special deploy layouts. |

Set these in **`Directory.Build.props.user`** (gitignored) so paths stay local. Example:

```xml
<PropertyGroup>
  <GameVersionsRoot>E:\Projects\Modding\SlayTheSpire2\GameVersions</GameVersionsRoot>
  <!-- Optional: <GameVersionFolder>v0_99_1</GameVersionFolder> when you must compile against a mirror other than your installed game. -->
</PropertyGroup>
```

### Typical workflow: compile all supported versions, deploy only the installed one

1. **Compile every version** (no copy to the game — safe for CI / preflight):

```bash
dotnet build DontAbandonYourFriends.csproj -t:BuildAllGameVersions
```

(Target name ends with **`s`**: **`BuildAllGameVersions`**. A typo alias **`BuildAllGameVersion`** is also defined.)

Outputs: **`publish/v0_99_1/DontAbandonYourFriends.dll`** plus **`DontAbandonYourFriends_0_1_0_StS2_0_99_1.zip`** (Windows: mod **`$(Version)`** with `.` → `_`, StS2 segment from **`GameVersionFolder`** without the leading **`v`**), and **`publish/v0_101_0/…`**, etc.

2. **Deploy only the installed build** — **`DeployGameVersionFolder`** matches **`GameVersionFolder`** when both are left unset (after auto-resolution from **`release_info.json`** + **`GameVersions`** mirror). Copy the **already built** DLL into **`mods/`**. Deploy is **opt-in** so a normal **`dotnet build`** never runs it:

```bash
dotnet build DontAbandonYourFriends.csproj -t:DeployGameVersionToMods -p:RunDeployGameVersionToMods=true
```

Override the folder only if needed:

```bash
dotnet build DontAbandonYourFriends.csproj -t:DeployGameVersionToMods -p:RunDeployGameVersionToMods=true -p:DeployGameVersionFolder=v0_99_1
```

**Alternative — one-step build + deploy** (recompiles **`GameVersionFolder`** and copies to **`mods/`** when **`GameVersionFolder`** matches the **resolved** deploy folder — i.e. your compile target matches the **currently installed** game):

```bash
dotnet build DontAbandonYourFriends.csproj
```

With defaults, **`GameVersionFolder`** and deploy tracking follow the **installed** game as long as **`GameVersions/<folder>/sts2.dll`** exists for that version.

### Build commands (summary)

| Goal | Command |
|------|---------|
| Build **all** versions under **`GameVersionsRoot`**, no deploy | `dotnet build DontAbandonYourFriends.csproj -t:BuildAllGameVersions` |
| Copy **one** prebuilt version to **`mods/`** (defaults to the install in **`release_info.json`**) | `dotnet build DontAbandonYourFriends.csproj -t:DeployGameVersionToMods -p:RunDeployGameVersionToMods=true` |
| Single configuration build (+ optional deploy per **`.user`**) | `dotnet build DontAbandonYourFriends.csproj` |

---

## Code architecture (developers)

| Area | Role |
|------|------|
| **`MainFile.cs`** | `[ModInitializer]` entry: logs version, runs **`GameCompatibility.IsSupportedGameBuild`**, then adds **`DontAbandonYourFriendsMenuButton`** to the scene tree. Sets **`MainFile.IsModEnabled`** false and exits on unsupported **`sts2.dll`**. |
| **`UI/DontAbandonYourFriendsMenuButton.cs`** | Main-menu entry that opens the archive UI when appropriate. |
| **`UI/DontAbandonYourFriendsScreen.cs`** | Full-screen modal: list of live + archived runs, **Load** / **Unload** / **Delete**, previews, guards for visibility and duplicate open. |
| **`UI/MainMenuUiHelper.cs`** | Shared helpers for menu placement / theming where used. |
| **`UI/LabelThemeKeys.cs`** | Label theme `StringName`s: `#if VERSION_0_101_0` / default use literals; `#elif VERSION_0_99_1` uses `ThemeConstants.Label` (needs matching **`GameVersionFolder`** / `sts2.dll`). |
| **`Services/MultiplayerSaveArchiveService.cs`** | Archive index (`index.json`), read/write under **`{profile}/mods/dont_abandon_your_friends/archives/`**, load/unload/delete, live slot coordination. |
| **`Services/RunSavePreviewFactory.cs`** | Parses multiplayer save JSON via **`SaveManager.FromJson<SerializableRun>`** and **`RunState`** for UI previews. |
| **`Services/SaveStoreAccessor.cs`** | Reflection to obtain **`ISaveStore`** from **`SaveManager`**. |
| **`GameCompatibility.cs`** / **`GameCompatibilityConstants.cs`** | **`release_info.json` `version`** vs embedded **`Sts2SupportedVersions.json`** (not **`sts2.dll` FileVersion**). |

---

## License

See **`LICENSE.txt`** (if present) or the **`PackageLicenseExpression`** in the project file (MIT).
