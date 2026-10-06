# Dota Chaos

A PEAK BepInEx prototype that injects Dota-inspired random chaos into normal PEAK gameplay.

**Status:** prototype / local mechanics test. Multiplayer synchronization is not implemented yet.

## Features

Randomly triggers:
- Blink
- Haste
- Double Jump
- Toss
- Vacuum
- Rupture
- Low Gravity
- Arcane Surge
- Tiny Throw
- Chronosphere

The prototype also spawns simple local prototype creeps.

This is a PEAK mod. It does not modify Dota 2 or bypass anti-cheat.

## Build

Requirements: Windows, PEAK, BepInEx, and a .NET SDK.

From the repository root:

```powershell
.\build.ps1 -PeakDir "C:\Path\To\PEAK"
```

The script reads references from the installed PEAK directory and installs the
result to:

```text
PEAK/BepInEx/plugins/DotaChaos/DotaChaos.dll
```

Expected PEAK layout:

```text
PEAK/
├── PEAK.exe
├── PEAK_Data/
│   └── Managed/
│       ├── UnityEngine.CoreModule.dll
│       └── UnityEngine.PhysicsModule.dll
└── BepInEx/
    └── core/
        └── BepInEx.dll
```

## Configuration

BepInEx creates `BepInEx/config/peak.dota.chaos.cfg`.

```ini
[General]
Enabled = true

[Chaos]
EventIntervalSeconds = 18
MaxLocalCreeps = 8
AbilityDurationSeconds = 8
SpawnPrototypeCreeps = true

[Debug]
Verbose = false
```

## Repository structure

```text
qcold/
├── .github/workflows/build.yml
├── DotaChaos.csproj
├── DotaChaosPlugin.cs
├── DotaChaos.cfg.example
├── DESIGN_NOTES.md
├── LICENSE
├── README.md
└── build.ps1
```
