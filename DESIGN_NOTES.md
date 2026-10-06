## Dota Chaos prototype

This package is source-first. It is designed for:
BepInEx/plugins/DotaChaos/

The plugin uses reflection for PEAK's game-side Character API so the source is less
coupled to a specific PEAK patch. It does not modify Dota 2, its client, or anti-cheat.

Prototype abilities:
- Blink
- Haste
- DoubleJump
- Toss
- Vacuum
- Rupture
- LowGravity
- ArcaneSurge
- TinyThrow
- Chronosphere

Prototype creeps are simple Unity capsules created locally. They are deliberately NOT
Dota 2 assets and are not networked in this first prototype.

Known limitation:
The prototype is local/client-side. Multiplayer synchronization and proper PEAK
network authority should be added only after the local mechanics are verified in-game.
