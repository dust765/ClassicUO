using System;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Options765ModalGump : Gump
    {
        private const int MODAL_WIDTH = 588;
        private const int MODAL_HEIGHT = 500;
        private const byte FONT = 0xFF;
        private const ushort HUE_TITLE = 0x0022;
        private const ushort HUE_TEXT = 0xFFFF;

        private readonly OptionsGump _owner;
        private readonly ScrollArea _scroll;

        public Options765ModalGump(OptionsGump owner, ScrollArea scroll) : base(0, 0)
        {
            _owner = owner;
            _scroll = scroll;

            X = Math.Max(0, (Client.Game.Window.ClientBounds.Width - MODAL_WIDTH) >> 1);
            Y = Math.Max(0, (Client.Game.Window.ClientBounds.Height - MODAL_HEIGHT) >> 1);
            Width = MODAL_WIDTH;
            Height = MODAL_HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;

            Dust765Language lang = Language.Instance.GetDust765;

            Add(new AlphaBlendControl(0.93f)
            {
                X = 1,
                Y = 1,
                Width = MODAL_WIDTH - 2,
                Height = MODAL_HEIGHT - 2,
                Hue = 999
            });

            Add(new Label(lang.Options765ModalTitle, true, HUE_TITLE, MODAL_WIDTH - 24, FONT, FontStyle.BlackBorder)
            {
                X = 14,
                Y = 14
            });

            Add(new Label(lang.Options765ModalIntro, true, HUE_TEXT, MODAL_WIDTH - 36, FONT, FontStyle.None)
            {
                X = 14,
                Y = 40
            });

            const int scrollX = 10;
            const int scrollY = 70;
            int scrollW = MODAL_WIDTH - 28;
            int scrollH = MODAL_HEIGHT - 118;

            _scroll.X = scrollX;
            _scroll.Y = scrollY;
            _scroll.Width = scrollW;
            _scroll.Height = scrollH;
            _scroll.ScrollbarBehaviour = ScrollbarBehaviour.ShowWhenDataExceedFromView;
            _scroll.UpdateScrollbarPosition();
            Add(_scroll);

            NiceButton close = new NiceButton(MODAL_WIDTH - 96, MODAL_HEIGHT - 36, 84, 26, ButtonAction.Activate, "Close")
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

        public override void Dispose()
        {
            if (!IsDisposed && _scroll != null)
            {
                Remove(_scroll);
                if (_owner != null && !_owner.IsDisposed)
                {
                    _owner.RestoreOptions765Scroll(_scroll);
                }
            }

            base.Dispose();
        }
    }
}
