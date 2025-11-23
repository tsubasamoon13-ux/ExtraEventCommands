# Extra Event Commands

A comprehensive suite of event commands for Stardew Valley modding that provides advanced control over farmer and NPC rendering during cutscenes and events.

## Features

### 🔄 Farmer Rotation

Rotate the farmer sprite to any angle during events, with full compatibility for Fashion Sense clothing and accessories.

**Commands:**

- `rotateFarmer [degrees]` - Rotate farmer by specified degrees (e.g., 180 for upside-down)
- `resetFarmerRotation` - Reset rotation to normal

**Console Commands (for testing):**

- `rotate_farmer [degrees]` - Test rotation in-game
- `reset_rotation` - Reset via console

### 👕 Clothing Control

Strip and restore farmer clothing during events with granular control over individual items. Supports both vanilla clothing and Fashion Sense accessories.

**Commands:**

- `stripClothing [items...]` - Remove all clothing, or specific items: `shirt`, `pants`, `shoes`, `hat`, `sleeves`, `accessory`, `accessory0`, `accessory1`, `accessory2`
- `restoreClothing [items...]` - Restore all clothing, or specific items
- `strip [items...]` - Short alias for stripClothing
- `wear [items...]` - Short alias for restoreClothing
- `stripAll` - Always strips everything (ignores arguments)
- `wearAll` - Always restores everything (ignores arguments)

**Examples:**
```
stripClothing shirt pants    # Remove only shirt and pants
strip hat shoes              # Remove hat and shoes
stripAll                     # Remove everything
wear shirt                   # Restore just the shirt
wearAll                      # Restore everything
```

Clothing automatically restores when events end. Requires [NoDress](https://www.nexusmods.com/stardewvalley/mods/16092) mod for invisible clothing items.

### 📊 Farmer Layer Control

Position the farmer in front of or behind specific NPCs during events, with full Fashion Sense compatibility.

**Commands:**

- `farmerAbove [npcName]` - Render farmer in front of specified NPC
- `farmerBelow [npcName]` - Render farmer behind specified NPC
- `resetLayers` - Clear all layer overrides

**Example:**
```
farmerAbove Shane            # Farmer appears in front of Shane
farmerBelow Emily            # Farmer appears behind Emily
```

### 🎭 Temporary Actor Positioning

Render temporary actors as UI overlays, guaranteed to appear on top of everything else. Needs to use addTemporaryActor and place actor off-screen first to prevent double drawing. Main usage is for temporaryActors with large frames to not worry about vegation or furniture rendering over the frame.

**Commands:**

- `tempActorAtTile [actorName] [tileX] [tileY]` - Position actor at map tile coordinates (recommended for world positioning)
- `tempActorAtScreen [actorName] [screenX] [screenY]` - Position actor at fixed screen pixel coordinates (for UI-style positioning)
- `resetTempActors` - Clear all actor overlays

**Examples:**
```
tempActorAtTile Jas 10 15         # Position Jas at tile (10, 15)
tempActorAtScreen Jas 640 360     # Position Jas at screen pixel (640, 360)
```

Actors automatically warp off-screen to prevent double-drawing. Overlays reset when events end.

## Installation

1. Install [SMAPI](https://smapi.io/)
2. Install NoDress by AlexGoD or similar mod (for truly naked farmer)
3. Download this mod and extract to `Stardew Valley/Mods`
4. Run the game through SMAPI

## Compatibility

- ✅ **Fashion Sense** - Full compatibility for all features with custom clothing/accessories
- ✅ **Content Patcher** - Works with CP event commands

## ⚠️ Notes

This mod was created with AI-assisted code.
I am not a professional C# developer, so bug fixes may take time.
Contributions and pull requests are welcome.











































