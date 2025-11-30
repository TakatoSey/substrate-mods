##How to Build the Mod

Follow these steps to compile the mod from source.

###Prerequisites
- [.NET SDK 6.0 or newer](https://dotnet.microsoft.com/download) (required for building)
- A copy of **Substrate: Emergence** (Steam or itch.io)

### Build Instructions

1. **Place your mod folder inside the game directory**  
   Copy this folder (e.g., `CellHUDMod`) into your game’s root folder
2. **Open a terminal in the mod folder**  
3. **Build the mod**
   dotnet build -c Release
4. **Locate the compiled mod**  
   The .dll file will be generated at:
   CellHUDMod/bin/Release/netstandard2.1/CellHUDMod.dll
5. **Install the mod**
   Copy CellHUDMod.dll into your BepInEx plugins folder:
   Substrate Emergence/BepInEx/plugins/CellHUDMod/CellHUDMod.dll
