<div class="header" align="center">
<img alt="RimFortress" width="880" height="451" src="https://github.com/user-attachments/assets/1a98bb7e-b07b-4182-a921-a75ce0d8b866">
</div>


RimFortress is a project inspired by the genre of settlement management games and is based on Space Station 14.

To prevent people forking RobustToolbox, a "content" pack is loaded by the client and server. This content pack contains everything needed to play the game on one specific server this is the content pack for RimFortress.

If you want to host or create content for SS14, go to the [Space Station 14](https://github.com/space-wizards/space-station-14) repository as it contains both RobustToolbox and the content pack for development of new content packs and is the base for your fork.

## Links

<div class="header" align="center">

| [Discord](https://discord.gg/rmK3AcNdz3) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) |

</div>

## Documentation

The engine documentation and space station 14, on which our project is based, can be found on the https://docs.spacestation14.io/.

## Contributing

We are happy to accept contributions from anybody. Get in Discord if you want to help. We've got a [list of issues](https://github.com/RimFortress/rim-fortress-14/issues) that need to be done and anybody can pick them up. Don't be afraid to ask for help either!

## Building

1. Clone this repo:
```shell
git clone https://github.com/RimFortress/rim-fortress-14.git
```
2. Go to the project folder and run `RUN_THIS.py` to initialize the submodules and load the engine:
```shell
cd space-station-14
python RUN_THIS.py
```
3. Compile the solution:

Build the server using `dotnet build`.

[More detailed instructions on building the project.](https://docs.spacestation14.com/en/general-development/setup.html)

## License

#### **1. Source Code**

This repository contains components under different licenses:

* **RimFortress Components** (all code in `_RF/` directories and code marked with `RimFortress` comments) are licensed under
  **MIT License + [Commons Clause](https://commonsclause.com/)**.
  This means the code is **source-available** but **not open source** under OSI definition.
  You may freely use, modify, and share it, but you **cannot sell** products or services whose value derives substantially from the RimFortress code.
  See the full terms in [`LICENSE.TXT`](./LICENSE.TXT) and [`LICENSE_RIMFORTRESS.TXT`](./LICENSES/LICENSE_RIMFORTRESS.TXT).

* **SS14 Components** (all other code and assets not marked as RimFortress) come from
  [space-wizards/space-station-14](https://github.com/space-wizards/space-station-14) and are licensed under the **[MIT License](./LICENSES/LICENSE_SS14.TXT)**.

* Other files may carry **alternative licenses**, noted in file headers or `.license` files.

#### **2. Dependencies & Third-Party Code**

* This project incorporates code and libraries from **Space Station 14** and its ecosystem.
* When using or redistributing derived code, you must comply with **all applicable licenses** (MIT and MIT+Commons Clause).

#### **3. Media Assets (Art, Sounds, Textures)**

* Most media files are licensed under **[CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/)** unless otherwise specified. Authorship and licensing details are provided in metadata files.
