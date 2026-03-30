using System;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;

namespace ClassicUO.Dust765.UI.Gumps
{
    internal sealed class CombatTrackingModalGump : Gump
    {
        private const int WIDTH = 456;
        private const int HEIGHT = 420;
        private const byte FONT = 0xFF;
        private const ushort HUE_TEXT = 0xFFFF;
        private const ushort HUE_TITLE = 0x0022;
        private Checkbox _cbDamageBar;
        private Checkbox _cbOverhead;
        private Checkbox _cbLowHp;
        private Checkbox _cbKillCount;
        private Checkbox _cbNameProfiles;

        public CombatTrackingModalGump() : base(0, 0)
        {
            X = Math.Max(0, (Client.Game.Window.ClientBounds.Width - WIDTH) >> 1);
            Y = Math.Max(0, (Client.Game.Window.ClientBounds.Height - HEIGHT) >> 1);
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;

            Build();
        }

        private void Build()
        {
            var lang = Language.Instance.GetDust765;
            Profile p = ProfileManager.CurrentProfile;

            Add(new AlphaBlendControl(0.93f)
            {
                X = 1,
                Y = 1,
                Width = WIDTH - 2,
                Height = HEIGHT - 2,
                Hue = 999
            });

            Add(new Label(lang.CombatTrackingModalTitle, true, HUE_TITLE, WIDTH - 24, FONT, FontStyle.BlackBorder)
            {
                X = 14,
                Y = 14
            });

            Add(new Label(lang.CombatTrackingModalIntro, true, HUE_TEXT, WIDTH - 36, FONT, FontStyle.None)
            {
                X = 14,
                Y = 42
            });

            ScrollArea scroll = new ScrollArea(8, 72, WIDTH - 24, HEIGHT - 112, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            Add(scroll);

            int y = 4;
            int x = 8;
            _cbDamageBar = NewCheckbox(lang.PvM_DamageCounterOnLastTarget, p.PvM_DamageCounterOnLastTarget, x, y);
            scroll.Add(_cbDamageBar);
            y += _cbDamageBar.Height + 8;
            _cbOverhead = NewCheckbox(lang.PvM_DamageCounterAsOverhead, p.PvM_DamageCounterAsOverhead, x, y);
            scroll.Add(_cbOverhead);
            y += _cbOverhead.Height + 8;

            _cbLowHp = NewCheckbox(lang.PvM_LowHpAlertOnLastTarget, p.PvM_LowHpAlertOnLastTarget, x, y);
            scroll.Add(_cbLowHp);
            y += _cbLowHp.Height + 8;
            _cbKillCount = NewCheckbox(lang.PvM_KillCountMarkerPerSession, p.PvM_KillCountMarkerPerSession, x, y);
            scroll.Add(_cbKillCount);
            y += _cbKillCount.Height + 8;
            _cbNameProfiles = NewCheckbox(lang.PvX_NameOverheadProfilesByContext, p.PvX_NameOverheadProfilesByContext, x, y);
            scroll.Add(_cbNameProfiles);

            Wire();

            NiceButton close = new NiceButton(WIDTH - 96, HEIGHT - 36, 84, 26, ButtonAction.Activate, "Close")
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            close.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtonType.Left)
                {
                    Dispose();
                }
            };
            Add(close);
        }

        private static Checkbox NewCheckbox(string text, bool ischecked, int x, int y)
        {
            return new Checkbox(0x00D2, 0x00D3, text, FONT, HUE_TEXT)
            {
                X = x,
                Y = y,
                IsChecked = ischecked
            };
        }

        private void Wire()
        {
            void Persist()
            {
                string path = ProfileManager.ProfilePath;
                if (!string.IsNullOrEmpty(path))
                {
                    ProfileManager.CurrentProfile.Save(path, false);
                }
            }

            _cbDamageBar.ValueChanged += (_, _) =>
            {
                ProfileManager.CurrentProfile.PvM_DamageCounterOnLastTarget = _cbDamageBar.IsChecked;
                Persist();
            };
            _cbOverhead.ValueChanged += (_, _) =>
            {
                ProfileManager.CurrentProfile.PvM_DamageCounterAsOverhead = _cbOverhead.IsChecked;
                Persist();
            };
            _cbLowHp.ValueChanged += (_, _) =>
            {
                ProfileManager.CurrentProfile.PvM_LowHpAlertOnLastTarget = _cbLowHp.IsChecked;
                Persist();
            };
            _cbKillCount.ValueChanged += (_, _) =>
            {
                ProfileManager.CurrentProfile.PvM_KillCountMarkerPerSession = _cbKillCount.IsChecked;
                Persist();
            };
            _cbNameProfiles.ValueChanged += (_, _) =>
            {
                ProfileManager.CurrentProfile.PvX_NameOverheadProfilesByContext = _cbNameProfiles.IsChecked;
                Persist();
            };
        }
    }
}
