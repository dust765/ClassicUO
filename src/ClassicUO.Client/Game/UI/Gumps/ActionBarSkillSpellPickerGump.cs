using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using Microsoft.Xna.Framework;
using SDL3;

namespace ClassicUO.Game.UI.Gumps
{
    internal enum ActionBarPickerMode
    {
        Skills,
        Spells
    }

    internal static class ActionBarSkillResolver
    {
        public static Skill FindPlayerSkill(int skillServerIndex)
        {
            if (World.Player == null || skillServerIndex < 0)
            {
                return null;
            }

            for (int j = 0; j < World.Player.Skills.Length; j++)
            {
                Skill s = World.Player.Skills[j];
                if (s != null && s.Index == skillServerIndex)
                {
                    return s;
                }
            }

            return null;
        }

        public static bool PlayerHasSkillServerIndex(int skillServerIndex)
        {
            return FindPlayerSkill(skillServerIndex) != null;
        }

        public static void ApplySkillToSlot(int slotIndex, int skillServerIndex)
        {
            Profile p = ProfileManager.CurrentProfile;
            if (p?.ActionBarSlots == null || slotIndex < 0 || slotIndex >= p.ActionBarSlots.Count)
            {
                return;
            }

            ActionBarSlotData slot = p.ActionBarSlots[slotIndex];
            slot.SlotType = (int)ActionBarSlotType.Skill;
            slot.SpellID = 0;
            slot.SkillIndex = skillServerIndex;
            slot.AbilityIndex = 0;
            slot.MacroName = null;
            ActionBarMacroHotkeyLookup.ApplyMacroHotkeyIfSlotKeyEmpty(slot, MacroManager.TryGetMacroManager());
            UIManager.GetGump<ActionBarGump>()?.RefreshSlots();
        }

        public static void ApplySpellToSlot(int slotIndex, int spellFullId)
        {
            Profile p = ProfileManager.CurrentProfile;
            if (p?.ActionBarSlots == null || slotIndex < 0 || slotIndex >= p.ActionBarSlots.Count)
            {
                return;
            }

            SpellDefinition def = SpellDefinition.FullIndexGetSpell(spellFullId);
            if (def == null || def.ID <= 0)
            {
                return;
            }

            ActionBarSlotData slot = p.ActionBarSlots[slotIndex];
            slot.SlotType = (int)ActionBarSlotType.Spell;
            slot.SpellID = spellFullId;
            slot.SkillIndex = -1;
            slot.AbilityIndex = 0;
            slot.MacroName = null;
            ActionBarMacroHotkeyLookup.ApplyMacroHotkeyIfSlotKeyEmpty(slot, MacroManager.TryGetMacroManager());
            UIManager.GetGump<ActionBarGump>()?.RefreshSlots();
        }

