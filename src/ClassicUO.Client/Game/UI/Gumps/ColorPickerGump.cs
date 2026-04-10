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

using System;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps
{
    internal class ColorPickerGump : Gump
    {
        private const int SLIDER_MIN = 0;
        private const int SLIDER_MAX = 4;
        private readonly ColorPickerBox _box;
        private readonly StaticPic _dyeTybeImage;
        private readonly HSliderBar _slider;
        private ushort _selectedHue;

        private readonly ushort _graphic;
        private readonly Action<ushort> _okClicked;

        public ColorPickerGump(uint serial, ushort graphic, int x, int y, Action<ushort> okClicked) : base(serial, 0)
        {
            CanCloseWithRightClick = serial == 0;
            _graphic = graphic;
            CanMove = true;
            AcceptMouseInput = false;
            X = x;
            Y = y;

            Add(new GumpPic(0, 0, 0x0906, 0));

            Add
            (
                new Button(1, 0x5669, 0x566B, 0x566A)
                {
                    X = 212,
                    Y = 33,
                    ButtonAction = ButtonAction.Activate
                }
            );

            Add
            (
                new Button(0, 0x0907, 0x0908, 0x909)
                {
                    X = 208, Y = 138, ButtonAction = ButtonAction.Activate
                }
            );

            Add
            (
                _slider = new HSliderBar
                (
                    39,
                    142,
                    145,
                    SLIDER_MIN,
                    SLIDER_MAX,
                    1,
                    HSliderBarStyle.BlueWidgetNoBar
                )
            );

            _slider.ValueChanged += (sender, e) => { _box.Graduation = _slider.Value; };
            Add(_box = new ColorPickerBox(34, 34));

            _box.ColorSelectedIndex += (sender, e) =>
            {
                _selectedHue = _box.SelectedHue;
                _dyeTybeImage.Hue = _selectedHue;
            };

            Add
            (
                _dyeTybeImage = new StaticPic(0x0FAB, 0)
                {
                    X = 200, Y = 78
                }
            );

            _okClicked = okClicked;
            _selectedHue = _box.SelectedHue;
            _dyeTybeImage.Hue = _selectedHue;
        }

        public ushort Graphic => _graphic;

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 0:

                    if (LocalSerial != 0)
                    {
                        NetClient.Socket.Send_DyeDataResponse(LocalSerial, _graphic, _selectedHue);
                    }

                    _okClicked?.Invoke(_selectedHue);
                    Dispose();

                    break;
                case 1:

                    _ = TargetHelper.TargetObject(EntityTargeted);
                    break;
            }
        }

        private void EntityTargeted(Entity obj)
        {
            if (obj != null)
            {
                _box.SelectedHue = obj.Hue;

                if (_box.SelectedHue == obj.Hue)
                {
                    _slider.Value = _box.Graduation;
                    _selectedHue = _box.SelectedHue;
                    _dyeTybeImage.Hue = _selectedHue;
                }
                else
                {
                    string badHueMessage = UOFileManager.Current.Clilocs.GetString(1042295);

                    MessageManager.HandleMessage
                    (
                        obj,
                        badHueMessage,
                        "System",
                        0,
                        MessageType.Regular,
                        3,
                        TextType.SYSTEM
                    );
                }
            }
        }
    }
}