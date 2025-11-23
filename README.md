Extra Event Commands
A comprehensive suite of event commands for Stardew Valley modding that provides advanced control over farmer and NPC rendering during cutscenes and events.
---
Features
🔄 Farmer Rotation
Rotate the farmer sprite to any angle during events, with full compatibility for Fashion Sense clothing and accessories.
Commands:

rotateFarmer [degrees] - Rotate farmer by specified degrees (e.g., 180 for upside-down)
resetFarmerRotation - Reset rotation to normal

Console Commands (for testing):

rotate_farmer [degrees] - Test rotation in-game
reset_rotation - Reset via console

👕 Clothing Control
Strip and restore farmer clothing during events with granular control over individual items. Supports both vanilla clothing and Fashion Sense accessories.
Commands:

stripClothing [items...] - Remove all clothing, or specific items: shirt, pants, shoes, hat, sleeves, accessory, accessory0, accessory1, accessory2
restoreClothing [items...] - Restore all clothing, or specific items
strip [items...] - Short alias for stripClothing
wear [items...] - Short alias for restoreClothing
stripAll - Always strips everything (ignores arguments)
wearAll - Always restores everything (ignores arguments)

---

⚠️ Notes

This mod was created with AI-assisted code.
I am not a professional C# developer, so bug fixes may take time.
Contributions and pull requests are welcome.
