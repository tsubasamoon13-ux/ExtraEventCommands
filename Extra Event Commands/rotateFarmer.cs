using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Linq;
using System.Reflection;

namespace ExtraEventCommands
{
    /// <summary>
    /// Provides event commands to rotate the farmer sprite during events.
    /// Compatible with Fashion Sense mod for custom clothing/accessories.
    /// 
    /// The rotation system works by:
    /// 1. Tracking when the farmer is being rendered (via FarmerRenderer.draw or Fashion Sense's DrawManager.DrawLayers)
    /// 2. Intercepting all SpriteBatch.Draw calls during farmer rendering
    /// 3. Applying rotation transformation and adjusting positions to rotate around the farmer's center point
    /// 4. Hiding the farmer's shadow during rotation (except at 0/360 degree angles)
    /// 
    /// Commands:
    /// - rotateFarmer [degrees]: Rotates the farmer by the specified degrees (e.g., 180 for upside-down)
    ///   Rotation persists until reset or the event ends. Shadow is automatically hidden except at 0/360 degrees.
    /// 
    /// - resetFarmerRotation: Resets farmer rotation to 0 degrees and restores shadow
    ///   Also happens automatically when warping or when events end
    /// 
    /// Console Commands (for testing):
    /// - rotate_farmer [degrees]: Same as event command but usable in console
    /// - reset_rotation: Resets rotation via console
    /// </summary>
    public class rotateFarmer
    {
        private static IModHelper Helper;
        private static IMonitor Monitor;
        private static float CurrentRotation;
        private static bool IsRotationActive;
        private static bool IsDrawingFarmerRenderer;
        private static Vector2 FarmerRenderCenter;

