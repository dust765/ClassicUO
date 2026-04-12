#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.Assets;
using ClassicUO.Configuration;
using FontStyle = ClassicUO.Game.FontStyle;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ClassicUO.Game.UI.Gumps
{
    internal class NameOverheadGump : Gump
    {
        private AlphaBlendControl _background;
        private Point _lockedPosition,
            _lastLeftMousePositionDown;
        private bool _positionLocked,
            _leftMouseIsDown,
            _isLastTarget,
            _needsNameUpdate;
        private bool _hasBarsBelow;
        private bool _otherPlayerSingleHpBar;
        private bool _lastNamePlateHealthBar;
        private bool _lastNamePlateHealthBarMatchStrip;
        private bool _lastNamePlateFullPlateWidthScalesWithHp;
        private bool _lastShowHpLineInNoh;
        private bool _lastNamePlateUseCustomChrome;
        private UOLabel _text;
        private Texture2D _borderColor = SolidColorTextureCache.GetTexture(Color.Black);
        private Vector2 _textDrawOffset = Vector2.Zero;
        private static int currentHeight = 18;

        public static int CurrentHeight
        {
            get
            {
                if (NameOverHeadManager.IsShowing)
                {
                    return currentHeight;
                }

                return 0;
            }
            private set
            {
                currentHeight = value;
            }
        }

        public static int GetFloatingTextVerticalReserve(uint serial)
        {
            if (!NameOverHeadManager.IsShowing)
            {
                return 0;
            }

            NameOverheadGump g = UIManager.GetGump<NameOverheadGump>(serial);

            if (g == null || g.IsDisposed || !g._hasBarsBelow)
            {
                return 0;
            }

            if (g.NamePlateSuppressedByProfile())
            {
                return 0;
            }

            int h = g.Height;
            int nameH = g._text != null ? g._text.Height : 18;
            if (g._otherPlayerSingleHpBar)
            {
                int pull = ProfileManager.CurrentProfile?.NamePlateHealthBarMatchStrip == true
                    ? Math.Max(36, (g._text?.Height ?? 18) + 24)
                    : 36;
                int target = h - pull;
                int floor = Math.Max(10, nameH - 6);
                return Math.Max(floor, target);
            }

            int pullCloser = 28;
            int t2 = h - pullCloser;
            int floor2 = Math.Max(12, nameH);
            return Math.Max(floor2, t2);
        }

        private bool NamePlateSuppressedByProfile()
        {
            if (!SerialHelper.IsMobile(LocalSerial))
            {
                return false;
            }

            Mobile m = World.Mobiles.Get(LocalSerial);

            if (m == null)
            {
                return true;
            }

            Profile current = ProfileManager.CurrentProfile;

            if (current == null)
            {
                return false;
            }

            if (current.NamePlateHideAtFullHealthInWarmode && !(World.Player != null && World.Player.InWarMode))
            {
                return true;
            }

            if (current.NamePlateHideAtFullHealth && m.HitsMax > 0 && m.Hits >= m.HitsMax)
            {
                return true;
            }

            return false;
        }

        public NameOverheadGump(uint serial) : base(serial, 0)
        {
            CanMove = false;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;

            Entity entity = World.Get(serial);

            if (entity == null)
            {
                Dispose();

                return;
            }

            _text = new UOLabel(string.Empty, ProfileManager.CurrentProfile.NamePlateFont, entity is Mobile m ? Notoriety.GetHue(m.NotorietyFlag) : (ushort)0x0481, TEXT_ALIGN_TYPE.TS_CENTER, 0, FontStyle.BlackBorder);

            SetTooltip(entity);

            BuildGump();
            SetName();
        }

        public bool SetName()
        {
            Entity entity = World.Get(LocalSerial);

            if (entity == null)
            {
                return false;
            }

            if (entity is Item item)
            {
                if (!World.OPL.TryGetNameAndData(item, out string t, out _))
                {
                    _needsNameUpdate = true;
                    if (!item.IsCorpse && item.Amount > 1)
                    {
                        t = item.Amount.ToString() + ' ';
                    }

                    if (string.IsNullOrEmpty(item.ItemData.Name))
                    {
                        t += UOFileManager.Current.Clilocs.GetString(1020000 + item.Graphic, true, t);
                    }
                    else
                    {
                        t += StringHelper.CapitalizeAllWords(
                            StringHelper.GetPluralAdjustedString(
                                item.ItemData.Name,
                                item.Amount > 1
                            )
                        );
                    }
                }
                else
                {
                    _needsNameUpdate = false;
                }

                if (string.IsNullOrEmpty(t))
                {
                    return false;
                }

                _text.Text = t;

                Width = _background.Width = _text.Width + 4;
                Height = _background.Height = CurrentHeight = _text.Height;
                _hasBarsBelow = false;
                _otherPlayerSingleHpBar = false;
                _textDrawOffset.X = (Width - _text.Width - 4) >> 1;
                _textDrawOffset.Y = (Height - _text.Height) >> 1;
                WantUpdateSize = false;

                return true;
            }

            if (!string.IsNullOrEmpty(entity.Name))
            {
                string t = entity.Name;

                _text.Text = t;

                int textLineH = _text.Height;
                Mobile mobEnt = entity as Mobile;
                bool isMobile = mobEnt != null;
                bool skipInvulnHpBar = isMobile
                    && mobEnt.NotorietyFlag == NotorietyFlag.Invulnerable
                    && !mobEnt.Equals(World.Player)
                    && !World.Party.Contains(mobEnt.Serial);
                bool fullPlateHp = isMobile && FullPlateNameplateHpActive(mobEnt);
                bool isSelf = isMobile && mobEnt.Equals(World.Player);
                int nameBlockHeight = textLineH;
                bool hasHpBarBelow = isMobile
                    && ProfileManager.CurrentProfile.NamePlateHealthBar
                    && !skipInvulnHpBar
                    && !fullPlateHp;
                int hpInner = NameplateStripBarInnerHeight(textLineH);
                int barExtra = hasHpBarBelow ? hpInner + 4 : 0;
                _hasBarsBelow = hasHpBarBelow;
                _otherPlayerSingleHpBar = hasHpBarBelow;
                int textPlateW = _text.Width + 4;
                Width = _background.Width = textPlateW;
                Height = _background.Height = nameBlockHeight + barExtra;
                CurrentHeight = Height;
                _textDrawOffset.X = Math.Max(0, (Width - _text.Width - 4) >> 1);
                if (hasHpBarBelow)
                {
                    int gapBelowBar = isSelf ? 2 : 4;
                    _textDrawOffset.Y = hpInner + gapBelowBar;
                }
                else
                {
                    _textDrawOffset.Y = (Height - _text.Height) >> 1;
                }
                WantUpdateSize = false;
                _lastNamePlateHealthBar = ProfileManager.CurrentProfile.NamePlateHealthBar;
                _lastNamePlateHealthBarMatchStrip = ProfileManager.CurrentProfile.NamePlateHealthBarMatchStrip;
                _lastNamePlateFullPlateWidthScalesWithHp = ProfileManager.CurrentProfile.NamePlateFullPlateWidthScalesWithHp;
                _lastShowHpLineInNoh = ProfileManager.CurrentProfile.ShowHPLineInNOH;
                _lastNamePlateUseCustomChrome = ProfileManager.CurrentProfile.NamePlateUseCustomChrome;

                return true;
            }

            return false;
        }

        private void BuildGump()
        {
            Entity entity = World.Get(LocalSerial);

            if (entity == null)
            {
                Dispose();

                return;
            }

            Add
            (
                _background = new AlphaBlendControl(ProfileManager.CurrentProfile.NamePlateOpacity / 100f)
                {
                    WantUpdateSize = false,
                    Hue = NamePlateBackgroundHue(entity),
                    BaseColor = Color.White
                }
            );
        }

        protected override void CloseWithRightClick()
        {
            Entity entity = World.Get(LocalSerial);

            if (entity != null)
            {
                entity.ObjectHandlesStatus = ObjectHandlesStatus.CLOSED;
            }

            base.CloseWithRightClick();
        }

        private void DoDrag()
        {
            var delta = Mouse.Position - _lastLeftMousePositionDown;

            if (
                Math.Abs(delta.X) <= Constants.MIN_GUMP_DRAG_DISTANCE
                && Math.Abs(delta.Y) <= Constants.MIN_GUMP_DRAG_DISTANCE
            )
            {
                return;
            }

            _leftMouseIsDown = false;
            _positionLocked = false;

            Entity entity = World.Get(LocalSerial);

            if (entity is Mobile || entity is Item it && it.IsDamageable)
            {
                if (UIManager.IsDragging)
                {
                    return;
                }

                BaseHealthBarGump gump = UIManager.GetGump<BaseHealthBarGump>(LocalSerial);
                gump?.Dispose();

                if (ProfileManager.CurrentProfile.CustomBarsToggled)
                {
                    Rectangle rect = new Rectangle(
                        0,
                        0,
                        HealthBarGumpCustom.HPB_WIDTH,
                        HealthBarGumpCustom.HPB_HEIGHT_SINGLELINE
                    );

                    UIManager.Add(
                        gump = new HealthBarGumpCustom(entity)
                        {
                            X = Mouse.Position.X - (rect.Width >> 1),
                            Y = Mouse.Position.Y - (rect.Height >> 1)
                        }
                    );
                }
                else
                {
                    ref readonly var gumpInfo = ref Client.Game.Gumps.GetGump(0x0804);

                    UIManager.Add(
                        gump = new HealthBarGump(entity)
                        {
                            X = Mouse.LClickPosition.X - (gumpInfo.UV.Width >> 1),
                            Y = Mouse.LClickPosition.Y - (gumpInfo.UV.Height >> 1)
                        }
                    );
                }

                UIManager.AttemptDragControl(gump, true);
            }
            else if (entity != null)
            {
                GameActions.PickUp(LocalSerial, 0, 0);

                //if (entity.Texture != null)
                //    GameActions.PickUp(LocalSerial, entity.Texture.Width >> 1, entity.Texture.Height >> 1);
                //else
                //    GameActions.PickUp(LocalSerial, 0, 0);
            }
        }

        protected override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                if (SerialHelper.IsMobile(LocalSerial))
                {
                    if (World.Player.InWarMode)
                    {
                        GameActions.Attack(LocalSerial);
                    }
                    else
                    {
                        GameActions.DoubleClick(LocalSerial);
                    }
                }
                else
                {
                    if (!GameActions.OpenCorpse(LocalSerial))
                    {
                        GameActions.DoubleClick(LocalSerial);
                    }
                }

                return true;
            }

            return false;
        }

        protected override void OnMouseDown(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                _lastLeftMousePositionDown = Mouse.Position;
                _leftMouseIsDown = true;
            }

            base.OnMouseDown(x, y, button);
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                _leftMouseIsDown = false;

                if (!Client.Game.GameCursor.ItemHold.Enabled)
                {
                    if (
                        UIManager.IsDragging
                        || Math.Max(Math.Abs(Mouse.LDragOffset.X), Math.Abs(Mouse.LDragOffset.Y))
                            >= 1
                    )
                    {
                        _positionLocked = false;

                        return;
                    }
                }

                if (TargetManager.IsTargeting)
                {
                    switch (TargetManager.TargetingState)
                    {
                        case CursorTarget.Internal:
                        case CursorTarget.Position:
                        case CursorTarget.Object:
                        case CursorTarget.Grab:
                        case CursorTarget.SetGrabBag:
                            TargetManager.Target(LocalSerial);
                            Mouse.LastLeftButtonClickTime = 0;

                            break;

                        case CursorTarget.SetTargetClientSide:
                            TargetManager.Target(LocalSerial);
                            Mouse.LastLeftButtonClickTime = 0;
                            UIManager.Add(new InspectorGump(World.Get(LocalSerial)));

                            break;

                        case CursorTarget.HueCommandTarget:
                            CommandManager.OnHueTarget(World.Get(LocalSerial));

                            break;
                    }
                }
                else
                {
                    if (
                        Client.Game.GameCursor.ItemHold.Enabled
                        && !Client.Game.GameCursor.ItemHold.IsFixedPosition
                    )
                    {
                        uint drop_container = 0xFFFF_FFFF;
                        bool can_drop = false;
                        ushort dropX = 0;
                        ushort dropY = 0;
                        sbyte dropZ = 0;

                        Entity obj = World.Get(LocalSerial);

                        if (obj != null)
                        {
                            can_drop = obj.Distance <= Constants.DRAG_ITEMS_DISTANCE;

                            if (can_drop)
                            {
                                if (obj is Item it && it.ItemData.IsContainer || obj is Mobile)
                                {
                                    dropX = 0xFFFF;
                                    dropY = 0xFFFF;
                                    dropZ = 0;
                                    drop_container = obj.Serial;
                                }
                                else if (
                                    obj is Item it2
                                    && (
                                        it2.ItemData.IsSurface
                                        || it2.ItemData.IsStackable
                                            && it2.DisplayedGraphic
                                                == Client.Game.GameCursor.ItemHold.DisplayedGraphic
                                    )
                                )
                                {
                                    dropX = obj.X;
                                    dropY = obj.Y;
                                    dropZ = obj.Z;

                                    if (it2.ItemData.IsSurface)
                                    {
                                        dropZ += (sbyte)(
                                            it2.ItemData.Height == 0xFF ? 0 : it2.ItemData.Height
                                        );
                                    }
                                    else
                                    {
                                        drop_container = obj.Serial;
                                    }
                                }
                            }
                            else
                            {
                                Client.Game.Audio.PlaySound(0x0051);
                            }

                            if (can_drop)
                            {
                                if (drop_container == 0xFFFF_FFFF && dropX == 0 && dropY == 0)
                                {
                                    can_drop = false;
                                }

                                if (can_drop)
                                {
                                    GameActions.DropItem(
                                        Client.Game.GameCursor.ItemHold.Serial,
                                        dropX,
                                        dropY,
                                        dropZ,
                                        drop_container
                                    );
                                }
                            }
                        }
                    }
                    else if (!DelayedObjectClickManager.IsEnabled)
                    {
                        DelayedObjectClickManager.Set(
                            LocalSerial,
                            Mouse.Position.X,
                            Mouse.Position.Y,
                            Time.Ticks + Mouse.MOUSE_DELAY_DOUBLE_CLICK
                        );
                    }
                }
            }

            base.OnMouseUp(x, y, button);
        }

        protected override void OnMouseOver(int x, int y)
        {
            if (_leftMouseIsDown)
            {
                DoDrag();
            }

            if (!_positionLocked && SerialHelper.IsMobile(LocalSerial))
            {
                Mobile m = World.Mobiles.Get(LocalSerial);

                if (m == null)
                {
                    Dispose();

                    return;
                }

                _positionLocked = true;
            }

            base.OnMouseOver(x, y);
        }

        protected override void OnMouseExit(int x, int y)
        {
            _positionLocked = false;
            base.OnMouseExit(x, y);
        }

        public override void Update()
        {
            base.Update();

            Entity entity = World.Get(LocalSerial);

            if (
                entity == null
                || entity.IsDestroyed
                || entity.ObjectHandlesStatus == ObjectHandlesStatus.NONE
                || entity.ObjectHandlesStatus == ObjectHandlesStatus.CLOSED
            )
            {
                Dispose();
            }
            else
            {
                if (ProfileManager.CurrentProfile != null && _background != null)
                {
                    _background.Alpha = ProfileManager.CurrentProfile.NamePlateOpacity / 100f;
                }

                Profile p = ProfileManager.CurrentProfile;

                if (entity.Serial == TargetManager.LastTargetInfo.Serial && !entity.Equals(World.Player))
                {
                    if (!_isLastTarget)
                    {
                        _borderColor = SolidColorTextureCache.GetTexture(Color.Red);
                        _isLastTarget = true;
                    }
                }
                else if (_isLastTarget)
                {
                    _borderColor = SolidColorTextureCache.GetTexture(Color.Black);
                    _isLastTarget = false;
                }

                if (entity is Mobile mobUp && p != null)
                {
                    ushort notoHue = Notoriety.GetHue(mobUp.NotorietyFlag);
                    _text.Hue = notoHue;
                    bool lastTargetSerial = mobUp.Serial == TargetManager.LastTargetInfo.Serial && !mobUp.Equals(World.Player);
                    if (lastTargetSerial)
                    {
                        _background.Hue = notoHue;
                    }
                    else
                    {
                        _background.Hue = p.NamePlateUseCustomChrome ? p.NamePlateCustomBackgroundHue : notoHue;
                    }
                }

                if (_needsNameUpdate)
                {
                    SetName();
                }

                if (entity is Mobile mCh && p != null)
                {
                    bool curBar = p.NamePlateHealthBar;
                    bool curStrip = p.NamePlateHealthBarMatchStrip;
                    bool curFullW = p.NamePlateFullPlateWidthScalesWithHp;
                    bool curNoh = p.ShowHPLineInNOH;
                    bool curCustom = p.NamePlateUseCustomChrome;
                    bool needLayout = curBar != _lastNamePlateHealthBar
                        || curStrip != _lastNamePlateHealthBarMatchStrip
                        || curFullW != _lastNamePlateFullPlateWidthScalesWithHp
                        || curNoh != _lastShowHpLineInNoh
                        || curCustom != _lastNamePlateUseCustomChrome;
                    if (needLayout)
                    {
                        _lastNamePlateHealthBar = curBar;
                        _lastNamePlateHealthBarMatchStrip = curStrip;
                        _lastNamePlateFullPlateWidthScalesWithHp = curFullW;
                        _lastShowHpLineInNoh = curNoh;
                        _lastNamePlateUseCustomChrome = curCustom;
                        SetName();
                    }
                }
            }
        }

        private static ushort NamePlateBackgroundHue(Entity entity)
        {
            Profile p = ProfileManager.CurrentProfile;
            if (p != null && entity is Mobile && p.NamePlateUseCustomChrome)
            {
                return p.NamePlateCustomBackgroundHue;
            }

            return entity is Mobile m ? Notoriety.GetHue(m.NotorietyFlag) : Notoriety.GetHue(NotorietyFlag.Gray);
        }

        private static int HealthBarInnerHeight(int nameTextHeight)
        {
            Profile p = ProfileManager.CurrentProfile;
            if (p == null || !p.NamePlateHealthBarMatchStrip)
            {
                return 4;
            }

            return Math.Max(4, nameTextHeight);
        }

        private static int NameplateStripBarInnerHeight(int nameTextHeight)
        {
            return Math.Max(6, HealthBarInnerHeight(nameTextHeight));
        }

        private static bool FullPlateNameplateHpActive(Mobile mob)
        {
            if (mob == null)
            {
                return false;
            }

            Profile p = ProfileManager.CurrentProfile;
            if (p == null || !p.ShowHPLineInNOH)
            {
                return false;
            }

            if (mob.NotorietyFlag == NotorietyFlag.Invulnerable
                && !mob.Equals(World.Player)
                && !World.Party.Contains(mob.Serial))
            {
                return false;
            }

            return true;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (IsDisposed)
            {
                return false;
            }

            bool _isMobile = false;
            double _hpPercent = 1;
            IsVisible = true;
            if (SerialHelper.IsMobile(LocalSerial))
            {
                Mobile m = World.Mobiles.Get(LocalSerial);

                if (m == null)
                {
                    Dispose();

                    return false;
                }

                if (!string.IsNullOrEmpty(NameOverHeadManager.Search))
                {
                    string sText = NameOverHeadManager.Search.ToLower();
                    if (m.Name == null || !m.Name.ToLower().Contains(sText))
                    {
                        if (World.OPL.TryGetNameAndData(m.Serial, out string name, out string data))
                        {
                            if (/*(data != null && !data.ToLower().Contains(sText)) && */(name != null && !name.ToLower().Contains(sText)))
                            {
                                IsVisible = false;
                                return true;
                            }
                        }
                        else
                        {
                            IsVisible = false;
                            return true;
                        }
                    }
                }

                _isMobile = true;
                _hpPercent = (double)m.Hits / (double)m.HitsMax;

                IsVisible = true;

                bool onlyWarMode = ProfileManager.CurrentProfile.NamePlateHideAtFullHealthInWarmode;
                bool playerInWar = World.Player != null && World.Player.InWarMode;

                if (onlyWarMode && !playerInWar)
                {
                    IsVisible = false;
                    return false;
                }

                if (ProfileManager.CurrentProfile.NamePlateHideAtFullHealth && _hpPercent >= 1)
                {
                    IsVisible = false;
                    return false;
                }

                Client.Game.Animations.GetAnimationDimensions(
                    m.AnimIndex,
                    m.GetGraphicForAnimation(),
                    /*(byte) m.GetDirectionForAnimation())*/
                    0,
                    /*Mobile.GetGroupForAnimation(m, isParent:true)*/
                    0,
                    m.IsMounted,
                    /*(byte) m.AnimIndex*/
                    0,
                    out int centerX,
                    out int centerY,
                    out int width,
                    out int height
                );

                x = (int)(m.RealScreenPosition.X + m.Offset.X + 22 + 5);
                y = (int)(
                    m.RealScreenPosition.Y
                    + (m.Offset.Y - m.Offset.Z)
                    - (height + centerY + 15)
                    + (
                        m.IsGargoyle && m.IsFlyingVisual
                            ? -22
                            : !m.IsMounted
                                ? 22
                                : 0
                    )
                    + 8
                );
            }
            else if (SerialHelper.IsItem(LocalSerial))
            {
                Item item = World.Items.Get(LocalSerial);

                if (item == null)
                {
                    Dispose();
                    return false;
                }

                if (!string.IsNullOrEmpty(NameOverHeadManager.Search))
                {
                    string sText = NameOverHeadManager.Search.ToLower();
                    if (item.Name == null || !item.Name.ToLower().Contains(sText))// && (!item.ItemData.Name?.ToLower().Contains(sText)))
                    {
                        if (World.OPL.TryGetNameAndData(item.Serial, out string name, out string data))
                        {
                            if ((data != null && !data.ToLower().Contains(sText)) && (name != null && !name.ToLower().Contains(sText)))
                            {
                                IsVisible = false;
                                return true;
                            }
                        }
                        else
                        {
                            IsVisible = false;
                            return true;
                        }
                    }
                }

                var bounds = Client.Game.Arts.GetRealArtBounds(item.Graphic);

                x = item.RealScreenPosition.X + (int)item.Offset.X + 22 + 5;
                y =
                    item.RealScreenPosition.Y
                    + (int)(item.Offset.Y - item.Offset.Z)
                    + (bounds.Height >> 1);
            }

            Point p = Client.Game.Scene.Camera.WorldToScreen(new Point(x, y));
            x = p.X - (Width >> 1);
            y = p.Y - (Height);// >> 1);

            var camera = Client.Game.Scene.Camera;
            x += camera.Bounds.X;
            y += camera.Bounds.Y;

            if (x < camera.Bounds.X || x + Width > camera.Bounds.Right)
            {
                return false;
            }

            if (y < camera.Bounds.Y || y + Height > camera.Bounds.Bottom)
            {
                return false;
            }

            X = x;
            Y = y;

            Profile prof = ProfileManager.CurrentProfile;

            Vector3 hueVector;
            Mobile mBorder = _isMobile ? World.Mobiles.Get(LocalSerial) : null;
            if (_isLastTarget && _isMobile)
            {
                hueVector = ShaderHueTranslator.GetHueVector(0, false, prof != null ? prof.NamePlateBorderOpacity / 100f : 0.5f, true);
            }
            else if (prof != null && prof.NamePlateUseCustomChrome && _isMobile)
            {
                hueVector = ShaderHueTranslator.GetHueVector(
                    prof.NamePlateCustomBorderHue,
                    false,
                    prof.NamePlateBorderOpacity / 100f,
                    true);
            }
            else if (mBorder != null && prof != null)
            {
                hueVector = ShaderHueTranslator.GetHueVector(
                    Notoriety.GetHue(mBorder.NotorietyFlag),
                    false,
                    prof.NamePlateBorderOpacity / 100f,
                    true);
            }
            else
            {
                hueVector = ShaderHueTranslator.GetHueVector(0);
                hueVector.Z = prof != null ? prof.NamePlateBorderOpacity / 100f : 0.5f;
            }

            batcher.DrawRectangle
            (
                _borderColor,
                x,
                y,
                Width,
                Height,
                hueVector
            );

            base.Draw(batcher, x, y);

            if (_isMobile && prof != null && prof.ShowHPLineInNOH)
            {
                Mobile mPlate = World.Mobiles.Get(LocalSerial);
                if (mPlate != null && FullPlateNameplateHpActive(mPlate))
                {
                    DrawFullPlateHpOverBackground(batcher, mPlate, x, y);
                }
            }

            if (ProfileManager.CurrentProfile.NamePlateHealthBar && _isMobile && !ProfileManager.CurrentProfile.ShowHPLineInNOH)
            {
                Mobile m = World.Mobiles.Get(LocalSerial);
                if (m != null)
                {
                    bool isPlayer = m is PlayerMobile;
                    bool isInParty = World.Party.Contains(m.Serial);
                    if (m.NotorietyFlag != NotorietyFlag.Invulnerable || isPlayer || isInParty)
                    {
                        DrawHpBarAboveName(batcher, m, x, y, ProfileManager.CurrentProfile.NamePlateHealthBarOpacity / 100f);
                    }
                }
            }

            if (_isLastTarget && _isMobile)
            {
                Mobile m = World.Mobiles.Get(LocalSerial);
                if (m != null && !m.Equals(World.Player))
                    DrawDistanceBar(batcher, m, x, y);
            }

            return _text.Draw(batcher, (int)(x + 2 + _textDrawOffset.X), (int)(y + 2 + _textDrawOffset.Y));
        }

        private const int DIST_BAR_HEIGHT = 5;
        private const int DIST_BAR_GAP = 2;

        private void DrawDistanceBar(UltimaBatcher2D batcher, Mobile m, int x, int y)
        {
            int dist = m.Distance;
            Color barColor = dist <= 6 ? Color.Green : dist <= 12 ? Color.Yellow : Color.Red;
            float fillFraction = dist <= 6 ? 1f : dist <= 12 ? (12f - dist) / 6f : 0.15f;
            int barWidth = Width;
            int barY = y + Height + DIST_BAR_GAP;
            Vector3 alpha = ShaderHueTranslator.GetHueVector(0, false, 0.85f, true);
            Vector3 border = ShaderHueTranslator.GetHueVector(0, false, 0.9f, true);
            Color borderColor = new Color(
                Math.Max(0, barColor.R - 80),
                Math.Max(0, barColor.G - 80),
                Math.Max(0, barColor.B - 80)
            );
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(borderColor), x + 1, barY, barWidth - 2, DIST_BAR_HEIGHT + 2, border);
            int fillW = Math.Max(1, (int)((barWidth - 4) * fillFraction));
            batcher.Draw(SolidColorTextureCache.GetTexture(barColor), new Vector2(x + 2, barY + 1), new Rectangle(0, 0, fillW, DIST_BAR_HEIGHT), alpha);
        }
        private static int NohHpFillWidth(int hitsMax, int hits, int barPixels)
        {
            if (hitsMax <= 0 || barPixels <= 0)
            {
                return 0;
            }

            long w = (long)hits * barPixels / hitsMax;
            if (w < 0)
            {
                return 0;
            }

            if (w > barPixels)
            {
                return barPixels;
            }

            return (int)w;
        }

        private static ushort NameplateHueForHpOverlay(Mobile m)
        {
            if (m.Serial == TargetManager.LastTargetInfo.Serial && !m.Equals(World.Player))
            {
                return Notoriety.GetHue(m.NotorietyFlag);
            }

            Profile p = ProfileManager.CurrentProfile;
            if (p != null && p.NamePlateUseCustomChrome)
            {
                return p.NamePlateCustomBackgroundHue;
            }

            return Notoriety.GetHue(m.NotorietyFlag);
        }

        private void DrawFullPlateHpOverBackground(UltimaBatcher2D batcher, Mobile m, int gx, int gy)
        {
            int w = Width;
            int h = Height;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            Profile pr = ProfileManager.CurrentProfile;
            bool widthTracksHp = pr != null && pr.NamePlateFullPlateWidthScalesWithHp && m.HitsMax > 0;
            float bgOp = pr != null ? pr.NamePlateOpacity / 100f : 0.75f;
            float fillA = Math.Min(0.97f, 0.52f + bgOp * 0.44f);
            float depA = Math.Max(0.2f, Math.Min(fillA - 0.18f, 0.26f + bgOp * 0.42f));
            Texture2D depletedTex = SolidColorTextureCache.GetTexture(new Color(52, 52, 56));
            Texture2D fillTex = SolidColorTextureCache.GetTexture(Color.White);
            ushort nh = NameplateHueForHpOverlay(m);
            if (nh == 0)
            {
                nh = Notoriety.GetHue(m.NotorietyFlag);
            }

            if (nh == 0)
            {
                nh = 0x0481;
            }

            Vector3 vDepleted = ShaderHueTranslator.GetHueVector(nh, true, depA, true);
            Vector3 vFill = ShaderHueTranslator.GetHueVector(nh, false, fillA, true);

            if (widthTracksHp)
            {
                if (m.HitsMax <= 0)
                {
                    batcher.Draw(depletedTex, new Rectangle(gx, gy, w, h), vDepleted);
                    return;
                }

                int poolW = Math.Min(w, (w * m.Hits + m.HitsMax - 1) / m.HitsMax);
                if (m.Hits > 0 && poolW < 1)
                {
                    poolW = 1;
                }

                int poolX = gx + (w - poolW) / 2;
                batcher.Draw(depletedTex, new Rectangle(gx, gy, w, h), vDepleted);
                if (poolW > 0)
                {
                    batcher.Draw(fillTex, new Rectangle(poolX, gy, poolW, h), vFill);
                }

                return;
            }

            int hpW = NohHpFillWidth(m.HitsMax, m.Hits, w);
            batcher.Draw(depletedTex, new Rectangle(gx, gy, w, h), vDepleted);
            if (hpW > 0)
            {
                batcher.Draw(fillTex, new Rectangle(gx, gy, hpW, h), vFill);
            }
        }

        private void DrawColoredBar(UltimaBatcher2D batcher, int x, int barY, int barWidth, int innerH, Texture2D texture, Color barColor, Vector3 barHue, Vector3 borderHue, double percent, int minFillWidth)
        {
            Color borderColor = new Color(
                Math.Max(0, barColor.R - 80),
                Math.Max(0, barColor.G - 80),
                Math.Max(0, barColor.B - 80)
            );
            batcher.DrawRectangle(
                SolidColorTextureCache.GetTexture(borderColor),
                x + 1,
                barY,
                barWidth - 2,
                innerH + 2,
                borderHue
            );
            int innerPixels = Math.Max(0, barWidth - 4);
            int fillWidth = innerPixels <= 0 ? 0 : Math.Max(0, (int)(innerPixels * percent + 1e-6));
            if (percent > 0 && minFillWidth > 0 && fillWidth > 0 && fillWidth < minFillWidth)
            {
                fillWidth = Math.Min(minFillWidth, innerPixels);
            }

            if (fillWidth > 0)
            {
                batcher.Draw(texture, new Vector2(x + 2, barY + 1), new Rectangle(0, 0, fillWidth, innerH), barHue);
            }
        }

        private void DrawHpBarAboveName(UltimaBatcher2D batcher, Mobile m, int x, int y, float alpha)
        {
            int hpH = NameplateStripBarInnerHeight(_text.Height);
            int barY = y + 2;
            int barWidth = Width;
            double hpPercent = m.HitsMax > 0 ? (double)m.Hits / m.HitsMax : 1;
            Color barColor;
            if (hpPercent > 0.5)
            {
                barColor = new Color(45, 210, 90);
            }
            else if (hpPercent > 0.2)
            {
                barColor = new Color(255, 215, 45);
            }
            else
            {
                barColor = new Color(255, 28, 28);
            }

            Vector3 barHue = ShaderHueTranslator.GetHueVector(0, false, alpha, true);
            Vector3 borderHue = ShaderHueTranslator.GetHueVector(0, false, 0.9f, true);
            DrawColoredBar(
                batcher,
                x,
                barY,
                barWidth,
                hpH,
                SolidColorTextureCache.GetTexture(barColor),
                barColor,
                barHue,
                borderHue,
                hpPercent,
                m.Hits > 0 ? 3 : 0);
        }

        public override void Dispose()
        {
            _text.Dispose();
            base.Dispose();
        }
    }
}
