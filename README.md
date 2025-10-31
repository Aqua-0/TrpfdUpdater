# TrpfdUpdater
Updates Trpfd so you don't need to rely on TrinityModLoader for everything.


# UpdateTrpfd – Quick Start

<img width="926" height="600" alt="image" src="https://github.com/user-attachments/assets/9787f770-006c-4948-966a-b64d51d307f0" />

## TL;DR
1) Make a **mod pack** folder.
2) Put the game’s `data.trpfd` inside `arc/` (or `romfs/arc/` if your loader expects that).
3) Put your loose files under `romfs/...` using the *exact in-game paths*.
4) Run **UpdateTrpfd.exe** → set **Game Root** and **Romfs Root** → **Update TRPFD**.
5) (Optional) enable **Watch** to auto-update while you add/remove files.

---

## Folder layout
- ModPackRoot/romfs/arc/data.trpfd **the descriptor this tool edits (copy from the game)**
- ModPackRoot/romfs/ik_chara/model/... **your loose files, real in-game relative paths**

---

## Running
- Download the release ZIP and extract it anywhere.
- Run `UpdateTrpfd.exe`.
- Set:
  - **Game Root** → `ModPackRoot` (folder containing `romfs/arc/data.trpfd`)
  - **Romfs Root** → `ModPackRoot/romfs`
- Click **Update TRPFD**.
- Optional:
  - **Watch** → live updates as you add/remove loose files.
  - **Dry-run** → show what would change without saving.
  - **Auto-backup before save** → creates `data.trpfd.bak-YYYYMMDD-HHmmss` before each write.

> Framework-dependent builds require the **.NET 8 Desktop Runtime**. If you get a “missing runtime” dialog, install it from Microsoft.

## What it does
- For every loose file under `romfs/...`, it computes the hash of the romfs-relative path and **removes** that hash from `data.trpfd`, so the game loads your loose file.
- If you delete a loose file later, it **restores** the original mapping (fall back to the archive).
- Files not present in `data.trpfd` are **ignored** (prevents arbitrary/unknown types from loading).

---

## Buttons (one-liners)
- **Update TRPFD** – one-time sync (remove mappings for present loose files; restore missing ones).
- **Watch** (checkbox) – keep `data.trpfd` in sync as the folder changes.
- **Dry-run** – simulate changes; no file writes.
- **Auto-backup before save** – write a timestamped backup before modifying `data.trpfd`.
- **Export TRPFD to CSV** – dump all hashes + pack indices to `data.trpfd-entries-<timestamp>.csv`.
- **List recognized loose files** – log up to 500 romfs paths and whether they override, still mapped, or are unknown.
- **Status** lines – show the resolved `TRPFD` path, your `ROMFS` path, and the last backup path.

---

## Tips
- Paths must match game internals **exactly** (slashes and case matter per game rules).
- Keep **Game Root** and **Romfs Root** different: `Game Root = ModPackRoot`, `Romfs Root = ModPackRoot/romfs`.

---

## Troubleshooting
- **“value cannot be an empty string (path)”** – one of the roots is blank/invalid; set both roots, or run **Export TRPFD to CSV** after pressing **Test configuration** (if present) to verify detection.

## Credits
- Trinity Mod Loader / GFTool — research inspiration on TR archives.
https://github.com/pkZukan/gftool
