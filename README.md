# BulletBox Mod

Mod for Escape from Duckov that adds a Bullet Box with 12 slots, accepts only ammo, and reduces ammo weight while stored inside.

## Requirements
- Escape from Duckov installed (Steam).
- .NET SDK for build (dotnet CLI).

## Mod folder structure
Place the mod folder at:
`Duckov_Data/Mods/BulletBox`

Expected files:
- `BulletBox.dll`
- `info.ini`
- `preview.png` (recommended 256x256)
- `box.png` (item icon in-game)

## Local build
From the project directory:
```
dotnet build "MOD ESCAPE.sln" -c Release
```

The DLL output is:
`BulletBox/bin/Release/net472/BulletBox.dll`

## Local install (manual)
Copy the files to:
`D:\steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\BulletBox`

`info.ini` example:
```
name = BulletBox
displayName = Bullet Box
description = Adds a Bullet Box with 12 slots that stores only ammo. Ammo weight is reduced by 80% while inside.
tags = Utility,Gameplay
```

## Contributing
If you want to contribute:
1. Fork the repository.
2. Clone your fork:
   ```
   git clone https://github.com/Math-benites/BulletBox-duckov.git
   ```
3. Create a branch:
   ```
   git checkout -b feature/my-change
   ```
4. Make changes and commit:
   ```
   git add .
   git commit -m "feat: my change"
   ```
5. Push to your fork:
   ```
   git push origin feature/my-change
   ```
6. Open a Pull Request.

You can also open an Issue with suggestions or bug reports.
