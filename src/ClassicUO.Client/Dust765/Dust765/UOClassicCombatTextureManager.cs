using System.Collections.Generic;
using System.IO;
using System.Linq;

using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using ClassicUO.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Dust765.Dust765
{
    internal class TextureManager
    {
        public bool IsEnabled => ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.TextureManagerEnabled;
        public bool IsArrowEnabled => ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.TextureManagerArrows;
        public bool IsHalosEnabled => ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.TextureManagerHalos;


        private static BlendState _blend = new BlendState
        {
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha
        };

        private static Vector3 _hueVector = Vector3.Zero;

        private static Texture2D LoadEmbeddedPng(ref Texture2D cache, string fileName)
        {
            if (cache != null && !cache.IsDisposed)
            {
                return cache;
            }

            cache = null;
            var asm = typeof(CUOEnviroment).Assembly;
            string r1 = "ClassicUO." + fileName;
            using (Stream s = asm.GetManifestResourceStream(r1) ?? asm.GetManifestResourceStream("cuo." + fileName))
            {
                if (s == null)
                {
                    return null;
                }

                cache = Texture2D.FromStream(Client.Game.GraphicsDevice, s);
            }

            return cache;
        }

        //TEXTURES ARROW
        private int ARROW_WIDTH_HALF = 14;
        private int ARROW_HEIGHT_HALF = 14;

        private static Texture2D _arrowGreen;
        public static Texture2D ArrowGreen => LoadEmbeddedPng(ref _arrowGreen, "arrow_green.png");

        private static Texture2D _arrowPurple;
        public static Texture2D ArrowPurple => LoadEmbeddedPng(ref _arrowPurple, "arrow_purple.png");

        private static Texture2D _arrowRed;
        public static Texture2D ArrowRed => LoadEmbeddedPng(ref _arrowRed, "arrow_red.png");

        private static Texture2D _arrowYellow;
        public static Texture2D ArrowYellow => LoadEmbeddedPng(ref _arrowYellow, "arrow_yellow.png");

        private static Texture2D _arrowOrange;
        public static Texture2D ArrowOrange => LoadEmbeddedPng(ref _arrowOrange, "arrow_orange.png");

        private static Texture2D _arrowBlue;
        public static Texture2D ArrowBlue => LoadEmbeddedPng(ref _arrowBlue, "arrow_blue2.png");
        //TEXTURES HALO
        private int HALO_WIDTH_HALF = 25;

        private static Texture2D _haloGreen;
        private static Texture2D HaloGreen => LoadEmbeddedPng(ref _haloGreen, "halo_green.png");

        private static Texture2D _haloPurple;
        private static Texture2D HaloPurple => LoadEmbeddedPng(ref _haloPurple, "halo_purple.png");

        private static Texture2D _haloRed;
        private static Texture2D HaloRed => LoadEmbeddedPng(ref _haloRed, "halo_red.png");

        private static Texture2D _haloWhite;
        private static Texture2D HaloWhite => LoadEmbeddedPng(ref _haloWhite, "halo_white.png");

        private static Texture2D _haloYellow;
        private static Texture2D HaloYellow => LoadEmbeddedPng(ref _haloYellow, "halo_yellow.png");

        private static Texture2D _haloOrange;
        private static Texture2D HaloOrange => LoadEmbeddedPng(ref _haloOrange, "halo_orange.png");

        private static Texture2D _haloBlue;
        private static Texture2D HaloBlue => LoadEmbeddedPng(ref _haloBlue, "halo_blue2.png");
        //TEXTURES END
        public void Draw(UltimaBatcher2D batcher)
        {
            if (!IsEnabled)
                return;

            if (World.Player == null)
                return;

            // Apply performance optimizations
            PerformanceOptimizations.UpdatePerformanceMetrics();
            
            int allMobilesCount = World.Mobiles.Count;
            var visibleMobiles = new List<Mobile>();
            foreach (KeyValuePair<uint, Mobile> mkv in World.Mobiles)
            {
                Mobile mobile = mkv.Value;
                if (
                    PerformanceOptimizations.IsObjectVisible(mobile, Client.Game.Scene.Camera)
                    && PerformanceOptimizations.ShouldRenderAtLOD(mobile, Client.Game.Scene.Camera)
                )
                {
                    visibleMobiles.Add(mobile);
                }
            }

            var groupedMobiles = PerformanceOptimizations.GroupObjectsByType(visibleMobiles.Cast<Entity>());

            PerformanceMonitor.RecordObjectCount(allMobilesCount, visibleMobiles.Count);
            PerformanceMonitor.Update();

            foreach (var group in groupedMobiles)
            {
                foreach (Mobile mobile in group.Value.Cast<Mobile>())
            {
                //SKIP FOR PLAYER
                if (mobile == World.Player)
                    continue;

                _hueVector.Z = 1f;

                if (IsHalosEnabled && (HaloOrange != null || HaloGreen != null || HaloPurple != null || HaloRed != null || HaloBlue != null))
                {
                    //CALC WHERE MOBILE IS
                    Point pm = CombatCollection.CalcUnderChar(mobile);
                    //Move from center by half texture width
                    pm.X -= HALO_WIDTH_HALF;

                    //_hueVector.Y = ShaderHueTranslator.SHADER_LIGHTS;
                    _hueVector.Y = _hueVector.X > 1.0f ? ShaderHueTranslator.SHADER_LIGHTS : ShaderHueTranslator.SHADER_NONE;
                    batcher.SetBlendState(_blend);

                    //HUMANS ONLY
                    if (ProfileManager.CurrentProfile.TextureManagerHumansOnly && !mobile.IsHuman)
                    {

                    }
                    else
                    {
                        //PURPLE HALO FOR: LAST ATTACK, LASTTARGET
                        if (HaloPurple != null && ((TargetManager.LastAttack == mobile.Serial & TargetManager.LastAttack != 0 & ProfileManager.CurrentProfile.TextureManagerPurple) || (TargetManager.LastTargetInfo.Serial == mobile.Serial & TargetManager.LastTargetInfo.Serial != 0 & ProfileManager.CurrentProfile.TextureManagerPurple)))
                            batcher.Draw(HaloPurple, new Rectangle(pm.X, pm.Y - 15, HaloPurple.Width, HaloPurple.Height), _hueVector);

                        //GREEN HALO FOR: ALLYS AND PARTY
                        else if (HaloGreen != null && (mobile.NotorietyFlag == NotorietyFlag.Ally & ProfileManager.CurrentProfile.TextureManagerGreen || World.Party.Contains(mobile.Serial) & ProfileManager.CurrentProfile.TextureManagerGreen))
                            batcher.Draw(HaloGreen, new Rectangle(pm.X, pm.Y - 15, HaloGreen.Width, HaloGreen.Height), _hueVector);
                        //RED HALO FOR: CRIMINALS, GRAY, MURDERER
                        else if (mobile.NotorietyFlag == NotorietyFlag.Criminal || mobile.NotorietyFlag == NotorietyFlag.Gray || mobile.NotorietyFlag == NotorietyFlag.Murderer)
                        {
                            if (HaloRed != null && ProfileManager.CurrentProfile.TextureManagerRed)
                            {
                                batcher.Draw(HaloRed, new Rectangle(pm.X, pm.Y - 15, HaloRed.Width, HaloRed.Height), _hueVector);
                            }
                        }
                        //ORANGE HALO FOR: ENEMY
                        else if (HaloOrange != null && mobile.NotorietyFlag == NotorietyFlag.Enemy & ProfileManager.CurrentProfile.TextureManagerOrange)
                            batcher.Draw(HaloOrange, new Rectangle(pm.X, pm.Y - 15, HaloOrange.Width, HaloOrange.Height), _hueVector);
                        //BLUE HALO FOR: INNOCENT
                        else if (HaloBlue != null && mobile.NotorietyFlag == NotorietyFlag.Innocent & ProfileManager.CurrentProfile.TextureManagerBlue)
                            batcher.Draw(HaloBlue, new Rectangle(pm.X, pm.Y - 15, HaloBlue.Width, HaloBlue.Height), _hueVector);
                    }

                    batcher.SetBlendState(null);
                }
                //HALO TEXTURE

                //ARROW TEXTURE
                if (IsArrowEnabled && (ArrowGreen != null || ArrowRed != null || ArrowPurple != null || ArrowOrange != null || ArrowBlue != null))
                {
                    //CALC MOBILE HEIGHT FROM ANIMATION
                    Point p1 = CombatCollection.CalcOverChar(mobile);
                    //Move from center by half texture width
                    p1.X -= ARROW_WIDTH_HALF;
                    p1.Y -= ARROW_HEIGHT_HALF;

                    /* MAYBE USE THIS INCASE IT SHOWS OUTSIDE OF GAMESCREEN?
                    if (!(p1.X < 0 || p1.X > screenW - mobile.HitsTexture.Width || p1.Y < 0 || p1.Y > screenH))
                                {
                                    mobile.HitsTexture.Draw(batcher, p1.X, p1.Y);
                                }
                    */

                    //_hueVector.Y = ShaderHueTranslator.SHADER_LIGHTS;
                    _hueVector.Y = _hueVector.X > 1.0f ? ShaderHueTranslator.SHADER_LIGHTS : ShaderHueTranslator.SHADER_NONE;
                    batcher.SetBlendState(_blend);

                    //HUMANS ONLY
                    if (ProfileManager.CurrentProfile.TextureManagerHumansOnlyArrows && !mobile.IsHuman)
                    {
                        batcher.SetBlendState(null);
                        continue;
                    }

                    //PURPLE ARROW FOR: LAST ATTACK, LASTTARGET
                    if (ArrowPurple != null && ((TargetManager.LastAttack == mobile.Serial & TargetManager.LastAttack != 0 & ProfileManager.CurrentProfile.TextureManagerPurpleArrows) || (TargetManager.LastTargetInfo.Serial == mobile.Serial & TargetManager.LastTargetInfo.Serial != 0 & ProfileManager.CurrentProfile.TextureManagerPurpleArrows)))
                        batcher.Draw(ArrowPurple, new Rectangle(p1.X, p1.Y, ArrowPurple.Width, ArrowPurple.Height), _hueVector);
                    //GREEN ARROW FOR: ALLYS AND PARTY
                    else if (ArrowGreen != null && (mobile.NotorietyFlag == NotorietyFlag.Ally & ProfileManager.CurrentProfile.TextureManagerGreenArrows || World.Party.Contains(mobile.Serial)) && mobile != World.Player & ProfileManager.CurrentProfile.TextureManagerGreenArrows)
                        batcher.Draw(ArrowGreen, new Rectangle(p1.X, p1.Y, ArrowGreen.Width, ArrowGreen.Height), _hueVector);
                    //RED ARROW FOR: CRIMINALS, GRAY, MURDERER
                    else if (mobile.NotorietyFlag == NotorietyFlag.Criminal || mobile.NotorietyFlag == NotorietyFlag.Gray || mobile.NotorietyFlag == NotorietyFlag.Murderer)
                    {
                        if (ArrowRed != null && ProfileManager.CurrentProfile.TextureManagerRedArrows)
                            batcher.Draw(ArrowRed, new Rectangle(p1.X, p1.Y, ArrowRed.Width, ArrowRed.Height), _hueVector);
                    }
                    //ORANGE ARROW FOR: ENEMY
                    else if (ArrowOrange != null && mobile.NotorietyFlag == NotorietyFlag.Enemy & ProfileManager.CurrentProfile.TextureManagerOrangeArrows)
                        batcher.Draw(ArrowOrange, new Rectangle(p1.X, p1.Y, ArrowOrange.Width, ArrowOrange.Height), _hueVector);
                    //BLUE ARROW FOR: INNOCENT
                    else if (ArrowBlue != null && mobile.NotorietyFlag == NotorietyFlag.Innocent & ProfileManager.CurrentProfile.TextureManagerBlueArrows)
                        batcher.Draw(ArrowBlue, new Rectangle(p1.X, p1.Y, ArrowBlue.Width, ArrowBlue.Height), _hueVector);

                    batcher.SetBlendState(null);
                }
                //ARROW TEXTURE
                }
            }
            
            // Draw performance stats if enabled
            PerformanceMonitor.Draw(batcher);
        }
    }
}