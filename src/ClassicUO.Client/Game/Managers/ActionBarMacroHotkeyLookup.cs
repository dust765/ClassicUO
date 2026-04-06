using ClassicUO.Input;
using SDL3;

namespace ClassicUO.Game.Managers
{
    internal static class ActionBarMacroHotkeyLookup
    {
        public static bool ActionMatchesSlot(ActionBarSlotData slot, MacroObject action)
        {
            if (slot == null || action == null)
            {
                return false;
            }
            if (action.Code == MacroType.CastSpell)
            {
                if (!MacroManager.TryGetSpellFullIdFromCastSpellSubCode(action.SubCode, out int sid))
                {
                    return false;
                }
                return (slot.SlotType == (int)ActionBarSlotType.Spell || slot.SlotType == 0) && slot.SpellID == sid;
            }
            if (action.Code == MacroType.UseSkill)
            {
                if (!MacroManager.TryGetSkillServerIndexFromUseSkillSubCode(action.SubCode, out int sk))
                {
                    return false;
                }
                return slot.SlotType == (int)ActionBarSlotType.Skill && slot.SkillIndex == sk;
            }
            return false;
        }

        public static bool TryFindKeyboardHotkeyForSlot(ActionBarSlotData slot, MacroManager mm, out int key, out bool alt, out bool ctrl, out bool shift)
        {
            key = 0;
            alt = false;
            ctrl = false;
            shift = false;
            if (slot == null || mm == null)
            {
                return false;
            }
            foreach (Macro macro in mm.GetAllMacros())
            {
                if (macro == null)
                {
                    continue;
                }
                if (macro.WheelScroll || macro.MouseButton != MouseButtonType.None)
                {
                    continue;
                }
                if (macro.Key == SDL.SDL_Keycode.SDLK_UNKNOWN)
                {
                    continue;
                }
                MacroObject mo = Macro.FindFirstCastSpellOrUseSkillAction(macro);
                if (mo == null || !ActionMatchesSlot(slot, mo))
                {
                    continue;
                }
                key = (int)macro.Key;
                alt = macro.Alt;
                ctrl = macro.Ctrl;
                shift = macro.Shift;
                return true;
            }
            return false;
        }

        public static void ApplyMacroHotkeyIfSlotKeyEmpty(ActionBarSlotData slot, MacroManager mm)
        {
            if (slot == null || mm == null)
            {
                return;
            }
            if (slot.Key != 0 && (SDL.SDL_Keycode)slot.Key != SDL.SDL_Keycode.SDLK_UNKNOWN)
            {
                return;
            }
            if (!TryFindKeyboardHotkeyForSlot(slot, mm, out int key, out bool alt, out bool ctrl, out bool shift))
            {
                return;
            }
            slot.Key = key;
            slot.Alt = alt;
            slot.Ctrl = ctrl;
            slot.Shift = shift;
        }
    }
}
