using System;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ActionBarMacroPickerGump : Gump
    {
        public ActionBarMacroPickerGump(int slotIndex) : base(0, 0)
        {
            CanMove = true;
            AcceptMouseInput = true;
            IsModal = true;
            ModalClickOutsideAreaClosesThisControl = true;
            LayerOrder = UILayer.Over;

            const int gw = 340;
            const int gh = 400;
            Width = gw;
            Height = gh;
            X = (Client.Game.Window.ClientBounds.Width - gw) / 2;
            Y = Math.Max(40, (Client.Game.Window.ClientBounds.Height - gh) / 2);

            Add(new BorderControl(0, 0, gw, gh, 4));
            Add(
                new AlphaBlendControl(0.94f)
                {
                    X = 4,
                    Y = 4,
                    Width = gw - 8,
                    Height = gh - 8,
                    BaseColor = Color.FromNonPremultiplied(22, 22, 32, 252)
                }
            );

            string title = Language.Instance?.GetOptionsGumpLanguage?.GetActionBar?.PickMacroTitle ?? "Choose macro for this slot";
            Add(new Label(title, true, 0xFFFF, 0, 1, FontStyle.BlackBorder) { X = 14, Y = 12 });

            var closeBtn = new NiceButton(gw - 88, 8, 72, 24, ButtonAction.Activate, Language.Instance?.GetOptionsGumpLanguage?.GetActionBar?.PickerClose ?? "Close");
            closeBtn.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtonType.Left)
                {
                    Dispose();
                }
            };
            Add(closeBtn);

            var listBox = new DataBox(0, 0, 300, 1) { WantUpdateSize = true };
            var scroll = new ScrollArea(14, 44, 312, 320, true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowWhenDataExceedFromView };
            scroll.Add(listBox);
            Add(scroll);

            MacroManager mm = MacroManager.TryGetMacroManager();
            if (mm == null)
            {
                listBox.Add(new Label("(macros unavailable)", true, 0x0386, 0, 1) { X = 0, Y = 4 });
            }
            else
            {
                foreach (Macro macro in mm.GetAllMacros())
                {
                    if (macro == null || string.IsNullOrEmpty(macro.Name))
                    {
                        continue;
                    }
                    string name = macro.Name;
                    var row = new NiceButton(0, 0, 296, 22, ButtonAction.Activate, name) { IsSelectable = false };
                    row.MouseUp += (_, e) =>
                    {
                        if (e.Button == MouseButtonType.Left)
                        {
                            ActionBarSkillResolver.ApplyMacroToSlot(slotIndex, name);
                            Dispose();
                        }
                    };
                    listBox.Add(row);
                }
                if (listBox.Children.Count == 0)
                {
                    listBox.Add(new Label("(no macros)", true, 0x0386, 0, 1) { X = 0, Y = 4 });
                }
            }

            listBox.ReArrangeChildren(2);
        }

        public override GumpType GumpType => GumpType.None;
    }
}
