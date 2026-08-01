# Contributing

Bug reports and focused pull requests are welcome.

## Development setup

1. Install Stationeers, BepInEx 5.4, and StationeersLaunchPad 0.5.
2. Clone this repository into a convenient development directory.
3. Build with `dotnet build --configuration Release`, overriding `StationeersPath` if the game is not installed at the project default.
4. Copy or link the LaunchPad payload into `Documents\My Games\Stationeers\mods` if the repository itself is elsewhere.

Stationeers and its managed assemblies are proprietary dependencies and must not be committed or included in pull requests.

## Before submitting a change

- Run a clean Release build with zero errors and warnings.
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ValidateRepository.ps1`.
- Test creative spawning, printing, placement, all three connectors, switching, deconstruction, and save/reload.
- Test Mode 0, Mode 1, and Mode 2 reads and writes through an IC Housing.
- For network changes, also test output-local devices, chains, duplicate paths, cycles, and unrelated IC networks.
- For multiplayer changes, test a dedicated server with a connected client using the same DLL.

Keep compatibility fixes scoped to bridge-connected networks. The cable-network Harmony hooks execute for every network in the world, so unrelated networks must retain vanilla behavior.

## Bug reports

Include:

- Stationeers build
- BepInEx and StationeersLaunchPad versions
- Mod version and whether every peer has the same DLL
- Single-player, listen-server, or dedicated-server environment
- Reproduction steps and relevant `Player.log` excerpt
