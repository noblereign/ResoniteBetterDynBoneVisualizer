# BetterDynBoneVisualizer

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that adds a less flashy dynamic bone visualizer.

The modded visualizer acts more like the visualizer from Rig, FingerReferencePoseSource, etc... Instead of creating and destroying debug visuals every frame, it instead generates visuals once and reuses them every update for better visual stability.

## Usage
On the bottom of DynamicBoneChain components there will be a new section called "Better DynBone Debug Visuals".

Click "Generate debug visuals" to start the modded visualizer. When you're done you can hit "Clear debug visuals" to stop it.

It should act and look mostly like the vanilla visualizer, just without all the glitchy movements

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Place [BetterDynBoneVisualizer.dll](https://github.com/noblereign/ResoniteBetterDynBoneVisualizer/releases/latest/download/BetterDynBoneVisualizer.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create this folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Resonite logs.
