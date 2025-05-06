RimFortress is a project inspired by the genre of settlement management games and is based on Space Station 14.

To prevent people forking RobustToolbox, a "content" pack is loaded by the client and server. This content pack contains everything needed to play the game on one specific server this is the content pack for RimFortress.

If you want to host or create content for SS14, go to the [Space Station 14](https://github.com/space-wizards/space-station-14) repository as it contains both RobustToolbox and the content pack for development of new content packs and is the base for your fork.

## Links

| [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) |

## Documentation

The engine documentation and space station 14, on which our project is based, can be found on the https://docs.spacestation14.io/.

## Contributing

We are happy to accept contributions from anybody. Get in Discord if you want to help. We've got a [list of issues](https://github.com/RimFortress/rim-fortress-14/issues) that need to be done and anybody can pick them up. Don't be afraid to ask for help either!

## Building

1. Clone this repo.
2. Run `RUN_THIS.py` to init submodules and download the engine.
3. Compile the solution.

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

#### **1. Source Code**  
All original code in this repository (unless explicitly stated otherwise) is licensed under **[AGPL-3.0](https://www.gnu.org/licenses/agpl-3.0.html)** or any later version.  

- Code created for **RimFortress** is located in `_RF/` directories or marked with special comments. [Example](https://github.com/RimFortress/rim-fortress-14/blob/master/Content.Server/NPC/Systems/NPCUtilitySystem.cs).  
- Some files may be available under **alternative licenses** (noted in file headers or `.license` files). This enables compatibility with non-AGPL projects. Full license texts are available in `LICENSES/`.  

#### **2. Dependencies & Third-Party Code**  
- This repository incorporates code from **[space-wizards/space-station-14](https://github.com/space-wizards/space-station-14)**, licensed under the **[MIT License](https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT)**.  
- If using derived code, ensure compliance with **all applicable licenses**.  

#### **3. Media Assets (Art, Sounds, Textures)**  
- Most media files are licensed under **[CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/)** unless otherwise specified. Authorship and licensing details are provided in metadata files.  
- **Important**: Some assets use **non-commercial licenses** (e.g., [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/)). These **cannot be used in commercial projects**—remove or replace such files before commercial distribution.  

#### **4. Commercial Use**  
- AGPL-3.0 **permits commercial use** but requires:  
  - Open-sourcing derivative works.  
  - Preserving license notices and copyright.  
- For commercialization, verify licenses of **all components** (especially NC-licensed media).
