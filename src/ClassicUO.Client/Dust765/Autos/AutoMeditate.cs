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
using ClassicUO.Dust765.Shared;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Utility;

namespace ClassicUO.Dust765.Autos
{
    internal class AutoMeditate
    {
        public static bool IsEnabled { get; set; }
        private static uint _nextCheckTick;
        private static uint _nextMeditateTick;

		//##AutoMeditate Toggle##//
        public static void Toggle()
        {
            GameActions.Print(String.Format("Auto Meditate:{0}abled", (IsEnabled = !IsEnabled) == true ? "En" : "Dis"), 70);
        }
		
		//##Register Command and Perform Checks##//
        public static void Initialize()
        {
            CommandManager.Register("automed", args => Toggle());
        }
		
		//##Default AutoMeditate Status on GameLoad##//
        static AutoMeditate()
        {
            IsEnabled = false;
        }
		
		//##Perform AutoMeditate Update on Toggle/Player Death##//
        public static void Update()
        {
            if (!IsEnabled || World.Player == null || World.Player.IsDead)
            {
                return;
            }

            uint now = Time.Ticks;
            if (now < _nextCheckTick)
            {
                return;
            }

            _nextCheckTick = now + 250;

            if (now < _nextMeditateTick)
            {
                return;
            }

            if (
                World.Player.Steps.Count == 0
                && World.Player.Mana < World.Player.ManaMax
                && !TargetManager.IsTargeting
            )
            {
                GameActions.Print("Auto Meditating!", 70, MessageType.System);
                GameActions.UseSkill(46);
                _nextMeditateTick = now + 15000;
            }
        }
		
		//##Perform Message##//
        public static void Print(string message, MessageColor color = MessageColor.Default)
        {
            GameActions.Print(message, (ushort) color, MessageType.System, 1);
        }
    }
}