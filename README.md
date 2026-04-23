# Against the Odds

## Overview
**Against the Odds** is a 2D Dark Fantasy turn-based boss rush game built with Unity. Players face off against powerful, corrupted bosses using a strategic Trading Card Game (TCG) combat system. The goal is to reduce the enemy's health to zero using predefined, themed decks (e.g., Fire, Water, Electricity) while managing a regenerating pool of Action Points (Mana).

## Team Members
*   Brinchat Hugo
*   Dill Antoine
*   Morisetti Alexandre

## Core Mechanics
*   **Turn-Based Combat:** Tactical back-and-forth battles inspired by classic RPGs and TCGs.
*   **Card System:** 
    *   *Offensive Cards:* Deal damage to the boss.
    *   *Defensive Cards:* Protect the player from incoming attacks.
    *   *Utility/Status/Weather Cards:* Alter the battlefield conditions.
*   **Action Points (Mana):** Resource management system where cards cost AP to play, which recharges at the start of each turn.
*   **Boss Rush Campaign:** A sequential gauntlet of challenging enemies, leading to the source of the world's corruption.

## Lore
Since the fall of the Rhodesia Kingdom, the world has been reduced to ruins, ashes, and corrupted flesh. An ancient curse has awakened fallen sovereigns and knights, transforming them into monstrous rulers of cursed lands. The player embodies a nameless warrior, returned from the dead, to face these powerful enemies. In this hopeless universe, only one question remains: live or die.

## Technical Stack
*   **Engine:** Unity 2D
*   **Version Control:** Git / GitHub
*   **Assets Generation:** AI tools for concept art, UI elements, music, and sound effects.
*   **Card Prototyping:** [Card Conjurer](https://cardconjurer.com/creator)

## Git Workflow (Internal Team Rules)
1.  **Never work on the same Unity Scene simultaneously.**
2.  Develop features (cards, enemies) in isolated test scenes.
3.  Turn your elements into **Prefabs** before pushing them to the repository.
4.  The main branch is locked; all new features must be developed on branches branching off from `dev`.