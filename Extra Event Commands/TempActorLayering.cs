using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace ExtraEventCommands
{
    /// <summary>
    /// Provides event commands to control the render order of temporary actors during events.
    /// Draws actors as UI overlays that are guaranteed to be on top of everything.
    /// 
    /// The commands automatically move the original actor off-screen to prevent double-drawing.
    /// Overlays automatically reset when events end.
    /// 
    /// Commands:
    /// - tempActorAtTile [actorName] [tileX] [tileY]: Renders at specific tile coordinates (moves with camera)
    ///   RECOMMENDED: Use this for positioning actors at map tiles like normal world objects
    /// 
    /// - tempActorAtScreen [actorName] [screenX] [screenY]: Renders at fixed screen pixel coordinates (doesn't move with camera)
    ///   Use for UI-style positioning that stays in place regardless of camera movement
    /// 
    /// - resetTempActors: Manually resets all actor overlays (happens automatically at event end)
    /// </summary>
    public class TempActorLayering
    {
        private class ActorOverlay
        {
            public NPC Actor;
            public bool UseFixedScreenPosition;
            public bool UseFixedTilePosition;
            public Vector2 FixedScreenPosition;
            public Vector2 FixedTilePosition;

            public ActorOverlay(NPC actor)
            {
                Actor = actor;
                UseFixedScreenPosition = false;
                UseFixedTilePosition = false;
            }
        }

        private static IMonitor Monitor;
        private static IModHelper Helper;
        private static Dictionary<string, ActorOverlay> ActorsOnTop = new Dictionary<string, ActorOverlay>();
        private static Harmony Harmony;

        public static void Initialize(IModHelper helper, IMonitor monitor, string modId)
        {
            Monitor = monitor;
            Helper = helper;
            Harmony = new Harmony(modId + ".TempActorLayering");

            Harmony.Patch(
                original: AccessTools.Method(typeof(Event), "draw", new Type[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(TempActorLayering), nameof(Event_draw_Prefix))
            );

            Harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), "drawCharacters", new Type[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(TempActorLayering), nameof(GameLocation_drawCharacters_Prefix))
            );

            helper.Events.Display.RenderingHud += OnRenderingHud;

            StardewValley.Event.RegisterCommand("tempActorAtScreen", TempActorAtScreenCommand);
            StardewValley.Event.RegisterCommand("tempActorAtTile", TempActorAtTileCommand);
            StardewValley.Event.RegisterCommand("resetTempActors", ResetTempActorsCommand);

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private static void OnUpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (Game1.CurrentEvent == null && ActorsOnTop.Count > 0)
            {
                ActorsOnTop.Clear();
                Monitor.Log("Auto-reset temp actor layering after event ended", LogLevel.Debug);
            }
        }

        private static bool GameLocation_drawCharacters_Prefix(GameLocation __instance, SpriteBatch b)
        {
            if (ActorsOnTop.Count == 0 || Game1.CurrentEvent == null)
            {
                return true;
            }

            if (__instance.shouldHideCharacters() || (Game1.eventUp && (Game1.CurrentEvent == null || !Game1.CurrentEvent.showWorldCharacters)))
            {
                return true;
            }

            for (int i = 0; i < __instance.characters.Count; i++)
            {
                if (__instance.characters[i] != null)
                {
                    if (ActorsOnTop.ContainsKey(__instance.characters[i].Name))
                    {
                        continue;
                    }
                    __instance.characters[i].draw(b);
                }
            }

            return false;
        }

        private static void OnRenderingHud(object sender, StardewModdingAPI.Events.RenderingHudEventArgs e)
        {
            if (Game1.CurrentEvent == null || ActorsOnTop.Count == 0)
            {
                return;
            }

            SpriteBatch b = e.SpriteBatch;

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, null);

            foreach (var overlay in ActorsOnTop.Values)
            {
                NPC actor = overlay.Actor;

                Rectangle sourceRect = actor.Sprite.SourceRect;
                Texture2D texture = actor.Sprite.Texture;

                Vector2 drawPosition;

                if (overlay.UseFixedScreenPosition)
                {
                    // Fixed screen position
                    drawPosition = overlay.FixedScreenPosition;
                }
                else if (overlay.UseFixedTilePosition)
                {
                    // Convert tile position to screen position
                    Vector2 worldPosition = overlay.FixedTilePosition * 64f; // Tile to world pixels
                    drawPosition = Game1.GlobalToLocal(Game1.viewport, worldPosition);
                    drawPosition.Y -= sourceRect.Height * 4; // Adjust for sprite height
                }
                else
                {
                    // Follow actor's actual position
                    Vector2 actorPosition = actor.Position;
                    drawPosition = Game1.GlobalToLocal(Game1.viewport, actorPosition);
                    drawPosition.Y -= sourceRect.Height * 4;
                }

                b.Draw(
                    texture: texture,
                    position: drawPosition,
                    sourceRectangle: sourceRect,
                    color: Color.White,
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 4f,
                    effects: actor.flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                    layerDepth: 0.99f
                );
            }

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, null);
        }

        private static bool Event_draw_Prefix(Event __instance, SpriteBatch b)
        {
            if (ActorsOnTop.Count == 0)
            {
                return true;
            }

            if (__instance.currentCustomEventScript != null)
            {
                __instance.currentCustomEventScript.draw(b);
                return false;
            }

            foreach (NPC n in __instance.actors)
            {
                if (__instance.ShouldHideCharacter(n))
                {
                    continue;
                }

                if (ActorsOnTop.ContainsKey(n.Name))
                {
                    continue;
                }

                if (n.ySourceRectOffset == 0)
                {
                    n.draw(b);
                }
                else
                {
                    n.draw(b, n.ySourceRectOffset);
                }
            }

            foreach (StardewValley.Object prop in __instance.props)
            {
                prop.drawAsProp(b);
            }

            foreach (Prop festivalProp in __instance.festivalProps)
            {
                festivalProp.draw(b);
            }

            if (__instance.isSpecificFestival("fall16"))
            {
                DrawGrangeDisplay(__instance, b);
            }

            if (AccessTools.Field(typeof(Event), "drawTool").GetValue(__instance) as bool? ?? false)
            {
                Farmer eventFarmer = AccessTools.Field(typeof(Event), "farmer").GetValue(__instance) as Farmer;
                if (eventFarmer != null)
                {
                    Game1.drawTool(eventFarmer);
                }
            }

            return false;
        }

        private static void DrawGrangeDisplay(Event @event, SpriteBatch b)
        {
            var start = Game1.GlobalToLocal(Game1.viewport, new Vector2(37f, 56f) * 64f);
            start.X += 4f;
            int xCutoff = (int)start.X + 168;
            start.Y += 8f;

            for (int i = 0; i < Game1.player.team.grangeDisplay.Count; i++)
            {
                if (Game1.player.team.grangeDisplay[i] != null)
                {
                    start.Y += 42f;
                    start.X += 4f;
                    b.Draw(Game1.shadowTexture, start, Game1.shadowTexture.Bounds, Color.White,
                        0f, Vector2.Zero, 4f, SpriteEffects.None, 0.0001f);
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

        private static void TempActorAtScreenCommand(Event @event, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string actorName, out string error))
            {
                context.LogErrorAndSkip("tempActorAtScreen requires an actor name and screen coordinates");
                return;
            }

            NPC actor = @event.getActorByName(actorName);
            if (actor == null)
            {
                context.LogErrorAndSkip($"Temporary actor '{actorName}' not found in event actors");
                return;
            }

            // Require screen position parameters
            if (!ArgUtility.TryGet(args, 2, out string xStr, out error) ||
                !ArgUtility.TryGet(args, 3, out string yStr, out error))
            {
                context.LogErrorAndSkip("tempActorAtScreen requires screen X and Y coordinates (e.g., tempActorAtScreen ActorName 640 360)");
                return;
            }

            if (!float.TryParse(xStr, out float x) || !float.TryParse(yStr, out float y))
            {
                context.LogErrorAndSkip("tempActorAtScreen requires valid numeric screen coordinates");
                return;
            }

            // Automatically warp the original actor off-screen to prevent double-drawing
            actor.Position = new Vector2(-1000, -1000);
            Monitor.Log($"Warped original actor '{actorName}' to off-screen position (-1000, -1000)", LogLevel.Trace);

            var overlay = new ActorOverlay(actor)
            {
                UseFixedScreenPosition = true,
                FixedScreenPosition = new Vector2(x, y)
            };

            Monitor.Log($"Actor '{actorName}' will render at fixed screen position ({x}, {y})", LogLevel.Debug);

            ActorsOnTop[actorName] = overlay;
            @event.CurrentCommand++;
        }

        private static void TempActorAtTileCommand(Event @event, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGet(args, 1, out string actorName, out string error))
            {
                context.LogErrorAndSkip("tempActorAtTile requires an actor name");
                return;
            }

            if (!ArgUtility.TryGet(args, 2, out string tileXStr, out error))
            {
                context.LogErrorAndSkip("tempActorAtTile requires tileX coordinate");
                return;
            }

            if (!ArgUtility.TryGet(args, 3, out string tileYStr, out error))
            {
                context.LogErrorAndSkip("tempActorAtTile requires tileY coordinate");
                return;
            }

            NPC actor = @event.getActorByName(actorName);
            if (actor == null)
            {
                context.LogErrorAndSkip($"Temporary actor '{actorName}' not found in event actors");
                return;
            }

            if (!float.TryParse(tileXStr, out float tileX) || !float.TryParse(tileYStr, out float tileY))
            {
                context.LogErrorAndSkip("tempActorAtTile requires valid numeric tile coordinates");
                return;
            }

            // Automatically warp the original actor off-screen to prevent double-drawing
            actor.Position = new Vector2(-1000, -1000);
            Monitor.Log($"Warped original actor '{actorName}' to off-screen position (-1000, -1000)", LogLevel.Trace);

            var overlay = new ActorOverlay(actor)
            {
                UseFixedTilePosition = true,
                FixedTilePosition = new Vector2(tileX, tileY)
            };

            Monitor.Log($"Actor '{actorName}' will render at tile position ({tileX}, {tileY})", LogLevel.Debug);

            ActorsOnTop[actorName] = overlay;
            @event.CurrentCommand++;
        }

        private static void ResetTempActorsCommand(Event @event, string[] args, EventContext context)
        {
            int count = ActorsOnTop.Count;
            ActorsOnTop.Clear();
            Monitor.Log($"Reset all temporary actor layer overrides ({count} actors)", LogLevel.Debug);

            @event.CurrentCommand++;
        }
    }
}