        public static void Initialize(IModHelper helper, IMonitor monitor, Harmony harmony)
        {
            Helper = helper;
            Monitor = monitor;

            // Patch FarmerRenderer.draw to track when we're in the renderer
            harmony.Patch(
                original: AccessTools.Method(
                    typeof(FarmerRenderer),
                    "draw",
                    new Type[] {
                        typeof(SpriteBatch),
                        typeof(FarmerSprite.AnimationFrame),
                        typeof(int),
                        typeof(Rectangle),
                        typeof(Vector2),
                        typeof(Vector2),
                        typeof(float),
                        typeof(int),
                        typeof(Color),
                        typeof(float),
                        typeof(float),
                        typeof(Farmer)
                    }),
                prefix: new HarmonyMethod(typeof(rotateFarmer), nameof(FarmerRenderer_Draw_Prefix)),
                postfix: new HarmonyMethod(typeof(rotateFarmer), nameof(FarmerRenderer_Draw_Postfix))
            );

            // Patch Farmer.DrawShadow to skip shadow when rotating (except at 0/360 degrees)
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), "DrawShadow"),
                prefix: new HarmonyMethod(typeof(rotateFarmer), nameof(Farmer_DrawShadow_Prefix))
            );

            // Patch SpriteBatch.Draw to modify rotation AND position
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteBatch), "Draw",
                    new Type[] {
                        typeof(Texture2D),
                        typeof(Vector2),
                        typeof(Rectangle?),
                        typeof(Color),
                        typeof(float),
                        typeof(Vector2),
                        typeof(float),
                        typeof(SpriteEffects),
                        typeof(float)
                    }),
                prefix: new HarmonyMethod(typeof(rotateFarmer), nameof(SpriteBatch_Draw_Prefix))
            );

            // Try to patch Fashion Sense's DrawManager if it exists
            try
            {
                var fashionSenseAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "FashionSense");

                if (fashionSenseAssembly != null)
                {
                    var drawManagerType = fashionSenseAssembly.GetType("FashionSense.Framework.Managers.DrawManager");
                    if (drawManagerType != null)
                    {
                        var drawLayersMethod = AccessTools.Method(drawManagerType, "DrawLayers");
                        if (drawLayersMethod != null)
                        {
                            harmony.Patch(
                                original: drawLayersMethod,
                                prefix: new HarmonyMethod(typeof(rotateFarmer), nameof(FashionSense_DrawLayers_Prefix)),
                                postfix: new HarmonyMethod(typeof(rotateFarmer), nameof(FashionSense_DrawLayers_Postfix))
                            );
                            monitor.Log("Successfully patched Fashion Sense for rotation compatibility", LogLevel.Debug);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"Failed to patch Fashion Sense (rotation may not work with custom clothing): {ex.Message}", LogLevel.Warn);
            }

            helper.ConsoleCommands.Add("rotate_farmer", "Rotate the farmer by X degrees", RotateFarmerCommand);
            helper.ConsoleCommands.Add("reset_rotation", "Reset farmer rotation to 0", ResetRotationCommand);
            Event.RegisterCommand("rotateFarmer", RotateFarmerEventCommand);
            Event.RegisterCommand("resetFarmerRotation", ResetFarmerRotationEventCommand);
            helper.Events.Player.Warped += OnWarped;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private static void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer)
            {
                CurrentRotation = 0f;
                IsRotationActive = false;
            }
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (IsRotationActive && Game1.CurrentEvent == null)
            {
                CurrentRotation = 0f;
                IsRotationActive = false;
            }
        }

        private static void RotateFarmerEventCommand(Event @event, string[] args, EventContext context)
        {
            if (args.Length < 2)
            {
                context.LogErrorAndSkip("rotateFarmer requires a degree parameter");
            }
            else if (float.TryParse(args[1], out float degrees))
            {
                CurrentRotation = MathHelper.ToRadians(degrees);
                IsRotationActive = true;
                @event.CurrentCommand++;
            }
            else
            {
                context.LogErrorAndSkip("Invalid degree value: " + args[1]);
            }
        }

        private static void ResetFarmerRotationEventCommand(Event @event, string[] args, EventContext context)
        {
            CurrentRotation = 0f;
            IsRotationActive = false;
            @event.CurrentCommand++;
        }

        private static void RotateFarmerCommand(string command, string[] args)
        {
            if (args.Length == 0)
            {
                Monitor.Log("Usage: rotate_farmer <degrees>", LogLevel.Info);
            }
            else if (float.TryParse(args[0], out float degrees))
            {
                CurrentRotation = MathHelper.ToRadians(degrees);
                IsRotationActive = true;
                Monitor.Log($"Rotating farmer by {degrees} degrees", LogLevel.Info);
            }
        }

        private static void ResetRotationCommand(string command, string[] args)
        {
            CurrentRotation = 0f;
            IsRotationActive = false;
            Monitor.Log("Reset farmer rotation", LogLevel.Info);
        }

        private static bool Farmer_DrawShadow_Prefix(Farmer __instance)
        {
            // Skip drawing shadow if this is the player and rotation is active AND not at a 0/360 degree angle
            if (__instance == Game1.player && IsRotationActive)
            {
                // Calculate degrees from radians, normalize to 0-360 range
                float degrees = MathHelper.ToDegrees(CurrentRotation) % 360f;

                // Normalize negative angles to positive (e.g., -90 becomes 270)
                if (degrees < 0)
                {
                    degrees += 360f;
                }

                // Show shadow only at 0 degrees (accounting for floating point precision)
                // This handles 0, 360, 720, etc.
                if (Math.Abs(degrees) > 0.01f && Math.Abs(degrees - 360f) > 0.01f)
                {
                    return false; // Skip shadow drawing
                }
            }
            return true; // Draw shadow normally
        }

        private static void FarmerRenderer_Draw_Prefix(Farmer who, Vector2 position, Vector2 origin)
        {
            if (IsRotationActive && who == Game1.player)
            {
                IsDrawingFarmerRenderer = true;
                FarmerRenderCenter = position + origin;
            }
        }

        private static void FarmerRenderer_Draw_Postfix()
        {
            IsDrawingFarmerRenderer = false;
        }

        private static void FashionSense_DrawLayers_Prefix(Farmer who, object __instance)
        {
            if (who == Game1.player && IsRotationActive)
            {
                IsDrawingFarmerRenderer = true;

                // Get DrawTool from the DrawManager instance - it's internal so we need NonPublic flag
                var drawToolProperty = __instance.GetType().GetProperty("DrawTool", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (drawToolProperty != null)
                {
                    var drawTool = drawToolProperty.GetValue(__instance);
                    if (drawTool != null)
                    {
                        var positionField = drawTool.GetType().GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);
                        var originField = drawTool.GetType().GetProperty("Origin", BindingFlags.Public | BindingFlags.Instance);

                        if (positionField != null && originField != null)
                        {
                            var position = (Vector2)positionField.GetValue(drawTool);
                            var origin = (Vector2)originField.GetValue(drawTool);
                            FarmerRenderCenter = position + origin;
                        }
                    }
                }
            }
        }

        private static void FashionSense_DrawLayers_Postfix()
        {
            IsDrawingFarmerRenderer = false;
        }

        private static void SpriteBatch_Draw_Prefix(ref float rotation, ref Vector2 position, Vector2 origin)
        {
            if (IsDrawingFarmerRenderer && IsRotationActive)
            {
                // Add rotation
                rotation += CurrentRotation;

                // Rotate the position around the farmer's center
                Vector2 offset = position - FarmerRenderCenter;
                float cos = (float)Math.Cos(CurrentRotation);
                float sin = (float)Math.Sin(CurrentRotation);

                Vector2 rotatedOffset = new Vector2(
                    offset.X * cos - offset.Y * sin,
                    offset.X * sin + offset.Y * cos
                );

                position = FarmerRenderCenter + rotatedOffset;
            }
        }
    }
}
