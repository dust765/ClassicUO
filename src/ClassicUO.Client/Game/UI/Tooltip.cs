#region license

// SPDX-License-Identifier: BSD-2-Clause

#endregion

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using World = ClassicUO.Game.World;
using ClassicUO.IO;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using FontStyle = ClassicUO.Game.FontStyle;

namespace ClassicUO.Game.UI
{
    internal class Tooltip
    {
        private uint _hash;
        private uint _lastHoverTime;
        private int _maxWidth;
        private RenderedText _renderedText;
        private string _textHTML;

        public static bool IsEnabled;

        public static int X, Y;
        public static int Width, Height;

        public string Text { get; protected set; }

        public bool IsEmpty => Text == null;

        public uint Serial { get; private set; }

        public bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (ProfileManager.CurrentProfile != null && !ProfileManager.CurrentProfile.UseTooltip)
                return false;

            if (SerialHelper.IsValid(Serial) && World.OPL.TryGetRevision(Serial, out uint revision) && _hash != revision)
            {
                _hash = revision;
                Text = ReadProperties(Serial, out _textHTML);
            }

            if (string.IsNullOrEmpty(Text))
                return false;

            if (_lastHoverTime > Time.Ticks)
                return false;

            byte font = 1;
            float alpha = 0.7f;
            ushort hue = 0xFFFF;
            float zoom = 1;

            if (ProfileManager.CurrentProfile != null)
            {
                font = ProfileManager.CurrentProfile.TooltipFont;
                alpha = ProfileManager.CurrentProfile.TooltipBackgroundOpacity / 100f;

                if (float.IsNaN(alpha))
                    alpha = 0f;

                hue = ProfileManager.CurrentProfile.TooltipTextHue;
                zoom = ProfileManager.CurrentProfile.TooltipDisplayZoom / 100f;
            }

            UOFileManager.Current.Fonts.SetUseHTML(true);
            UOFileManager.Current.Fonts.RecalculateWidthByInfo = true;

            if (_renderedText == null)
            {
                _renderedText = RenderedText.Create(
                    null,
                    hue,
                    font,
                    isunicode: true,
                    style: FontStyle.BlackBorder,
                    align: TEXT_ALIGN_TYPE.TS_CENTER,
                    maxWidth: 0,
                    cell: 5,
                    isHTML: true,
                    recalculateWidthByInfo: true
                );
            }

            if (_renderedText.Text != Text)
            {
                if (_maxWidth == 0)
                {
                    int width = UOFileManager.Current.Fonts.GetWidthUnicode(font, Text);

                    if (width > 600)
                        width = 600;

                    width = UOFileManager.Current.Fonts.GetWidthExUnicode(
                        font,
                        Text,
                        width,
                        TEXT_ALIGN_TYPE.TS_CENTER,
                        (ushort)FontStyle.BlackBorder
                    );

                    if (width > 600)
                        width = 600;

                    _renderedText.MaxWidth = width;
                }
                else
                    _renderedText.MaxWidth = _maxWidth;

                _renderedText.Font = font;
                _renderedText.Hue = hue;
                _renderedText.Text = _textHTML;
            }

            UOFileManager.Current.Fonts.RecalculateWidthByInfo = false;
            UOFileManager.Current.Fonts.SetUseHTML(false);

            if (!_renderedText.HasContent)
                return false;

            int z_width = _renderedText.Width + 8;
            int z_height = _renderedText.Height + 8;

            int clientW = Client.Game.Window.ClientBounds.Width;
            int clientH = Client.Game.Window.ClientBounds.Height;

            if (x < 0)
                x = 0;
            else if (x > clientW - z_width)
                x = clientW - z_width;

            if (y < 0)
                y = 0;
            else if (y > clientH - z_height)
                y = clientH - z_height;

            Vector3 hue_vec = ShaderHueTranslator.GetHueVector(0, false, alpha);

            batcher.Draw(
                SolidColorTextureCache.GetTexture(Color.Black),
                new Rectangle(x - 4, y - 2, (int)(z_width * zoom), (int)(z_height * zoom)),
                hue_vec,
                0f
            );

            batcher.DrawRectangle(
                SolidColorTextureCache.GetTexture(Color.Gray),
                x - 4,
                y - 2,
                (int)(z_width * zoom),
                (int)(z_height * zoom),
                hue_vec,
                0f
            );

            _renderedText.Draw(batcher, x + 3, y + 3, 1f, 0, 0f, zoom);

            X = x - 4;
            Y = y - 2;
            Width = (int)(z_width * zoom) + 1;
            Height = (int)(z_height * zoom) + 1;
            IsEnabled = true;

            return true;
        }

        public void Clear()
        {
            Serial = 0;
            _hash = 0;
            _textHTML = Text = null;
            _maxWidth = 0;
            IsEnabled = false;
        }

        public void SetGameObject(uint serial)
        {
            if (Serial == 0 || serial != Serial)
            {
                uint revision2 = 0;

                if (Serial == 0
                    || Serial != serial
                    || (World.OPL.TryGetRevision(Serial, out uint revision)
                        && World.OPL.TryGetRevision(serial, out revision2)
                        && revision != revision2))
                {
                    _maxWidth = 0;
                    Serial = serial;
                    _hash = revision2;
                    Text = ReadProperties(serial, out _textHTML);

                    _lastHoverTime = (uint)(Time.Ticks + (ProfileManager.CurrentProfile != null ? ProfileManager.CurrentProfile.TooltipDelayBeforeDisplay : 250));
                }
            }
        }

        private string ReadProperties(uint serial, out string htmltext)
        {
            bool hasStartColor = false;

            string result = null;
            htmltext = string.Empty;

            if (SerialHelper.IsValid(serial) && World.OPL.TryGetNameAndData(serial, out string name, out string data))
            {
                ValueStringBuilder sbHTML = new ValueStringBuilder();
                {
                    ValueStringBuilder sb = new ValueStringBuilder();
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (SerialHelper.IsItem(serial))
                            {
                                sbHTML.Append("<basefont color=\"yellow\">");
                                hasStartColor = true;
                            }
                            else
                            {
                                Mobile mob = World.Mobiles.Get(serial);

                                if (mob != null)
                                {
                                    sbHTML.Append(Notoriety.GetHTMLHue(mob.NotorietyFlag));
                                    hasStartColor = true;
                                }
                            }

                            sb.Append(name);
                            sbHTML.Append(name);

                            if (hasStartColor)
                                sbHTML.Append("<basefont color=\"#FFFFFFFF\">");
                        }

                        if (!string.IsNullOrEmpty(data))
                        {
                            sb.Append('\n');
                            sb.Append(data);
                            sbHTML.Append('\n');
                            sbHTML.Append(data);
                        }

                        htmltext = sbHTML.ToString();
                        result = sb.ToString();

                        sb.Dispose();
                        sbHTML.Dispose();
                    }
                }
            }

            return string.IsNullOrEmpty(result) ? null : result;
        }

        public void SetText(string text, int maxWidth = 0)
        {
            if (ProfileManager.CurrentProfile != null && !ProfileManager.CurrentProfile.UseTooltip)
                return;

            _maxWidth = maxWidth;
            Serial = 0;
            Text = _textHTML = text;

            _lastHoverTime = (uint)(Time.Ticks + (ProfileManager.CurrentProfile != null ? ProfileManager.CurrentProfile.TooltipDelayBeforeDisplay : 250));
        }
    }
}
