using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Dust765.Managers;
using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;

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
        public SpellAction _spell;

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

            if (ProfileManager.CurrentProfile?.OnCastingGump_hidden != true)
            {
                IsVisible = true;
            }

            try
            {
                _spell = (SpellAction)_spell_id;
                circle = (uint)SpellManager.GetCircle(_spell);
                uint protection_delay = 0;
                bool ignore_proctetion_delay = (_spell == SpellAction.Protection || _spell == SpellAction.ArchProtection);
                if (World.Player.IsBuffIconExists(BuffIconType.Protection) && !ignore_proctetion_delay
                    || World.Player.IsBuffIconExists(BuffIconType.EssenceOfWind))
                {
                    protection_delay = 2;
                }
                _endTime = _startTime + 400 + (circle + protection_delay) * 250 + _re;
                GameActions.iscasting = true;
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
            if (!IsVisible ||
                ProfileManager.CurrentProfile == null ||
                !ProfileManager.CurrentProfile.OnCastingGump ||
                World.Player == null ||
                World.Player.IsDestroyed)
            {
                return false;
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
