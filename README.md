# Logic Filter Bridge

Logic Filter Bridge is a [StationeersLaunchPad](https://stationeerslaunchpad.github.io/) mod that exposes a configurable view of one data network to another.

The mod clones the vanilla Logic Mirror at runtime, so it does not redistribute game assets or require a Unity project. Imported devices remain their original live instances: direct and batch reads, writes, reference-ID access, and slot operations work transparently across the bridge.

> [!NOTE]
> The current 3D model is a pure runtime copy of the vanilla Logic Mirror. Contributions providing a bespoke model and visual identity for the Logic Filter Bridge are very welcome.

## Requirements

- Stationeers
- BepInEx 5.4.23.3
- StationeersLaunchPad 0.5.0
- The same mod version installed on the server and every connected client

Version 0.1.0 targets the Stationeers build current on 1 August 2026. Stationeers updates can change internal APIs used by runtime prefab cloning and Harmony patches.

## Installation

1. Install BepInEx and StationeersLaunchPad.
2. Download the release archive and extract it into `Documents\My Games\Stationeers\mods`.
3. Confirm the resulting layout contains `LogicFilterBridge\About\About.xml` and `LogicFilterBridge\LogicFilterBridge.dll`.
4. Enable the mod through LaunchPad and start the game.

Dedicated servers and joining clients must all use the same release.

## Crafting and wiring

Print **Kit (Logic Filter Bridge)** in the Electronics Printer for:

- 10 seconds
- 1000 J
- 1 g copper
- 1 g gold

The cloned Logic Mirror layout has data input on the left, power on top, and data output on the right. A newly constructed bridge defaults to Mode 0. Supply 10 W and turn its physical switch on to begin forwarding; an IC Housing is only needed to configure a filter.

Forwarding fails closed unless the bridge is fully built, on, powered, and connected to distinct input and output data networks. Devices physically connected to the output remain visible in every state.

## IC10 configuration

Configure the bridge from an IC Housing on its input network. The bridge deliberately hides itself from its output network.

`Mode` selects the filter:

- `0` — all devices
- `1` — devices whose signed `PrefabHash` equals `Setting`
- `2` — devices whose display name contains the packed text in `Setting`, ignoring case

Selected-device examples, where the housing screws assign the bridge to `d0`:

```ic10
s d0 Mode 0

s d0 Mode 1
s d0 Setting HASH("StructureGasSensor")

s d0 Mode 2
s d0 Setting STR("Analyz")
```

Batch examples from the input network:

```ic10
sb HASH("StructureLogicFilterBridge") Mode 1
sb HASH("StructureLogicFilterBridge") Setting HASH("StructureGasSensor")

sbn HASH("StructureLogicFilterBridge") HASH("Intake Filter") Mode 2
sbn HASH("StructureLogicFilterBridge") HASH("Intake Filter") Setting STR("Analyz")
```

Native `STR` values contain one to six ASCII characters. Empty, fractional, non-ASCII, oversized, and otherwise invalid packed values match no names. `db` addresses the IC Housing running the program, not the bridge; use `d0`–`d5`, `sb`, or `sbn` to configure it.

Finite `Mode` writes are truncated and clamped to `0`–`2`. Non-finite writes are ignored. The server is authoritative for configuration writes.

## Network behavior

- Input-side devices are appended to the output network after applying the selected filter.
- Output-local devices are preserved.
- The bridge itself is visible only from its input side, except when input and output are accidentally the same network.
- Chained bridges and vanilla data links forward their complete visible lists.
- Duplicate paths are deduplicated by reference ID, and traversal guards terminate cycles.
- Off, unpowered, incomplete, disconnected, and same-network bridges import nothing.

Imported devices retain their native logic rules. For example, a passive, unlinked Logic Transmitter cannot service a `Mode` read; an `lbn` selecting it will produce vanilla `IncorrectLogicType` behavior.

## Building from source

The project targets `netstandard2.1` and references assemblies from a local Stationeers installation. No game or BepInEx binaries are committed.

The default game path is `D:\SteamLibrary\steamapps\common\Stationeers`:

```powershell
dotnet build --configuration Release
```

Override it when necessary:

```powershell
dotnet build --configuration Release -p:StationeersPath='E:\Games\Stationeers'
```

The build installs the only loadable copy of `LogicFilterBridge.dll` into the repository root. LaunchPad scans enabled mod directories recursively, so generated copies under `bin` and `obj` are deleted after compilation.

Create a GitHub-ready release archive with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package.ps1 -StationeersPath 'E:\Games\Stationeers'
```

The archive is written to `artifacts/LogicFilterBridge-0.1.0.zip` and contains the standard LaunchPad mod directory.

## Troubleshooting

- Search `Player.log` or a LaunchPad debug package for `[LogicFilterBridge]`.
- Confirm only one `LogicFilterBridge.dll` exists below the enabled mod directory.
- Confirm the server and all clients have matching DLLs.
- If an IC10 batch instruction reports `IncorrectLogicType`, inspect every matching device. Stationeers aborts a batch read when any matching device cannot expose the requested property.

Please include the Stationeers build, BepInEx version, LaunchPad version, listen/dedicated-server status, and relevant log excerpt in bug reports.

## Public identifiers

- Mod ID: `com.james.logicfilterbridge`
- Assembly: `LogicFilterBridge.dll`
- Kit prefab: `ItemKitLogicFilterBridge`
- Structure prefab: `StructureLogicFilterBridge`

## License

Logic Filter Bridge is available under the [MIT License](LICENSE).
