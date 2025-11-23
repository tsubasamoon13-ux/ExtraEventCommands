using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace ExtraEventCommands
{
    /// <summary>
    /// Provides event commands to control the render order of the farmer relative to NPCs during events.
    /// Compatible with Fashion Sense mod - all clothing layers will follow the farmer's layer position.
    /// Uses Harmony patches to intercept Event drawing and manipulate layer depth values.
    /// 
    /// Commands:
    /// - farmerAbove [npcName]: Renders the farmer (and all Fashion Sense clothing) in front of the specified NPC
    /// - farmerBelow [npcName]: Renders the farmer (and all Fashion Sense clothing) behind the specified NPC
    /// - resetLayers: Clears all layer overrides
    /// 
    /// How it works:
    /// 1. Patches Event.drawFarmers to skip drawing Game1.player
    /// 2. Patches Event.draw to insert farmer drawing at the correct position in the NPC loop
    /// 3. Uses farmer's drawLayerDisambiguator to force all rendering (including Fashion Sense) to the correct layer
    /// 4. Patches Fashion Sense's DrawPatch to adjust the base layer depth for all clothing items
    /// </summary>
    public class FarmerLayering
    {
        private static IMonitor Monitor;
        private static Dictionary<string, float> LayerOverrides = new Dictionary<string, float>();
        private static Harmony Harmony;
        private static bool IsFashionSensePatched = false;

        // Store the current farmer being drawn for Fashion Sense patches
        private static Farmer CurrentFarmerBeingDrawn = null;

        public static void Initialize(IModHelper helper, IMonitor monitor, string modId)
        {
            Monitor = monitor;
            Harmony = new Harmony(modId + ".FarmerLayering");

            // Patch Event.draw to control NPC + Farmer draw order
            Harmony.Patch(
                original: AccessTools.Method(typeof(Event), "draw", new Type[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(FarmerLayering), nameof(Event_draw_Prefix))
            );

            // Skip normal farmer drawing when we have overrides
            Harmony.Patch(
                original: AccessTools.Method(typeof(Event), "drawFarmers"),
                prefix: new HarmonyMethod(typeof(FarmerLayering), nameof(Event_drawFarmers_Prefix))
            );

            // Patch Farmer.draw to track when we're drawing the player
            Harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), "draw", new Type[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(FarmerLayering), nameof(Farmer_draw_Prefix)),
                postfix: new HarmonyMethod(typeof(FarmerLayering), nameof(Farmer_draw_Postfix))
            );

            // Try to patch Fashion Sense if it's loaded
            TryPatchFashionSense();

            // Register event commands
            StardewValley.Event.RegisterCommand("farmerAbove", FarmerAboveCommand);
            StardewValley.Event.RegisterCommand("farmerBelow", FarmerBelowCommand);
            StardewValley.Event.RegisterCommand("resetLayers", ResetLayersCommand);

            // Reset on event end
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private static void TryPatchFashionSense()
        {
            try
            {
                var fashionSenseAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "FashionSense");

                if (fashionSenseAssembly != null)
                {
                    // Patch DrawPatch.DrawPrefix to modify layerDepth before DrawManager is created
                    var drawPatchType = fashionSenseAssembly.GetType("FashionSense.Framework.Patches.Renderer.DrawPatch");
                    if (drawPatchType != null)
                    {
                        var drawPrefixMethod = AccessTools.Method(drawPatchType, "DrawPrefix");
                        if (drawPrefixMethod != null)
                        {
                            Harmony.Patch(
                                original: drawPrefixMethod,
                                prefix: new HarmonyMethod(typeof(FarmerLayering), nameof(DrawPatch_DrawPrefix_Prefix))
                            );
                            IsFashionSensePatched = true;
                            Monitor.Log("Successfully patched Fashion Sense for layer depth control", LogLevel.Info);
                        }
                    }

                    if (!IsFashionSensePatched)
                    {
                        Monitor.Log("Fashion Sense detected but could not patch DrawPatch", LogLevel.Warn);
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to patch Fashion Sense: {ex.Message}", LogLevel.Warn);
            }
        }

        // Patch Fashion Sense's DrawPatch.DrawPrefix to modify layerDepth BEFORE DrawManager is created
        private static void DrawPatch_DrawPrefix_Prefix(Farmer who, ref float layerDepth)
        {
            // Only apply to the player when we have layer overrides
            if (who != Game1.player || LayerOverrides.Count == 0)
            {
                return;
            }

            // Modify the layerDepth to match the farmer's adjusted layer
            // This will be passed to DrawManager and used as the base for all Fashion Sense clothing
            layerDepth = who.getDrawLayer() + who.drawLayerDisambiguator;
        }

        private static void Farmer_draw_Prefix(Farmer __instance)
        {
            if (__instance == Game1.player && LayerOverrides.Count > 0)
            {
                CurrentFarmerBeingDrawn = __instance;
            }
        }

        private static void Farmer_draw_Postfix(Farmer __instance)
        {
            if (__instance == Game1.player)
            {
                CurrentFarmerBeingDrawn = null;
            }
        }

        private static void OnUpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            // Auto-reset when event ends
            if (Game1.CurrentEvent == null && LayerOverrides.Count > 0)
            {
                Game1.player.drawLayerDisambiguator = 0f;
                LayerOverrides.Clear();
                CurrentFarmerBeingDrawn = null;
                Monitor.Log("Auto-reset NPC layering after event ended", LogLevel.Debug);
            }
        }

        // Skip normal farmer drawing when we have layer overrides
        private static bool Event_drawFarmers_Prefix(Event __instance, SpriteBatch b)
        {
            if (LayerOverrides.Count == 0)
            {
                return true; // No overrides, use normal drawing
            }

            // Draw all farmers except Game1.player (we handle that in Event_draw_Prefix)
            foreach (Farmer farmerActor in __instance.farmerActors)
            {
                if (farmerActor != Game1.player)
                {
                    farmerActor.draw(b);
                }
            }

            return false; // Skip original method
        }

        // Control NPC and farmer draw order by intercepting Event.draw
        private static bool Event_draw_Prefix(Event __instance, SpriteBatch b)
        {
            if (LayerOverrides.Count == 0 || __instance.currentCustomEventScript != null)
            {
                return true; // No overrides or custom script, use normal drawing
            }

            // Handle custom event scripts normally
            if (__instance.currentCustomEventScript != null)
            {
                __instance.currentCustomEventScript.draw(b);
                return false;
            }

            // Get the target NPC and offset
            string targetNpcName = null;
            float offset = 0f;
            foreach (var kvp in LayerOverrides)
            {
                targetNpcName = kvp.Key;
                offset = kvp.Value;
                break; // Only handle first override
            }

            if (targetNpcName == null)
            {
                return true; // No override, use normal drawing
            }

            Farmer farmer = Game1.player;
            bool farmerDrawn = false;
            float originalDisambiguator = farmer.drawLayerDisambiguator;

            // Draw NPCs, inserting farmer at the correct layer depth
            foreach (NPC n in __instance.actors)
            {
                if (__instance.ShouldHideCharacter(n))
                {
                    continue;
                }

                // Calculate NPC's layer depth
                float npcLayerDepth = (float)n.StandingPixel.Y / 10000f;

                // Draw farmer BEFORE this NPC if farmer should be below (behind)
                if (n.Name == targetNpcName && offset > 0 && !farmerDrawn)
                {
                    // Adjust farmer's layer to be below NPC
                    float farmerBaseLayer = farmer.getDrawLayer();
                    farmer.drawLayerDisambiguator = npcLayerDepth - farmerBaseLayer - 0.001f;

                    farmer.draw(b);
                    farmerDrawn = true;

                    // Restore original
                    farmer.drawLayerDisambiguator = originalDisambiguator;
                }

                // Draw the NPC
                if (n.ySourceRectOffset == 0)
                {
                    n.draw(b);
                }
                else
                {
                    n.draw(b, n.ySourceRectOffset);
                }

                // Draw farmer AFTER this NPC if farmer should be above (in front)
                if (n.Name == targetNpcName && offset < 0 && !farmerDrawn)
                {
                    // Adjust farmer's layer to be above NPC
                    float farmerBaseLayer = farmer.getDrawLayer();
                    farmer.drawLayerDisambiguator = npcLayerDepth - farmerBaseLayer + 0.001f;

                    farmer.draw(b);
                    farmerDrawn = true;

                    // Restore original
                    farmer.drawLayerDisambiguator = originalDisambiguator;
                }
            }

            // If farmer still hasn't been drawn, draw them now
            if (!farmerDrawn)
            {
                farmer.draw(b);
                farmer.drawLayerDisambiguator = originalDisambiguator;
            }

            // Draw props and festival props (from original method)
            foreach (StardewValley.Object prop in __instance.props)
            {
                prop.drawAsProp(b);
            }

            foreach (Prop festivalProp in __instance.festivalProps)
            {
                festivalProp.draw(b);
            }

            // Handle special festival grange display code (from original)
            if (__instance.isSpecificFestival("fall16"))
            {
                DrawGrangeDisplay(__instance, b);
            }

            // Handle drawTool (using reflection since it's private)
            bool drawToolFlag = AccessTools.Field(typeof(Event), "drawTool").GetValue(__instance) as bool? ?? false;
            if (drawToolFlag)
            {
                Farmer eventFarmer = AccessTools.Field(typeof(Event), "farmer").GetValue(__instance) as Farmer;
                if (eventFarmer != null)
                {
                    Game1.drawTool(eventFarmer);
                }
            }

            return false; // Skip original method
        }

        // Helper to draw grange display (copied from original Event.draw)
        private static void DrawGrangeDisplay(Event @event, SpriteBatch b)
        {
            var start = Game1.GlobalToLocal(Game1.viewport, new Microsoft.Xna.Framework.Vector2(37f, 56f) * 64f);
            start.X += 4f;
            int xCutoff = (int)start.X + 168;
            start.Y += 8f;

            for (int i = 0; i < Game1.player.team.grangeDisplay.Count; i++)
            {
                if (Game1.player.team.grangeDisplay[i] != null)
                {
                    start.Y += 42f;
                    start.X += 4f;
                    b.Draw(Game1.shadowTexture, start, Game1.shadowTexture.Bounds, Microsoft.Xna.Framework.Color.White,
                        0f, Microsoft.Xna.Framework.Vector2.Zero, 4f, SpriteEffects.None, 0.0001f);
                    start.Y -= 42f;
                    start.X -= 4f;
                    Game1.player.team.grangeDisplay[i].drawInMenu(b, start, 1f, 1f, (float)i / 1000f + 0.001f,
                        StackDrawType.Hide);
                }
                start.X += 60f;
                if (start.X >= (float)xCutoff)
                {
                    start.X = xCutoff - 168;
                    start.Y += 64f;
                }
            }
        }

        private static void FarmerAboveCommand(Event @event, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string npcName, out string error))
            {
                context.LogErrorAndSkip("farmerAbove requires an NPC name");
                return;
            }

            NPC npc = @event.getActorByName(npcName);
            if (npc == null)
            {
                context.LogErrorAndSkip($"NPC '{npcName}' not found in event actors");
                return;
            }

            LayerOverrides.Clear();
            LayerOverrides[npcName] = -1f; // Negative = farmer above (in front)
            Monitor.Log($"Set farmer to render above '{npcName}'", LogLevel.Debug);

            @event.CurrentCommand++;
        }

        private static void FarmerBelowCommand(Event @event, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string npcName, out string error))
            {
                context.LogErrorAndSkip("farmerBelow requires an NPC name");
                return;
            }

            NPC npc = @event.getActorByName(npcName);
            if (npc == null)
            {
                context.LogErrorAndSkip($"NPC '{npcName}' not found in event actors");
                return;
            }

            LayerOverrides.Clear();
            LayerOverrides[npcName] = 1f; // Positive = farmer below (behind)
            Monitor.Log($"Set farmer to render below '{npcName}'", LogLevel.Debug);

            @event.CurrentCommand++;
        }

        private static void ResetLayersCommand(Event @event, string[] args, EventContext context)
        {
            Game1.player.drawLayerDisambiguator = 0f;
            LayerOverrides.Clear();
            CurrentFarmerBeingDrawn = null;
            Monitor.Log("Reset all layer overrides", LogLevel.Debug);

            @event.CurrentCommand++;
        }
    }
}