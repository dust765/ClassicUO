#region license

// Copyright (C) 2020 project dust765
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion
using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using Microsoft.Xna.Framework;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Dust765.Macros
{
    internal class GrabBars
    {
        public static void GrabFriendlyBars()
        {
            foreach (KeyValuePair<uint, Mobile> mkv in World.Mobiles)
            {
                Mobile mobile = mkv.Value;

                if (mobile.Distance >= 18)
                {
                    continue;
                }

                if (mobile.Name == null || mobile.Name.Length == 0)
                {
                    return;
                }

                if (mobile.NotorietyFlag != NotorietyFlag.Innocent || mobile == World.Player || mobile.NotorietyFlag == NotorietyFlag.Invulnerable)
                {
                    continue;
                }

                Entity entity = World.Get(mobile.Serial);
                Point offset = ProfileManager.CurrentProfile.PullFriendlyBars;

                if (Math.Abs(offset.X) <= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS && Math.Abs(offset.Y) <= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS)
                {
                    continue;
                }

                GameActions.RequestMobileStatus(entity.Serial);
                HealthBarGumpCustom customgump = UIManager.GetGump<HealthBarGumpCustom>(entity.Serial);

                if (customgump != null)
                {
                    customgump.Dispose();
                }

                if (entity.Serial == World.Player)
                {
                    StatusGumpBase.GetStatusGump()?.Dispose();
                }

                int bar = UIManager.CountHealthBarGumpCustom();
                Rectangle rect = new Rectangle(0, 0, HealthBarGumpCustom.HPB_WIDTH, HealthBarGumpCustom.HPB_HEIGHT_SINGLELINE);
                UIManager.Add(new HealthBarGumpCustom(entity) { X = ProfileManager.CurrentProfile.PullFriendlyBarsFinalLocation.X - (rect.Width >> 1), Y = ProfileManager.CurrentProfile.PullFriendlyBarsFinalLocation.Y + (36 * bar) });
                break;
            }
        }

        public static void GrabEnemyBars()
        {
            foreach (KeyValuePair<uint, Mobile> mkv in World.Mobiles)
            {
                Mobile mobile = mkv.Value;

                if (mobile.Distance >= 18)
                {
                    continue;
                }

                if (mobile.Name == null || mobile.Name.Length == 0)
                {
                    return;
                }

                if ((mobile.NotorietyFlag != NotorietyFlag.Criminal && mobile.NotorietyFlag != NotorietyFlag.Enemy && mobile.NotorietyFlag != NotorietyFlag.Gray && mobile.NotorietyFlag != NotorietyFlag.Murderer) || (World.Party.Leader == mobile && (World.Party.Members.Length == 0 || World.Party.Contains(mobile))) || mobile == World.Player || mobile.NotorietyFlag == NotorietyFlag.Invulnerable)
                {
                    continue;
                }

                Entity entity = World.Get(mobile.Serial);
                Point offset = ProfileManager.CurrentProfile.PullEnemyBars;

                if (Math.Abs(offset.X) <= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS && Math.Abs(offset.Y) <= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS)
                {
                    continue;
                }

                GameActions.RequestMobileStatus(entity.Serial);
                HealthBarGumpCustom customgump = UIManager.GetGump<HealthBarGumpCustom>(entity.Serial);

                if (customgump != null)
                {
                    customgump.Dispose();
                }

                if (entity.Serial == World.Player)
                {
                    StatusGumpBase.GetStatusGump()?.Dispose();
                }

                int bar = UIManager.CountHealthBarGumpCustom();
                Rectangle rect = new Rectangle(0, 0, HealthBarGumpCustom.HPB_WIDTH, HealthBarGumpCustom.HPB_HEIGHT_SINGLELINE);
                UIManager.Add(new HealthBarGumpCustom(entity) { X = ProfileManager.CurrentProfile.PullEnemyBarsFinalLocation.X - (rect.Width >> 1), Y = ProfileManager.CurrentProfile.PullEnemyBarsFinalLocation.Y + (36 * bar) });
                break;
            }
        }

        public static void GrabPartyAllyBars()
        {
            foreach (KeyValuePair<uint, Mobile> mkv in World.Mobiles)
            {
                Mobile mobile = mkv.Value;

                if (mobile.Distance >= 18)
                {
                    continue;
                }

                if (mobile.Name == null || mobile.Name.Length == 0)
                {
                    return;
                }

                if ((mobile.NotorietyFlag != NotorietyFlag.Ally && World.Party.Leader != mobile && (World.Party.Members.Length == 0 || !World.Party.Contains(mobile))) || mobile == World.Player || mobile.NotorietyFlag == NotorietyFlag.Invulnerable)
                {
                    continue;
                }

                Entity entity = World.Get(mobile.Serial);
                Point offset = ProfileManager.CurrentProfile.PullPartyAllyBars;

                if (Math.Abs(offset.X) <= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS && Math.Abs(offset.Y) <= Constants.MIN_PICKUP_DRAG_DISTANCE_PIXELS)
                {
                    continue;
                }

                GameActions.RequestMobileStatus(entity.Serial);
                HealthBarGumpCustom customgump = UIManager.GetGump<HealthBarGumpCustom>(entity.Serial);

                if (customgump != null)
                {
                    customgump.Dispose();
                }

                if (entity.Serial == World.Player)
                {
                    StatusGumpBase.GetStatusGump()?.Dispose();
                }

                int bar = UIManager.CountHealthBarGumpCustom();
                Rectangle rect = new Rectangle(0, 0, HealthBarGumpCustom.HPB_WIDTH, HealthBarGumpCustom.HPB_HEIGHT_SINGLELINE);
                UIManager.Add(new HealthBarGumpCustom(entity) { X = ProfileManager.CurrentProfile.PullPartyAllyBarsFinalLocation.X - (rect.Width >> 1), Y = ProfileManager.CurrentProfile.PullPartyAllyBarsFinalLocation.Y + (36 * bar) });
                break;
            }
        }
    }
}