        public static void ApplyMacroToSlot(int slotIndex, string macroName)
        {
            Profile p = ProfileManager.CurrentProfile;
            if (p?.ActionBarSlots == null || slotIndex < 0 || slotIndex >= p.ActionBarSlots.Count)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(macroName))
            {
                return;
            }
            MacroManager mm = MacroManager.TryGetMacroManager();
            if (mm?.FindMacro(macroName) == null)
            {
                return;
            }
            ActionBarSlotData slot = p.ActionBarSlots[slotIndex];
            slot.SlotType = (int)ActionBarSlotType.Macro;
            slot.MacroName = macroName.Trim();
            slot.SpellID = 0;
            slot.SkillIndex = -1;
            slot.AbilityIndex = 0;
            Macro m = mm.FindMacro(macroName.Trim());
            if (m != null && (slot.Key == 0 || (SDL.SDL_Keycode)slot.Key == SDL.SDL_Keycode.SDLK_UNKNOWN) && m.Key != SDL.SDL_Keycode.SDLK_UNKNOWN && !m.WheelScroll && m.MouseButton == MouseButtonType.None)
            {
                slot.Key = (int)m.Key;
                slot.Alt = m.Alt;
                slot.Ctrl = m.Ctrl;
                slot.Shift = m.Shift;
            }
            UIManager.GetGump<ActionBarGump>()?.RefreshSlots();
        }
    }

    internal sealed class ActionBarSkillSpellPickerGump : Gump
    {
        private readonly int _slotIndex;
        private readonly ActionBarPickerMode _mode;
        private readonly DataBox _listBox;
        private Combobox _schoolCombo;
        private ScrollArea _scroll;

        private static readonly string[] SchoolNames =
        {
            "Magery",
            "Necromancy",
            "Chivalry",
            "Bushido",
            "Ninjitsu",
            "Spellweaving",
            "Mysticism",
            "Spell Mastery"
        };

        public ActionBarSkillSpellPickerGump(int slotIndex, ActionBarPickerMode mode) : base(0, 0)
        {
            _slotIndex = slotIndex;
            _mode = mode;
            CanMove = true;
            AcceptMouseInput = true;
            IsModal = true;
            ModalClickOutsideAreaClosesThisControl = true;
            LayerOrder = UILayer.Over;

            const int gw = 320;
            const int gh = 392;
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

            string title = mode == ActionBarPickerMode.Skills
                ? (Language.Instance?.GetOptionsGumpLanguage?.GetActionBar?.PickSkillTitle ?? "Skills — pick for slot")
                : (Language.Instance?.GetOptionsGumpLanguage?.GetActionBar?.PickSpellTitle ?? "Spells — pick school, then spell");

            Add(
                new Label(title, true, 0xFFFF, 0, 1, FontStyle.BlackBorder)
                {
                    X = 14,
                    Y = 12
                }
            );

            var closeBtn = new NiceButton(gw - 88, 8, 72, 24, ButtonAction.Activate, Language.Instance?.GetOptionsGumpLanguage?.GetActionBar?.PickerClose ?? "Close");
            closeBtn.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtonType.Left)
                {
                    Dispose();
                }
            };
            Add(closeBtn);

            _listBox = new DataBox(0, 0, 280, 1) { WantUpdateSize = true };

            if (mode == ActionBarPickerMode.Spells)
            {
                _schoolCombo = new Combobox(14, 40, 200, SchoolNames, 0, 220);
                _schoolCombo.OnOptionSelected += (_, idx) => RebuildSpellList(idx);
                Add(_schoolCombo);
                _scroll = new ScrollArea(14, 72, 292, 268, true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
                _scroll.Add(_listBox);
                Add(_scroll);
                RebuildSpellList(0);
            }
            else
            {
                _scroll = new ScrollArea(14, 44, 292, 296, true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
                _scroll.Add(_listBox);
                Add(_scroll);
                RebuildSkillList();
            }
        }

        public override GumpType GumpType => GumpType.None;

        private static IEnumerable<SpellDefinition> SpellsForSchool(int schoolIndex)
        {
            IReadOnlyDictionary<int, SpellDefinition> dict = schoolIndex switch
            {
                0 => SpellsMagery.GetAllSpells,
                1 => SpellsNecromancy.GetAllSpells,
                2 => SpellsChivalry.GetAllSpells,
                3 => SpellsBushido.GetAllSpells,
                4 => SpellsNinjitsu.GetAllSpells,
                5 => SpellsSpellweaving.GetAllSpells,
                6 => SpellsMysticism.GetAllSpells,
                7 => SpellsMastery.GetAllSpells,
                _ => SpellsMagery.GetAllSpells
            };

            return dict.Values.Where(s => s != null && s.ID > 0 && !string.IsNullOrEmpty(s.Name)).OrderBy(s => s.ID);
        }

        private static void ClearDataBox(DataBox box)
        {
            for (int i = box.Children.Count - 1; i >= 0; i--)
            {
                box.Children[i].Dispose();
            }
        }

        private void RebuildSpellList(int schoolIndex)
        {
            ClearDataBox(_listBox);
            foreach (SpellDefinition spell in SpellsForSchool(schoolIndex))
            {
                int sid = spell.ID;
                string line = spell.Name;
                var row = new NiceButton(0, 0, 268, 22, ButtonAction.Activate, line) { IsSelectable = false };
                row.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                    {
                        ActionBarSkillResolver.ApplySpellToSlot(_slotIndex, sid);
                        Dispose();
                    }
                };
                _listBox.Add(row);
            }

            if (_listBox.Children.Count == 0)
            {
                _listBox.Add(new Label(Language.Instance?.GetOptionsGumpLanguage?.GetActionBar?.NoSpellsInSchool ?? "(no spells)", true, 0x0386, 0, 1) { X = 0, Y = 4 });
            }

            _listBox.ReArrangeChildren(2);
        }

        private void RebuildSkillList()
        {
            ClearDataBox(_listBox);
            if (!World.InGame || World.Player == null)
            {
                _listBox.Add(new Label("(not in game)", true, 0x0386, 0, 1) { X = 0, Y = 4 });
                _listBox.ReArrangeChildren();
                return;
            }

            for (int i = 0; i < World.Player.Skills.Length; i++)
            {
                Skill sk = World.Player.Skills[i];
                if (sk == null || string.IsNullOrEmpty(sk.Name))
                {
                    continue;
                }

                int serverIdx = sk.Index;
                string label = sk.Name;
                if (!sk.IsClickable)
                {
                    label += " *";
                }

                var row = new NiceButton(0, 0, 268, 22, ButtonAction.Activate, label) { IsSelectable = false };
                row.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                    {
                        ActionBarSkillResolver.ApplySkillToSlot(_slotIndex, serverIdx);
                        Dispose();
                    }
                };
                _listBox.Add(row);
            }

            _listBox.ReArrangeChildren(2);
        }
    }
}
