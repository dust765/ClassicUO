using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ClassicUO.Game.UI.Controls
{
    /// <summary>
    /// A small clickable/draggable hue preview control.
    /// Used primarily by OptionsGump for color picker selection.
    /// </summary>
    public class HueDisplay : Control
    {
        private ushort hue;
        private readonly Action<ushort> hueChanged;
        private readonly bool isClickable;
        private readonly bool sendSysMessage;
        private Rectangle rect;
        private Rectangle bounds;
        private Texture2D texture;
        private Vector3 hueVector;
        private bool flash = false;
        private float flashAlpha = 1f;
        private bool rev = false;

        public ushort Hue
        {
            get { return hue; }
            set
            {
                hue = value;
                HueChanged?.Invoke(this, null);
                hueChanged?.Invoke(value);
                if (!isClickable)
                    SetTooltip(hue.ToString());
                else
                    SetTooltip($"Click to select a hue ({hue})");
            }
        }

        public void SetHueSilent(ushort value)
        {
            hue = value;
            hueVector = ShaderHueTranslator.GetHueVector(hue, true, 1);
            if (!isClickable)
                SetTooltip(hue.ToString());
            else
                SetTooltip($"Click to select a hue ({hue})");
        }

        public event EventHandler HueChanged;

        public HueDisplay(ushort hue, Action<ushort> hueChanged, bool isClickable = false, bool sendSysMessage = false)
        {
            hueVector = ShaderHueTranslator.GetHueVector(hue, true, 1);
            ref readonly var staticArt = ref Client.Game.Arts.GetArt(0x0FAB);
            texture = staticArt.Texture;
            rect = Client.Game.Arts.GetRealArtBounds(0x0FAB);
            Width = 18;
            Height = 18;
            this.bounds = staticArt.UV;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            if (!isClickable)
                SetTooltip(hue.ToString());
            else
                SetTooltip($"Click to select a hue ({hue})");
            this.hue = hue;
            this.hueChanged = hueChanged;
            this.isClickable = isClickable;
            this.sendSysMessage = sendSysMessage;
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            base.OnMouseUp(x, y, button);
            if (button == MouseButtonType.Left)
            {
                if (isClickable)
                {
                    UIManager.GetGump<ColorPickerGump>()?.Dispose();
                    UIManager.Add(new ColorPickerGump(0, 0, 100, 100, s => Hue = s));
                }
                else
                {
                    hueChanged?.Invoke(hue);
                }
                flash = true;
                if (sendSysMessage)
                    GameActions.Print($"Selected hue: {hue}");
            }
        }

        protected override void OnMouseDown(int x, int y, MouseButtonType button)
        {
            base.OnMouseDown(x, y, button);
            if (button == MouseButtonType.Left && !isClickable)
            {
                // Nothing special on mouse down for non-clickable mode
            }
        }

        protected override void OnMouseOver(int x, int y)
        {
            base.OnMouseOver(x, y);
            // Nothing special for mouse over
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);
            if (texture != null)
            {
                if (isClickable)
                    hueVector = ShaderHueTranslator.GetHueVector(hue, true, 1);
                if (flash)
                {
                    hueVector.Z = flashAlpha;
                    if (!rev) flashAlpha -= 0.1f;
                    else      flashAlpha += 0.1f;
                    if (flashAlpha <= 0) rev = true;
                    else if (flashAlpha >= 1) { rev = false; flash = false; }
                }
                batcher.Draw(
                    texture,
                    new Rectangle(x, y, Width, Height),
                    new Rectangle(bounds.X + rect.X, bounds.Y + rect.Y, rect.Width, rect.Height),
                    hueVector
                );
            }
            return true;
        }
    }
}
