#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.

#endregion

using System;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal partial class CounterBarGump
    {
        private class CounterItem : Control
        {
            private const int UPDATE_INTERVAL = 100;
            private const int DRAG_OFFSET = 22;
            private const float HIGHLIGHT_AMOUNT_CHANGED_DURATION = 5000f;
            private const int HIGHLIGHT_AMOUNT_CHANGED_UP_HUE = 1165;
            private const int HIGHLIGHT_AMOUNT_CHANGED_DOWN_HUE = 1166;

            private int _amount;
            private uint _lastChangeTime;
            private readonly ImageWithText _image;
            private uint _time;
            private AlphaBlendControl _background;
            private readonly CounterBarGump _gump;

            public CounterItem(CounterBarGump gump, ushort graphic, ushort? hue, int compareTo)
            {
                _gump = gump;
                CompareTo = compareTo;

                AcceptMouseInput = true;
                WantUpdateSize = false;
                CanMove = true;
                CanCloseWithRightClick = false;

                Add(_background = new AlphaBlendControl(0.0f) { X = 0, Y = 0 });
                Add(_image = new ImageWithText());

                SetGraphic(graphic, hue);
            }

            public ushort Graphic { get; private set; }

            public ushort? Hue { get; private set; }

            public int CompareTo { get; private set; }

            public void SetGraphic(ushort graphic, ushort? hue)
            {
                _image.ChangeGraphic(graphic, hue ?? 0);

                Graphic = graphic;
                Hue = hue;

                ConfigureContextMenu();
            }

            internal void ConfigureContextMenu()
            {
                ContextMenu = new ContextMenuControl();
                if (Graphic != 0)
                {
                    ContextMenu.Add(ResGumps.UseObject, Use);
                    ContextMenu.Add(ResGumps.CounterCompareTo, CompareToSelected);
                    ContextMenu.Add(Hue != null ? ResGumps.CounterIgnoreHueOff : ResGumps.CounterIgnoreHueOn, ToggleIgnoreHue);
                }
                else
                {
                    _gump.BuildCounterContextMenu(ContextMenu);
                }
            }

            private void ToggleIgnoreHue()
            {
                if (Hue != null)
                {
                    Hue = null;
                }
                else
                {
                    Hue = 0;
                }

                SetGraphic(Graphic, Hue);
            }

            private void CompareToSelected()
            {
                UIManager.Add(
                    new EntryDialog(
                        250,
                        160,
                        string.Format("{0}\n{1}", ResGumps.CounterCompareToDialogText1, ResGumps.CounterCompareToDialogText2),
                        CompareToDialogClosed,
                        _amount.ToString()
                    )
                );
            }

            private void CompareToDialogClosed(string newValue)
            {
                if (string.IsNullOrEmpty(newValue))
                {
                    CompareTo = 0;
                }
                else if (int.TryParse(newValue, out int parsedValue))
                {
                    CompareTo = parsedValue;
                }
                else
                {
                    UIManager.Add(
                        new EntryDialog(
                            250,
                            180,
                            string.Format(
                                "{0}\n{1}\n\n{2}",
                                ResGumps.CounterCompareToDialogText1,
                                ResGumps.CounterCompareToDialogText2,
                                ResGumps.CounterCompareToDialogInvalid
                            ),
                            CompareToDialogClosed,
                            newValue
                        )
                    );
                }
            }

            public void RemoveItem()
            {
                CounterBarGump bar = RootParent as CounterBarGump;
                bar?._dataBox.Remove(this);
                Dispose();
                bar?.SetupLayout();
            }

            public void Use()
            {
                if (Graphic == 0)
                {
                    return;
                }

                Item backpack = World.Player.FindItemByLayer(Layer.Backpack);

                Item item = backpack?.FindItem(Graphic, Hue ?? 0xFFFF);

                if (item != null)
                {
                    GameActions.DoubleClick(item);
                }
            }

            protected override void OnMouseOver(int x, int y)
            {
                base.OnMouseOver(x, y);
                Item backpack = World.Player.FindItemByLayer(Layer.Backpack);
                if (backpack == null)
                {
                    return;
                }

                if (Hue == null)
                {
                    if (backpack.FindItem(Graphic) is { } item)
                    {
                        SetTooltip(item);
                    }
                }
                else if (backpack.FindItem(Graphic, Hue.Value) is { } item2)
                {
                    SetTooltip(item2);
                }
            }

            protected override void OnMouseExit(int x, int y)
            {
                base.OnMouseExit(x, y);
                ClearTooltip();
            }

            protected override void OnDragBegin(int x, int y)
            {
                if (_gump.ReadOnly || Graphic == 0)
                {
                    return;
                }

                DraggableGump gump = new DraggableGump
                {
                    X = Mouse.LClickPosition.X - DRAG_OFFSET,
                    Y = Mouse.LClickPosition.Y - DRAG_OFFSET
                };
                gump.Add(this);
                X = 0;
                Y = 0;

                UIManager.Add(gump);

                UIManager.AttemptDragControl(gump, true);

                _gump.SetupLayout();
            }

            protected override void OnDragEnd(int x, int y)
            {
                if (_gump.ReadOnly)
                {
                    return;
                }

                Control oldParent = Parent;
                if (oldParent is DraggableGump)
                {
                    FinalizeDragDrop(oldParent);
                }

                base.OnDragEnd(x, y);
            }

            private void FinalizeDragDrop(Control oldParent)
            {
                CounterItem overItem = null;
                CounterBarGump overBar = null;

                for (Control c = UIManager.MouseOverControl; c != null; c = c.Parent)
                {
                    if (c is CounterItem ci)
                    {
                        overItem = ci;
                    }

                    if (c is CounterBarGump cb)
                    {
                        overBar = cb;
                        break;
                    }
                }

                if (overBar == _gump)
                {
                    if (overItem != null && overItem != this)
                    {
                        int idx = _gump._dataBox.Children.IndexOf(overItem);
                        if (idx >= 0)
                        {
                            _gump._dataBox.Insert(idx, this);
                        }
                        else
                        {
                            _gump._dataBox.Add(this);
                        }
                    }
                    else
                    {
                        _gump._dataBox.Add(this);
                    }

                    _gump.SetupLayout();
                }
                else if (overBar != null)
                {
                    if (overItem != null)
                    {
                        int idx = overBar._dataBox.Children.IndexOf(overItem);
                        if (idx >= 0)
                        {
                            overBar._dataBox.Insert(idx, this);
                        }
                        else
                        {
                            overBar._dataBox.Add(this);
                        }
                    }
                    else
                    {
                        overBar._dataBox.Add(this);
                    }

                    overBar.SetupLayout();
                }
                else
                {
                    _gump._dataBox.Add(this);
                    _gump.SetupLayout();
                }

                oldParent.Dispose();
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                switch (button)
                {
                    case MouseButtonType.Left:
                        if (Client.Game.GameCursor.ItemHold.Enabled)
                        {
                            if (!_gump.ReadOnly)
                            {
                                SetGraphic(
                                    Client.Game.GameCursor.ItemHold.Graphic,
                                    Client.Game.GameCursor.ItemHold.Hue
                                );
                            }

                            GameActions.DropItem(
                                Client.Game.GameCursor.ItemHold.Serial,
                                Client.Game.GameCursor.ItemHold.X,
                                Client.Game.GameCursor.ItemHold.Y,
                                0,
                                Client.Game.GameCursor.ItemHold.Container
                            );
                        }
                        else if (ProfileManager.CurrentProfile.CastSpellsByOneClick)
                        {
                            Use();
                        }

                        break;

                    case MouseButtonType.Right when Keyboard.Alt:
                        if (!_gump.ReadOnly)
                        {
                            RemoveItem();
                        }

                        break;

                    case MouseButtonType.Right:
                        base.OnMouseUp(x, y, button);
                        break;

                    default:
                        if (Graphic != 0)
                        {
                            base.OnMouseUp(x, y, button);
                        }

                        break;
                }
            }

            protected override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Left && !ProfileManager.CurrentProfile.CastSpellsByOneClick)
                {
                    Use();
                }

                return true;
            }

            private int CalculateDisplayAmount()
            {
                return _amount - CompareTo;
            }

            public override void Update()
            {
                base.Update();

                if (!IsDisposed)
                {
                    _image.Width = Width;
                    _image.Height = Height;

                    _background.Width = Width;
                    _background.Height = Height;
                }

                if (Parent == null || !Parent.IsEnabled || _time >= Time.Ticks)
                {
                    return;
                }

                _time = Time.Ticks + UPDATE_INTERVAL;

                if (Graphic == 0)
                {
                    _image.SetAmount(string.Empty);
                    return;
                }

                int previousAmount = _amount;
                _amount = GetTotalAmountOfItem(Graphic, Hue);

                UpdateOnChangeAnimation(previousAmount);

                _image.SetAmount(CalculateDisplayAmountText());
            }

            private static int GetTotalAmountOfItem(ushort graphic, ushort? hue)
            {
                int amount = 0;

                for (Item item = (Item)World.Player.Items; item != null; item = (Item)item.Next)
                {
                    if (
                        item.ItemData.IsContainer
                        && !item.IsEmpty
                        && item.Layer >= Layer.OneHanded
                        && item.Layer <= Layer.Legs
                    )
                    {
                        AccumulateAmount(item, graphic, hue, ref amount);
                    }
                }

                return amount;
            }

            private static void AccumulateAmount(Item parent, ushort graphic, ushort? hue, ref int amount)
            {
                if (parent == null)
                {
                    return;
                }

                for (LinkedObject i = parent.Items; i != null; i = i.Next)
                {
                    Item item = (Item)i;

                    AccumulateAmount(item, graphic, hue, ref amount);

                    if (item.Graphic == graphic && item.Exists)
                    {
                        if (hue == null || item.Hue == hue.Value)
                        {
                            amount += item.Amount;
                        }
                    }
                }
            }

            private void UpdateOnChangeAnimation(int previousAmount)
            {
                if (ProfileManager.CurrentProfile.CounterBarHighlightOnUse)
                {
                    if (_amount > previousAmount)
                    {
                        _background.Hue = HIGHLIGHT_AMOUNT_CHANGED_UP_HUE;
                        _lastChangeTime = Time.Ticks;
                    }
                    else if (_amount < previousAmount)
                    {
                        _background.Hue = HIGHLIGHT_AMOUNT_CHANGED_DOWN_HUE;
                        _lastChangeTime = Time.Ticks;
                    }

                    _background.Alpha = Math.Max(0, 1 - (Time.Ticks - _lastChangeTime) / HIGHLIGHT_AMOUNT_CHANGED_DURATION);
                }
            }

            private string CalculateDisplayAmountText()
            {
                int displayAmount = CalculateDisplayAmount();
                string prefix = CalculateAmountPrefix(displayAmount);

                if (string.IsNullOrEmpty(prefix) && displayAmount == 1)
                {
                    return string.Empty;
                }

                string displayText = prefix + displayAmount.ToString();
                if (ProfileManager.CurrentProfile.CounterBarDisplayAbbreviatedAmount)
                {
                    if (Math.Abs(displayAmount) >= ProfileManager.CurrentProfile.CounterBarAbbreviatedAmount)
                    {
                        displayText = prefix + StringHelper.IntToAbbreviatedString(displayAmount);
                    }
                }

                return displayText;
            }

            private string CalculateAmountPrefix(int displayAmount)
            {
                if (CompareTo == 0)
                {
                    return "";
                }

                if (displayAmount == 0)
                {
                    return "=";
                }

                if (displayAmount > 0)
                {
                    return "+";
                }

                return "";
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                base.Draw(batcher, x, y);

                Texture2D color = SolidColorTextureCache.GetTexture(
                    MouseIsOver
                        ? Color.Yellow
                        : ProfileManager.CurrentProfile.CounterBarHighlightOnAmount
                        && CalculateDisplayAmount() < ProfileManager.CurrentProfile.CounterBarHighlightAmount
                        && Graphic != 0
                            ? Color.Red
                            : Color.Gray
                );

                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);

                batcher.DrawRectangle(color, x, y, Width, Height, hueVector);

                return true;
            }

            private class ImageWithText : Control
            {
                private readonly Label _label;
                private ushort _graphic;
                private ushort _hue;
                private bool _partial;

                public ImageWithText()
                {
                    CanMove = true;
                    WantUpdateSize = true;
                    AcceptMouseInput = false;

                    _label = new Label("", true, 0x35, 0, 1, FontStyle.BlackBorder)
                    {
                        X = 2,
                        Y = Height - 15
                    };

                    Add(_label);
                }

                public void ChangeGraphic(ushort graphic, ushort hue)
                {
                    if (graphic != 0)
                    {
                        _graphic = graphic;
                        _hue = hue;
                        _partial = UOFileManager.Current.TileData.StaticData[graphic].IsPartialHue;
                    }
                    else
                    {
                        _graphic = 0;
                    }
                }

                public override void Update()
                {
                    base.Update();

                    if (Parent != null)
                    {
                        Width = Parent.Width;
                        Height = Parent.Height;
                        _label.Y = Parent.Height - 15;
                    }
                }

                public override bool Draw(UltimaBatcher2D batcher, int x, int y)
                {
                    if (_graphic != 0)
                    {
                        ref readonly var artInfo = ref Client.Game.Arts.GetArt(_graphic);
                        Rectangle rect = Client.Game.Arts.GetRealArtBounds(_graphic);

                        Vector3 hueVector = ShaderHueTranslator.GetHueVector(_hue, _partial, 1f);

                        Point originalSize = new Point(Width, Height);
                        Point point = new Point();

                        if (rect.Width < Width)
                        {
                            originalSize.X = rect.Width;
                            point.X = (Width >> 1) - (originalSize.X >> 1);
                        }

                        if (rect.Height < Height)
                        {
                            originalSize.Y = rect.Height;
                            point.Y = (Height >> 1) - (originalSize.Y >> 1);
                        }

                        Texture2D texture = artInfo.Texture;
                        Rectangle sourceRectangle = artInfo.UV;

                        batcher.Draw(
                            texture,
                            new Rectangle(x + point.X, y + point.Y, originalSize.X, originalSize.Y),
                            new Rectangle(
                                sourceRectangle.X + rect.X,
                                sourceRectangle.Y + rect.Y,
                                rect.Width,
                                rect.Height
                            ),
                            hueVector
                        );
                    }

                    return base.Draw(batcher, x, y);
                }

                public void SetAmount(string amount)
                {
                    _label.Text = amount;
                }
            }
        }
    }
}
