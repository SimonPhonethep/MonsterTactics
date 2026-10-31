# MonsterTactics

**A 2.5D strategy auto-battler I'm building in Unity 6 with C#.**

MonsterTactics is a game where you build a team, place units on a hex board, and watch them fight automatically. Winning isn't just about picking strong units. Positioning, team synergies, items, and how you spend your gold all make a difference.

I started the project as a browser prototype using React, TypeScript, and Phaser, then rebuilt it in Unity to develop the combat and game systems in C#. This repository contains the Unity version.

![Temporary character art used in this portfolio version](docs/placeholder_sprite_sheet.png)

*The image above shows placeholder art, not a screenshot of gameplay.*

## What I've built

- **Auto-battles on a hex grid:** Units find targets, move into range, attack, and use abilities without the player controlling every action.
- **Team building:** Units have different roles and traits. Combining the right units activates bonuses, and collecting copies lets you upgrade them.
- **Shop and economy:** Buy units, reroll the shop, level up, and manage gold between rounds. Shop odds change with player level.
- **Items and augments:** Equip items, combine components, and choose bonuses that change how a team plays.
- **Rounds and PvE:** Planning and combat phases, plus encounters against computer-controlled enemies.
- **Visuals and feedback:** Sprite animations, ability effects, and UI for the board, shop, and unit information.

## A part I'm especially proud of: the balance editor

I made a custom editor to make balancing easier. Instead of digging through combat code every time I want to adjust a unit, I can edit values like health, attack damage, armor, attack speed, and ability scaling through the editor.

The idea is to keep unit data separate from the C# systems that handle movement, targeting, and damage. It makes adding units and testing different balance ideas much easier.

## Built with

- **Unity 6** (`6000.6.6f1`)
- **C#** for gameplay systems and editor tools
- **React, TypeScript, and Phaser** for the original browser prototype (not included here)

## Open the project

1. Download or clone the repository.
2. In Unity Hub, choose **Add project from disk** and select the folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open the project with Unity **6000.6.6f1** if possible.
4. Open `Assets/Scenes/SampleScene.unity`.

**A note about this GitHub copy:** This is a portfolio version of my project, not a verified playable release. I replaced the original character artwork with temporary graphics and the imported 3D character models with static placeholders. Those replacements were made outside Unity, and I haven't tested this exported version in the editor afterward. Some animation, model, or scene references may need fixing before it runs correctly.

The image above is a preview of the temporary assets, not an in-game screenshot.

## More details

- [Portfolio conversion audit](docs/CONVERSION_AUDIT.md) — what was changed for this copy and what still needs checking
- [Asset policy](docs/ASSET_POLICY.md) — notes on the included artwork and other assets

## About this repository

I'm sharing the source to show how I've approached game logic, combat calculations, team-building mechanics, and editor tooling. It is still a work in progress.

No open-source license is included. Please don't assume the code or assets are licensed for reuse. Third-party dependencies remain subject to their own licenses.
