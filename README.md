# TRPFD Manager

Build a single `romfs/arc/data.trpfd` that points to loose files from one or many mods. No manual merging. Ordered packs decide winners on conflicts.

---

## Features

- Scan a **mods root** containing zip archives or raw folders.
- Normalize layouts that omit `romfs/` by detecting known romfs roots.
- Copy selected mods into a **target romfs**.
- Write `romfs/arc/data.trpfd` that references the copied files.
- Per-pack **Summary**, **Conflicts**, and **List loose** views.
- Optional overlay workflow for existing `user/load/<gameid>` pack sets.
- Dark mode, resizable and persistent log, saved paths and last tab.

---

## Requirements

- Windows
- .NET 8 Desktop Runtime
- A valid base `data.trpfd` taken from the extracted game or any mod

---

## Quickstart

> Goal: assemble a new merged mod in an emulator-style folder and create a `data.trpfd` for it.

### 0) Prepare a base TRPFD
Select a known-good `data.trpfd`. Any premade TRPFD works. Used only as a seed for format and base entries.

![Base TRPFD browse](https://github.com/user-attachments/assets/a8836cc0-034d-4baf-9df9-1b9a2da603c4)

### 1) Choose **Mods Root**
Point to a folder containing your mods. Mix zips and folders freely.

![Choose Mods Root](https://github.com/user-attachments/assets/5e9c23e3-9815-4a33-9525-f6ce03e646e2)

Example folder in Explorer:

![Mods root in Explorer](https://github.com/user-attachments/assets/baa8765d-ebe5-45f3-98c5-b0ba630d1594)

Click **Scan Mods** to index and normalize layout:

![Scan Mods](https://github.com/user-attachments/assets/bfb41b8e-9a5f-400f-b4ea-17a0149f2af9)

### 2) Choose **Target romfs**
Create a new mod folder under your emulator’s mod path and add a `romfs` inside it. Select that `romfs`.

Example:
<EmulatorModPath>\0100F43008C44000\NewMod\romfs


![Target romfs choose](https://github.com/user-attachments/assets/d63e22b3-0ce7-40c7-bf36-4067857e5f4e)

### 3) Select packs and **Copy + Build TRPFD**
- Tick mods to include. Use Up/Down to order. Later packs win on the same path.
- Click **Copy + Build TRPFD**. The tool:
  - Copies recognized files from each selected mod into the **target `romfs`**.
  - Skips any TRPFD files bundled in mods to avoid conflicts.
  - Writes `romfs\arc\data.trpfd` for the merged set.

![Copy + Build TRPFD](https://github.com/user-attachments/assets/69ab78df-2d61-46d6-abb6-eb0e3c2ebb71)

### 4) Launch the game
Enable the new mod in your emulator and start the game.

---

## How it decides what to copy

Only files under `romfs/...` are included. If a pack lacks `romfs/`, the scanner searches for known romfs roots (e.g., `ik_chara`, `ui`, `system`, `script`, etc) at any depth and treats them as `romfs/<root>/...`.

Bundled `data.trpfd` inside mods is ignored for merge operations.

Order defines the winner on collisions. Later pack overrides earlier for the same relative path.

---

## Useful actions

- **Summary**: Table per pack with columns Files, Wins, Loses and a file list on the right.
- **Conflicts**: Paths provided by multiple selected packs, shown in winner order.
- **List loose (selected)**: Logs recognized files per pack, paths trimmed to start at `romfs/...`.
- **Copy Selected → Target**: Copy only. No TRPFD build.
- **Copy + Build TRPFD**: Copy and write `romfs\arc\data.trpfd`.
- **Enable All / Disable All**: Toggle selection.
- **Dark mode / Show Log**: UI preferences. The log pane is resizable and the height persists.

---

## Overlay tab (advanced)

Works directly with `user/load/<gameid>/packX/romfs/...` trees.

- **Base TRPFD**: seed file descriptor.
- **Packs Root**: the `user/load/<gameid>` directory.
- **Output Root**: destination for `currenttrpfd/romfs/arc/data.trpfd`.
- **Fix Layout (create romfs)**: wraps packs that omitted `romfs/` by moving detected roots under `romfs/`.
- **Neutralize/Restore TRPFDs**: temporarily disable pack-bundled TRPFDs so the merged one is the only descriptor applied.
- **Apply Overlay**: writes a unified TRPFD for enabled packs in order.

Overlay base selection screenshot:

![Overlay base trpfd](https://github.com/user-attachments/assets/a8836cc0-034d-4baf-9df9-1b9a2da603c4)

---

## Directory examples

Target after build:
<EmulatorModPath>\0100F43008C44000\NewMod\romfs
arc
data.trpfd ← built by the tool
ik_chara...
ui...
system...


Zip or folder input accepted:
ModsRoot
MyPackA.zip ← contains romfs/... or roots/... detected and normalized
MyPackB\ ← raw folder
romfs
ui...


---

## Persistence

- Remembers theme, last tab, log height, and last used paths (base TRPFD, mods root, target romfs, overlay paths).
- Log height is adjustable via the splitter and saved on exit.

---

## Build from source



- dotnet restore
- dotnet build -c Release

---

## Troubleshooting

- **No files detected**  
  Click **Scan Mods**. Ensure the zip or folder contains `romfs/...` or one of the known roots at any depth.

- **Game crashes on boot**  
  Confirm `romfs\arc\data.trpfd` exists under the target mod and that the emulator loads that mod.

- **Unexpected overrides**  
  Use **Conflicts** or **Summary** to inspect. Move packs with Up/Down to change precedence.

- **Pack with duplicate nested folder names**  
  The scanner handles nested occurrences. If a pack still shows zero files, open **List loose** to see what was recognized.

---

## License

MIT. See `LICENSE`.

## Credits
- Trinity Mod Loader / GFTool — research inspiration on TR archives.
https://github.com/pkZukan/gftool
- Vibe Coding Assistance from ChatGPT.
- This manager is a fresh implementation with acknowledgements in `THIRD-PARTY-NOTICES`.
