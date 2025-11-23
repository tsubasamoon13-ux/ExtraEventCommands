using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace ExtraEventCommands
{
    /// <summary>
    /// Provides event commands to strip and restore farmer clothing during events.
    /// Supports both vanilla clothing items and Fashion Sense mod accessories.
    /// 
    /// Commands:
    /// - stripClothing [items...]: Strips all clothing (no args) or specific items (shirt, pants, shoes, hat, sleeves, accessory, accessory0-2)

    /// - strip [items...]: Alias for stripClothing with argument support
    /// - wear [items...]: Alias for restoreClothing with argument support
    /// - stripAll: Always strips all clothing (ignores arguments)
    /// - wearAll: Always restores all clothing (ignores arguments)
    /// 
    /// How it works:
    /// 1. Stores stripped clothing data (both vanilla and Fashion Sense) in a dictionary keyed by farmer ID
    /// 2. Uses NoDress mod invisible clothing items for stripped vanilla items
    /// 3. Removes Fashion Sense mod data keys to hide custom accessories
    /// 4. Automatically restores all clothing when events end
    /// 5. Supports Fashion Sense accessories in 3 slots (accessory0, accessory1, accessory2)
    /// </summary>
    public class ClothingToggle
    {
        private static IModHelper Helper;
        private static IMonitor Monitor;
        private static IManifest ModManifest;
        private static IFashionSenseApi FashionSenseAPI;

        private static Dictionary<long, ClothingData> StrippedClothing = new Dictionary<long, ClothingData>();

        public static void Initialize(IModHelper helper, IMonitor monitor, IManifest manifest)
        {
            Helper = helper;
            Monitor = monitor;
            ModManifest = manifest;

            // Register event commands with both long and short names
            // stripClothing/restoreClothing accept arguments for individual items, or no args for everything
            StardewValley.Event.RegisterCommand("stripClothing", StripClothingCommand);
            StardewValley.Event.RegisterCommand("restoreClothing", RestoreClothingCommand);

            // Shorter aliases - strip/wear accept arguments, stripAll/wearAll always do everything
            StardewValley.Event.RegisterCommand("strip", StripClothingCommand);
            StardewValley.Event.RegisterCommand("wear", RestoreClothingCommand);
            StardewValley.Event.RegisterCommand("stripAll", StripClothingCommand);
            StardewValley.Event.RegisterCommand("wearAll", RestoreClothingCommand);

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private static void OnGameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            FashionSenseAPI = Helper.ModRegistry.GetApi<IFashionSenseApi>("PeacefulEnd.FashionSense");
        }

        private static void OnUpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            // Auto-restore clothing when event ends
            if (Game1.CurrentEvent == null && StrippedClothing.Count > 0)
            {
                foreach (var kvp in StrippedClothing)
                {
                    RestoreAllClothing(Game1.player, kvp.Value);
                }
                StrippedClothing.Clear();
                Monitor.Log("Auto-restored clothing after event ended", LogLevel.Debug);
            }
        }

        private static void StripClothingCommand(Event @event, string[] args, EventContext context)
        {
            Farmer farmer = Game1.player;
            long farmerID = farmer.UniqueMultiplayerID;

            List<string> itemsToStrip = new List<string>();
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    itemsToStrip.Add(args[i].ToLower());
                }
            }
            else
            {
                // Strip EVERYTHING when no args given
                itemsToStrip.AddRange(new[] { "shirt", "pants", "shoes", "hat", "sleeves", "accessory" });
            }

            if (!StrippedClothing.ContainsKey(farmerID))
            {
                StrippedClothing[farmerID] = new ClothingData();
            }
            var clothingData = StrippedClothing[farmerID];

            foreach (string item in itemsToStrip)
            {
                switch (item)
                {
                    case "shirt":
                        StripShirt(farmer, clothingData);
                        break;
                    case "pants":
                        StripPants(farmer, clothingData);
                        break;
                    case "shoes":
                        StripShoes(farmer, clothingData);
                        break;
                    case "hat":
                        StripHat(farmer, clothingData);
                        break;
                    case "sleeves":
                        StripSleeves(farmer, clothingData);
                        break;
                    case "accessory":
                        StripAccessory(farmer, clothingData);
                        break;
                    case "accessory0":
                        StripAccessorySlot(farmer, clothingData, 0);
                        break;
                    case "accessory1":
                        StripAccessorySlot(farmer, clothingData, 1);
                        break;
                    case "accessory2":
                        StripAccessorySlot(farmer, clothingData, 2);
                        break;
                }
            }
            @event.CurrentCommand++;
        }

        private static void RestoreClothingCommand(Event @event, string[] args, EventContext context)
        {
            Farmer farmer = Game1.player;
            long farmerID = farmer.UniqueMultiplayerID;

            if (!StrippedClothing.TryGetValue(farmerID, out var clothingData))
            {
                Monitor.Log("No stored clothing to restore", LogLevel.Warn);
                @event.CurrentCommand++;
                return;
            }

            List<string> itemsToRestore = new List<string>();
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    itemsToRestore.Add(args[i].ToLower());
                }
            }
            else
            {
                itemsToRestore.AddRange(new[] { "shirt", "pants", "shoes", "hat", "sleeves", "accessory" });
            }

            foreach (string item in itemsToRestore)
            {
                switch (item)
                {
                    case "shirt":
                        RestoreShirt(farmer, clothingData);
                        break;
                    case "pants":
                        RestorePants(farmer, clothingData);
                        break;
                    case "shoes":
                        RestoreShoes(farmer, clothingData);
                        break;
                    case "hat":
                        RestoreHat(farmer, clothingData);
                        break;
                    case "sleeves":
                        RestoreSleeves(farmer, clothingData);
                        break;
                    case "accessory":
                        RestoreAccessory(farmer, clothingData);
                        break;
                    case "accessory0":
                        RestoreAccessorySlot(farmer, clothingData, 0);
                        break;
                    case "accessory1":
                        RestoreAccessorySlot(farmer, clothingData, 1);
                        break;
                    case "accessory2":
                        RestoreAccessorySlot(farmer, clothingData, 2);
                        break;
                }
            }

            farmer.UpdateClothing();

            if (args.Length == 1)
            {
                StrippedClothing.Remove(farmerID);
            }
            @event.CurrentCommand++;
        }

        private static void RestoreShirt(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - restore using correct mod data key
            if (!string.IsNullOrEmpty(data.FashionSenseShirt))
            {
                farmer.modData["FashionSense.CustomShirt.Id"] = data.FashionSenseShirt;
            }

            // Vanilla
            if (data.VanillaShirt != null)
            {
                farmer.shirtItem.Value = data.VanillaShirt;
                farmer.shirt.Set(data.VanillaShirtIndex);
                farmer.changeShirt(data.VanillaShirtIndex);
            }
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestorePants(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - restore using correct mod data key
            if (!string.IsNullOrEmpty(data.FashionSensePants))
            {
                farmer.modData["FashionSense.CustomPants.Id"] = data.FashionSensePants;
            }

            // Vanilla
            if (data.VanillaPants != null)
            {
                farmer.pantsItem.Value = data.VanillaPants;
                farmer.pants.Set(data.VanillaPantsIndex);
            }
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestoreShoes(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - restore using correct mod data key
            if (!string.IsNullOrEmpty(data.FashionSenseShoes))
            {
                farmer.modData["FashionSense.CustomShoes.Id"] = data.FashionSenseShoes;
            }

            // Vanilla
            if (data.VanillaShoes != null)
            {
                farmer.boots.Value = data.VanillaShoes;
            }
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestoreHat(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - restore using correct mod data key
            if (!string.IsNullOrEmpty(data.FashionSenseHat))
            {
                farmer.modData["FashionSense.CustomHat.Id"] = data.FashionSenseHat;
            }

            // Vanilla
            if (data.VanillaHat != null)
            {
                farmer.hat.Value = data.VanillaHat;
            }
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestoreSleeves(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - restore using correct mod data key
            if (!string.IsNullOrEmpty(data.FashionSenseSleeves))
            {
                farmer.modData["FashionSense.CustomSleeves.Id"] = data.FashionSenseSleeves;
            }
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestoreAccessory(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - restore all three accessory slots using CORRECT numbered keys
            if (!string.IsNullOrEmpty(data.FashionSenseAccessory))
            {
                farmer.modData["FashionSense.CustomAccessory.0.Id"] = data.FashionSenseAccessory;
            }

            if (!string.IsNullOrEmpty(data.FashionSenseAccessorySecondary))
            {
                farmer.modData["FashionSense.CustomAccessory.1.Id"] = data.FashionSenseAccessorySecondary;
            }

            if (!string.IsNullOrEmpty(data.FashionSenseAccessoryTertiary))
            {
                farmer.modData["FashionSense.CustomAccessory.2.Id"] = data.FashionSenseAccessoryTertiary;
            }

            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestoreAccessorySlot(Farmer farmer, ClothingData data, int slot)
        {
            string value = slot switch
            {
                0 => data.FashionSenseAccessory,
                1 => data.FashionSenseAccessorySecondary,
                2 => data.FashionSenseAccessoryTertiary,
                _ => null
            };

            if (!string.IsNullOrEmpty(value))
            {
                string key = $"FashionSense.CustomAccessory.{slot}.Id";
                farmer.modData[key] = value;
            }

            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void RestoreAllClothing(Farmer farmer, ClothingData data)
        {
            RestoreShirt(farmer, data);
            RestorePants(farmer, data);
            RestoreShoes(farmer, data);
            RestoreHat(farmer, data);
            RestoreSleeves(farmer, data);
            RestoreAccessory(farmer, data);

            farmer.UpdateClothing();
        }

        private static void StripShirt(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - use correct mod data key
            const string modDataKey = "FashionSense.CustomShirt.Id";
            if (farmer.modData.ContainsKey(modDataKey))
            {
                data.FashionSenseShirt = farmer.modData[modDataKey];
                farmer.modData.Remove(modDataKey);
            }

            // Store vanilla shirt properly
            data.VanillaShirt = farmer.shirtItem.Value;
            data.VanillaShirtIndex = farmer.shirt.Value;

            // Use NoDress invisible shirt
            string noDressShirtId = "AlexGoD.NoDress_Shirts";
            farmer.changeShirt(noDressShirtId);
            farmer.shirtItem.Value = new Clothing(noDressShirtId);
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void StripPants(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - use correct mod data key
            const string modDataKey = "FashionSense.CustomPants.Id";
            if (farmer.modData.ContainsKey(modDataKey))
            {
                data.FashionSensePants = farmer.modData[modDataKey];
                farmer.modData.Remove(modDataKey);
            }

            // Store vanilla pants properly
            data.VanillaPants = farmer.pantsItem.Value;
            data.VanillaPantsIndex = farmer.pants.Value;

            // Use NoDress invisible pants
            string noDressPantsId = "AlexGoD.NoDress_Pants";
            farmer.pants.Set(noDressPantsId);
            farmer.pantsItem.Value = new Clothing(noDressPantsId);
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void StripShoes(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - use correct mod data key
            const string modDataKey = "FashionSense.CustomShoes.Id";
            if (farmer.modData.ContainsKey(modDataKey))
            {
                data.FashionSenseShoes = farmer.modData[modDataKey];
                farmer.modData.Remove(modDataKey);
            }

            data.VanillaShoes = farmer.boots.Value;
            farmer.boots.Value = null;
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void StripHat(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - use correct mod data key
            const string modDataKey = "FashionSense.CustomHat.Id";
            if (farmer.modData.ContainsKey(modDataKey))
            {
                data.FashionSenseHat = farmer.modData[modDataKey];
                farmer.modData.Remove(modDataKey);
            }

            data.VanillaHat = farmer.hat.Value;
            farmer.hat.Value = null;
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void StripSleeves(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - use correct mod data key
            const string modDataKey = "FashionSense.CustomSleeves.Id";
            if (farmer.modData.ContainsKey(modDataKey))
            {
                data.FashionSenseSleeves = farmer.modData[modDataKey];
                farmer.modData.Remove(modDataKey);
            }
            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void StripAccessory(Farmer farmer, ClothingData data)
        {
            // Fashion Sense - strip ALL three accessory slots using CORRECT numbered keys
            const string accessory0Key = "FashionSense.CustomAccessory.0.Id";
            const string accessory1Key = "FashionSense.CustomAccessory.1.Id";
            const string accessory2Key = "FashionSense.CustomAccessory.2.Id";

            if (farmer.modData.ContainsKey(accessory0Key))
            {
                data.FashionSenseAccessory = farmer.modData[accessory0Key];
                farmer.modData.Remove(accessory0Key);
            }

            if (farmer.modData.ContainsKey(accessory1Key))
            {
                data.FashionSenseAccessorySecondary = farmer.modData[accessory1Key];
                farmer.modData.Remove(accessory1Key);
            }

            if (farmer.modData.ContainsKey(accessory2Key))
            {
                data.FashionSenseAccessoryTertiary = farmer.modData[accessory2Key];
                farmer.modData.Remove(accessory2Key);
            }

            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private static void StripAccessorySlot(Farmer farmer, ClothingData data, int slot)
        {
            string key = $"FashionSense.CustomAccessory.{slot}.Id";

            if (farmer.modData.ContainsKey(key))
            {
                string value = farmer.modData[key];
                farmer.modData.Remove(key);

                // Store in appropriate slot
                switch (slot)
                {
                    case 0:
                        data.FashionSenseAccessory = value;
                        break;
                    case 1:
                        data.FashionSenseAccessorySecondary = value;
                        break;
                    case 2:
                        data.FashionSenseAccessoryTertiary = value;
                        break;
                }
            }

            farmer.FarmerRenderer.MarkSpriteDirty();
        }

        private class ClothingData
        {
            public string FashionSenseHat { get; set; }
            public string FashionSenseShirt { get; set; }
            public string FashionSensePants { get; set; }
            public string FashionSenseSleeves { get; set; }
            public string FashionSenseShoes { get; set; }
            public string FashionSenseAccessory { get; set; }
            public string FashionSenseAccessorySecondary { get; set; }
            public string FashionSenseAccessoryTertiary { get; set; }

            public Clothing VanillaShirt { get; set; }
            public string VanillaShirtIndex { get; set; }
            public Clothing VanillaPants { get; set; }
            public string VanillaPantsIndex { get; set; }
            public Boots VanillaShoes { get; set; }
            public Hat VanillaHat { get; set; }
        }
    }

    public enum FashionSenseType
    {
        Unknown = 0,
        Hair = 1,
        Accessory = 2,
        Hat = 5,
        Shirt = 6,
        Pants = 7,
        Sleeves = 8,
        Shoes = 9
    }

    public interface IFashionSenseApi
    {
        KeyValuePair<bool, string> GetCurrentAppearanceId(FashionSenseType appearanceType, Farmer target = null);
        KeyValuePair<bool, string> SetAppearance(FashionSenseType appearanceType, string targetPackId, string targetAppearanceName, IManifest callerManifest);
        KeyValuePair<bool, string> ClearAppearance(FashionSenseType appearanceType, IManifest callerManifest);
    }
}