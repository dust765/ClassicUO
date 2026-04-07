using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Dust765.Managers;
using System;
using ClassicUO.Game.Data;
using Microsoft.Xna.Framework;

namespace ClassicUO.Dust765.External
{
    public class OnCastingGump : Gump
    {
        private const byte _iconSize = 16, _spaceSize = 3, _borderSize = 3;
        public uint Timer { get; set; }
        private uint _startTime;
        private uint _endTime;
        private AlphaBlendControl _background;
        private Label _text;

        public OnCastingGump() : base(0, 0)
        {
            CanMove = false;
            AcceptMouseInput = false;
            CanCloseWithEsc = false;
            CanCloseWithRightClick = false;
            GameActions.iscasting = false;
            IsVisible = false;
            BuildGump();
        }

        private static int[] _stopAtClilocs = new int[]
        {
            500641,
            502625,
            502630,
            500946,
            500015,
            502643,
            1061091,
            502644,
            1072060,
        };

        public void Start(uint _spell_id, uint _re = 0)
        {
            _startTime = Time.Ticks;
            uint circle;

            if (!ProfileManager.CurrentProfile.OnCastingGump_hidden)
            {
                IsVisible = true;
            }

            try
            {
                SpellAction spell = (SpellAction)_spell_id;
                circle = (uint)SpellManager.GetCircle(spell);
                uint protection_delay = 0;
                if (World.Player.IsBuffIconExists(BuffIconType.Protection))
                {
                    protection_delay = 1;
                    if (circle != 9)
                    {
                        protection_delay = protection_delay + 2;
                    }
                    else
                    {
                        protection_delay = protection_delay + 5;
                        circle = circle + 2;
                    }
                }
                _endTime = _startTime + 400 + (circle + protection_delay) * 250 + _re;
                GameActions.iscasting = true;
                GameActions.spellCircle = spell;
            }
            catch
            {
                Stop();
            }
        }

        public void Stop()
        {
            GameActions.iscasting = false;
            IsVisible = false;
            GameActions.spellCircle = 0;
        }

        public float GetCastProgress()
        {
            if (!GameActions.iscasting || _endTime <= _startTime)
            {
                return 0f;
            }

            float progress = (Time.Ticks - _startTime) / (float)(_endTime - _startTime);

            if (progress < 0f)
            {
                return 0f;
            }

            if (progress > 1f)
            {
                return 1f;
            }

            return progress;
        }

        public void OnCliloc(uint cliloc)
        {
            if (!GameActions.iscasting)
            {
                return;
            }

            for (int i = 0; i < _stopAtClilocs.Length; i++)
            {
                if (_stopAtClilocs[i] == cliloc)
                {
                    Stop();
                    return;
                }
            }
        }

        public static void OnSceneLoad()
        {
        }

        public static void OnSceneUnload()
        {
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (ProfileManager.CurrentProfile == null ||
                !ProfileManager.CurrentProfile.OnCastingGump ||
                World.Player == null ||
                World.Player.IsDestroyed)
            {
                return false;
            }

            if (ProfileManager.CurrentProfile.OnCastingUnderPlayerBar && GameActions.iscasting)
            {
                float progress = GetCastProgress();
                if (progress > 0f)
                {
                    int gx = ProfileManager.CurrentProfile.GameWindowPosition.X;
                    int gy = ProfileManager.CurrentProfile.GameWindowPosition.Y;

                    int px = gx + World.Player.RealScreenPosition.X + (int)World.Player.Offset.X;
                    int py = gy + World.Player.RealScreenPosition.Y + (int)(World.Player.Offset.Y - World.Player.Offset.Z);

                    int barWidth = 28;
                    int barHeight = 3;
                    int barX = px - (barWidth >> 1) + 10;
                    int barY = py + 16;
                    int fillWidth = Math.Max(1, (int)(barWidth * progress));
                    Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 1f);

                    batcher.Draw(SolidColorTextureCache.GetTexture(new Color(14, 14, 14, 220)), new Rectangle(barX, barY, barWidth, barHeight), hue);
                    batcher.Draw(SolidColorTextureCache.GetTexture(Color.Red), new Rectangle(barX, barY, fillWidth, barHeight), hue);
                    batcher.DrawRectangle(SolidColorTextureCache.GetTexture(Color.Black), barX, barY, barWidth, barHeight, hue);
                }
            }

            if (!IsVisible)
            {
                return true;
            }

            Width = _borderSize * 2 + _iconSize + _spaceSize + _text.Width;
            Height = _borderSize * 2 + _iconSize;

            _background.Width = Width;
            _background.Height = Height;

            return base.Draw(batcher, Mouse.Position.X, Mouse.Position.Y);
        }

        public override void Update()
        {
            base.Update();

            if (IsDisposed)
            {
                return;
            }

            if (World.Player == null || World.Player.IsDestroyed)
            {
                Dispose();
                return;
            }

            if (GameActions.iscasting)
            {
                if (Time.Ticks >= _endTime)
                {
                    Stop();
                }
            }
            else
            {
                if (!GameActions.iscasting && IsVisible)
                {
                    Stop();
                }
            }
        }

        private void BuildGump()
        {
            _background = new AlphaBlendControl()
            {
                Alpha = 0.6f
            };

            _text = new Label($"Casting", true, 0x35, 0, 1, FontStyle.BlackBorder)
            {
                X = _borderSize + _iconSize + _spaceSize + 3,
                Y = _borderSize - 2
            };

            Add(_background);
            Add(_text);
        }
    }
}
