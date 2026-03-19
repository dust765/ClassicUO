using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal class NearbyItems : ResizableGump
    {
        private const int X_SPACING = 1;
        private const int Y_SPACING = 1;
        private const int PADDING = 10;
        private const int Title_H = 24;
        private const int SentinelPos = -50000;

        public static NearbyItems NearbyItemGump;

        private readonly List<Item> _items;
        private readonly AlphaBlendControl _bg;
        private readonly GumpPicTiled _bgTex;
        private readonly Label _title;
        private readonly Line _line;
        private readonly ScrollArea _scroll;
        private int _innerBorder = 4;
        private int _lastGridBorderStyle = int.MinValue;
        private float _lastContainerOpacity = -1f;
        private ushort _lastAltHue = ushort.MaxValue;

        public static bool TryOpen()
        {
            if (NearbyItemGump != null)
            {
                return true;
            }

            List<Item> list = CollectNearbyItems();
            if (list.Count == 0)
            {
                return false;
            }

            UIManager.Add(new NearbyItems(list));
            return true;
        }

        private static List<Item> CollectNearbyItems()
        {
            List<Item> list = new List<Item>();
            foreach (Item i in World.Items.Values)
            {
                if (!i.OnGround)
                {
                    continue;
                }

                if (i.Distance > Constants.DRAG_ITEMS_DISTANCE)
                {
                    continue;
                }

                if (i.IsLocked && !i.ItemData.IsContainer)
                {
                    continue;
                }

                list.Add(i);
            }

            return list;
        }

        private static int GridSlotSize()
        {
            return Math.Max(1, (int)Math.Round(50.0 * ProfileManager.CurrentProfile.GridContainersScale / 100.0));
        }

        private static int ActionStripH(int g)
        {
            return Math.Max(18, Math.Min(24, g * 2 / 5));
        }

        private static int SlotRowPitch()
        {
            int g = GridSlotSize();
            return g + ActionStripH(g) + Y_SPACING;
        }

        private static int InnerBorderW()
        {
            switch ((GridContainer.BorderStyle)ProfileManager.CurrentProfile.Grid_BorderStyle)
            {
                case GridContainer.BorderStyle.Style1:
                    return 26;
                case GridContainer.BorderStyle.Style2:
                    return 12;
                case GridContainer.BorderStyle.Style3:
                    return 10;
                case GridContainer.BorderStyle.Style4:
                    return 7;
                case GridContainer.BorderStyle.Style5:
                    return 10;
                case GridContainer.BorderStyle.Style6:
                    return 4;
                case GridContainer.BorderStyle.Style7:
                    return 17;
                case GridContainer.BorderStyle.Style8:
                    return 16;
                default:
                    return 4;
            }
        }

        private static int MinWidthPx()
        {
            int g = GridSlotSize();
            int bw = InnerBorderW();
            return bw * 2 + 14 + g + X_SPACING;
        }

        private static int MinShrinkHeight()
        {
            int bw = InnerBorderW();
            return bw * 2 + PADDING + Title_H + 8 + SlotRowPitch() + 8;
        }

        private static int ContentHeightForWidth(List<Item> items, int widthPx)
        {
            int g = GridSlotSize();
            int bw = InnerBorderW();
            int inner = widthPx - bw * 2 - 14;
            int cols = Math.Max(1, (inner + X_SPACING) / (g + X_SPACING));
            int rows = Math.Max(1, (items.Count + cols - 1) / cols);
            return bw * 2 + PADDING + Title_H + 8 + rows * SlotRowPitch() - Y_SPACING + 8;
        }

        private static int InitialWidth(List<Item> items)
        {
            int g = GridSlotSize();
            int bw = InnerBorderW();
            int min6 = bw * 2 + 14 + 6 * (g + X_SPACING) - X_SPACING;
            int saved = ProfileManager.CurrentProfile.NearbyItemsGumpSize.X;
            return Math.Max(Math.Max(640, min6), saved >= MinWidthPx() ? saved : 640);
        }

        private static int InitialHeight(List<Item> items, int widthPx)
        {
            int ch = ContentHeightForWidth(items, widthPx);
            int saved = ProfileManager.CurrentProfile.NearbyItemsGumpSize.Y;
            int target = saved >= MinShrinkHeight() ? saved : 440;
            return Math.Max(MinShrinkHeight(), Math.Min(ch, target));
        }

        private NearbyItems(List<Item> items) : base(
            InitialWidth(items),
            InitialHeight(items, InitialWidth(items)),
            MinWidthPx(),
            MinShrinkHeight(),
            0,
            0)
        {
            _items = items;
            NearbyItemGump = this;
            AnchorType = ANCHOR_TYPE.DISABLED;
            CanMove = true;
            _prevCanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            ShowBorder = true;
            _prevBorder = true;
            _prevCloseWithRightClick = true;
            CanBeLocked = false;

            Insert(0, _bg = new AlphaBlendControl());
            Insert(1, _bgTex = new GumpPicTiled(0));
            Add(_title = new Label("Items nearby", true, 0x0481, 0, 1));
            Add(_line = new Line(0, 0, 100, 1, 0xFF7a6a5a));
            _scroll = new ScrollArea(0, 0, 100, 100, true);
            _scroll.CanMove = false;
            Add(_scroll);

            ApplySavedPosition();
            SyncPanelChrome(true);
        }

        private void ApplySavedPosition()
        {
            Point p = ProfileManager.CurrentProfile.NearbyItemsGumpPosition;
            if (p.X <= SentinelPos || p.Y <= SentinelPos)
            {
                X = Mouse.Position.X - (Width >> 1);
                Y = Mouse.Position.Y - (Height >> 1);
            }
            else
            {
                X = p.X;
                Y = p.Y;
            }
        }

        private void ApplyGridPanelBorderStyle()
        {
            int graphic = 0;
            int borderSize = 0;
            switch ((GridContainer.BorderStyle)ProfileManager.CurrentProfile.Grid_BorderStyle)
            {
                case GridContainer.BorderStyle.Style1:
                    graphic = 3500;
                    borderSize = 26;
                    break;
                case GridContainer.BorderStyle.Style2:
                    graphic = 5054;
                    borderSize = 12;
                    break;
                case GridContainer.BorderStyle.Style3:
                    graphic = 5120;
                    borderSize = 10;
                    break;
                case GridContainer.BorderStyle.Style4:
                    graphic = 9200;
                    borderSize = 7;
                    break;
                case GridContainer.BorderStyle.Style5:
                    graphic = 9270;
                    borderSize = 10;
                    break;
                case GridContainer.BorderStyle.Style6:
                    graphic = 9300;
                    borderSize = 4;
                    break;
                case GridContainer.BorderStyle.Style7:
                    graphic = 9260;
                    borderSize = 17;
                    break;
                case GridContainer.BorderStyle.Style8:
                    if (Client.Game.Gumps.GetGump(40303).Texture != null)
                    {
                        graphic = 40303;
                    }
                    else
                    {
                        graphic = 83;
                    }

                    borderSize = 16;
                    break;
                default:
                case GridContainer.BorderStyle.Default:
                    BorderControl.DefaultGraphics();
                    _bgTex.IsVisible = false;
                    _bg.IsVisible = true;
                    _innerBorder = 4;
                    BorderControl.IsVisible = !ProfileManager.CurrentProfile.Grid_HideBorder;
                    return;
            }

            BorderControl.T_Left = (ushort)graphic;
            BorderControl.H_Border = (ushort)(graphic + 1);
            BorderControl.T_Right = (ushort)(graphic + 2);
            BorderControl.V_Border = (ushort)(graphic + 3);
            _bgTex.Graphic = (ushort)(graphic + 4);
            _bgTex.IsVisible = true;
            _bg.IsVisible = false;
            BorderControl.V_Right_Border = (ushort)(graphic + 5);
            BorderControl.B_Left = (ushort)(graphic + 6);
            BorderControl.H_Bottom_Border = (ushort)(graphic + 7);
            BorderControl.B_Right = (ushort)(graphic + 8);
            BorderControl.BorderSize = borderSize;
            _innerBorder = borderSize;
            BorderControl.IsVisible = !ProfileManager.CurrentProfile.Grid_HideBorder;
        }

        private void SyncPanelChrome(bool reflowGrid)
        {
            float op = (float)ProfileManager.CurrentProfile.ContainerOpacity / 100f;
            ushort hue = ProfileManager.CurrentProfile.AltGridContainerBackgroundHue;
            _bg.Alpha = op;
            _bg.Hue = hue;
            ApplyGridPanelBorderStyle();
            _bgTex.Alpha = op;
            _bgTex.Hue = hue;
            BorderControl.Hue = hue;
            BorderControl.Alpha = op;

            int bw = _innerBorder;
            int headerH = PADDING + Title_H + 8;
            _bg.X = bw;
            _bg.Y = bw;
            _bg.Width = Width - bw * 2;
            _bg.Height = Height - bw * 2;
            _bgTex.X = bw;
            _bgTex.Y = bw;
            _bgTex.Width = _bg.Width;
            _bgTex.Height = _bg.Height;
            _title.X = bw + 4;
            _title.Y = bw + 4;
            _line.X = bw;
            _line.Y = bw + PADDING + Title_H + 2;
            _line.Width = Math.Max(20, Width - bw * 2);
            _scroll.X = bw;
            _scroll.Y = bw + headerH;
            _scroll.Width = Math.Max(MinWidthPx() - bw * 2, Width - bw * 2);
            _scroll.Height = Math.Max(80, Height - bw * 2 - headerH);
            _scroll.UpdateScrollbarPosition();
            if (reflowGrid)
            {
                ReflowGrid();
            }
        }

        private void ReflowGrid()
        {
            _scroll.Clear();
            int g = GridSlotSize();
            int sh = ActionStripH(g);
            int innerW = _scroll.Width - 14;
            int cols = Math.Max(1, (innerW + X_SPACING) / (g + X_SPACING));
            int pitch = g + sh + Y_SPACING;
            for (int i = 0; i < _items.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                NearbyGridSlot slot = new NearbyGridSlot(_items[i], g, sh);
                slot.X = col * (g + X_SPACING);
                slot.Y = row * pitch;
                _scroll.Add(slot);
            }
        }

        public override void Update()
        {
            Profile p = ProfileManager.CurrentProfile;
            if (_lastGridBorderStyle != p.Grid_BorderStyle
                || Math.Abs(_lastContainerOpacity - p.ContainerOpacity) > 0.01f
                || _lastAltHue != p.AltGridContainerBackgroundHue)
            {
                _lastGridBorderStyle = p.Grid_BorderStyle;
                _lastContainerOpacity = p.ContainerOpacity;
                _lastAltHue = p.AltGridContainerBackgroundHue;
                SyncPanelChrome(true);
            }

            base.Update();
        }

        public override void OnResize()
        {
            base.OnResize();
            if (_scroll != null)
            {
                SyncPanelChrome(true);
                ProfileManager.CurrentProfile.NearbyItemsGumpSize = new Point(Width, Height);
            }
        }

        protected override void OnDragEnd(int x, int y)
        {
            ProfileManager.CurrentProfile.NearbyItemsGumpPosition = Location;
            base.OnDragEnd(x, y);
        }

        public override void Dispose()
        {
            if (!IsDisposed && NearbyItemGump == this)
            {
                ProfileManager.CurrentProfile.NearbyItemsGumpSize = new Point(Width, Height);
                ProfileManager.CurrentProfile.NearbyItemsGumpPosition = Location;
            }

            NearbyItemGump = null;
            base.Dispose();
        }
    }

    internal class NearbyGridSlot : Control
    {
        private readonly Item _item;
        private readonly int _g;
        private readonly int _stripH;
        private readonly HitBox _itemHit;
        private readonly Rectangle _rect;
        private readonly Rectangle _bounds;
        private readonly Texture2D _texture;

        public NearbyGridSlot(Item item, int gridSize, int stripH)
        {
            _item = item;
            _g = gridSize;
            _stripH = stripH;
            Width = _g;
            Height = _g + _stripH;

            AlphaBlendControl slotFill = new AlphaBlendControl(0.25f)
            {
                X = 0,
                Y = 0,
                Width = _g,
                Height = _g
            };
            Add(slotFill);

            AlphaBlendControl stripFill = new AlphaBlendControl(0.25f)
            {
                X = 0,
                Y = _g,
                Width = _g,
                Height = _stripH
            };
            Add(stripFill);

            Add(_itemHit = new HitBox(0, 0, _g, _g, null, 0f));
            _itemHit.SetTooltip(item);

            ref readonly SpriteInfo text = ref Client.Game.Arts.GetArt((uint)item.DisplayedGraphic);
            _texture = text.Texture;
            _bounds = text.UV;
            _rect = Client.Game.Arts.GetRealArtBounds((uint)item.DisplayedGraphic);

            int itemAmt = item.ItemData.IsStackable ? item.Amount : 1;
            if (itemAmt > 1)
            {
                Label count = new Label(itemAmt.ToString(), true, 0x0481, align: TEXT_ALIGN_TYPE.TS_LEFT)
                {
                    X = 1,
                    Y = _g - 14
                };
                Add(count);
            }

            int half = _g >> 1;
            HitBox loot = new HitBox(0, _g, half, _stripH);
            loot.Add(new UOLabel("Loot", 1, UOLabelHue.Text, TEXT_ALIGN_TYPE.TS_CENTER, half));
            loot.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtonType.Left)
                {
                    GameActions.GrabItem(_item, _item.Amount);
                    NearbyItems.NearbyItemGump?.Dispose();
                }
            };
            Add(loot);

            HitBox use = new HitBox(half, _g, half, _stripH);
            UOLabel ul;
            use.Add(ul = new UOLabel("Use", 1, UOLabelHue.Text, TEXT_ALIGN_TYPE.TS_CENTER, half));
            ul.Y = (_stripH - ul.Height) >> 1;
            use.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtonType.Left)
                {
                    GameActions.DoubleClick(_item);
                }
            };
            Add(use);

            Add(new Line(0, _g, _g, 1, 0xFF6a6a6a));
            Add(new Line(half, _g, 1, _stripH, 0xFF6a6a6a));
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);

            Vector3 borderVec = ShaderHueTranslator.GetHueVector(
                ProfileManager.CurrentProfile.GridBorderHue,
                false,
                (float)ProfileManager.CurrentProfile.GridBorderAlpha / 100);
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(Color.White), x, y, _g, _g, borderVec);
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(Color.White), x, y + _g, _g, _stripH, borderVec);

            if (_itemHit.MouseIsOver && _item != null)
            {
                Vector3 h = ShaderHueTranslator.GetHueVector(0, false, 1f);
                h.Z = 0.3f;
                batcher.Draw(
                    SolidColorTextureCache.GetTexture(Color.White),
                    new Rectangle(x + 1, y, _g - 1, _g),
                    h);
            }

            if (_item != null && _texture != null && _rect.Width > 0 && _rect.Height > 0)
            {
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(_item.Hue, _item.ItemData.IsPartialHue, 1f);
                Point originalSize = new Point(_g, _g);
                Point point = new Point();
                float scale = ProfileManager.CurrentProfile.GridContainersScale / 100f;

                if (_rect.Width < _g)
                {
                    if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                    {
                        originalSize.X = (ushort)(_rect.Width * scale);
                    }
                    else
                    {
                        originalSize.X = _rect.Width;
                    }

                    point.X = (_g >> 1) - (originalSize.X >> 1);
                }
                else if (_rect.Width > _g)
                {
                    if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                    {
                        originalSize.X = (ushort)(_g * scale);
                    }
                    else
                    {
                        originalSize.X = _g;
                    }

                    point.X = (_g >> 1) - (originalSize.X >> 1);
                }

                if (_rect.Height < _g)
                {
                    if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                    {
                        originalSize.Y = (ushort)(_rect.Height * scale);
                    }
                    else
                    {
                        originalSize.Y = _rect.Height;
                    }

                    point.Y = (_g >> 1) - (originalSize.Y >> 1);
                }
                else if (_rect.Height > _g)
                {
                    if (ProfileManager.CurrentProfile.GridContainerScaleItems)
                    {
                        originalSize.Y = (ushort)(_g * scale);
                    }
                    else
                    {
                        originalSize.Y = _g;
                    }

                    point.Y = (_g >> 1) - (originalSize.Y >> 1);
                }

                if (ProfileManager.CurrentProfile.EnlargeJewelryOnPaperdoll
                    && (_item.ItemData.Layer == (byte)Layer.Ring || _item.ItemData.Layer == (byte)Layer.Bracelet))
                {
                    bool isBracelet = _item.ItemData.Layer == (byte)Layer.Bracelet;
                    float jMult = isBracelet ? 1.55f : 1.8f;
                    int jMin = isBracelet ? 8 : 9;
                    int pad = 2;
                    int jw = Math.Min(_g - pad, Math.Max(jMin, (int)Math.Ceiling(_rect.Width * jMult)));
                    int jh = Math.Min(_g - pad, Math.Max(jMin, (int)Math.Ceiling(_rect.Height * jMult)));
                    originalSize.X = jw;
                    originalSize.Y = jh;
                    point.X = (_g >> 1) - (jw >> 1);
                    point.Y = (_g >> 1) - (jh >> 1);
                }

                batcher.Draw(
                    _texture,
                    new Rectangle(x + point.X, y + point.Y, originalSize.X, originalSize.Y),
                    new Rectangle(_bounds.X + _rect.X, _bounds.Y + _rect.Y, _rect.Width, _rect.Height),
                    hueVector);
            }

            return true;
        }
    }
}
