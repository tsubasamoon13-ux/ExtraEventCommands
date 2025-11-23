using ExtraEventCommands;
using HarmonyLib;
using StardewModdingAPI;
using System;

namespace YourModNamespace
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var harmony = new Harmony(ModManifest.UniqueID);

            // Initialize rotation features
            rotateFarmer.Initialize(helper, Monitor, harmony);

            // Initialize clothing toggle features
            ClothingToggle.Initialize(helper, Monitor, ModManifest);

            // Initialize Farmer layering features
            FarmerLayering.Initialize(helper, Monitor, ModManifest.UniqueID);

            // Initialize Temp Actor layering features
            TempActorLayering.Initialize(helper, Monitor, ModManifest.UniqueID);
        }
    }
}