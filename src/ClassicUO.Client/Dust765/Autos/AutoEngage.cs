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
using ClassicUO.Dust765.Shared;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using System;
using ClassicUO.Utility;

namespace ClassicUO.Dust765.Autos
{
    internal class AutoEngage
    {
        public static bool IsEnabled { get; set; }
        private static uint _nextCheckTick;
        private static uint _nextActionTick;

        public static Entity _followingtarget;

        //##Auto Engage Toggle##//
        public static void Toggle()
        {
            GameActions.Print(String.Format("Auto Engage Is:{0}abled", (IsEnabled = !IsEnabled) == true ? "En" : "Dis"), 70);
        }

        //##Register Command and Perform Checks##//
        public static void Initialize()
        {
            CommandManager.Register("engage", args => Toggle());
        }

        //##Default AutoEngage Status on GameLoad##//
        static AutoEngage()
        {
            IsEnabled = false;
        }

        //##Perform AutoEngage Update on Toggle/Player Death##//
        public static void Update()
        {
            if (!IsEnabled || World.Player == null || World.Player.IsDead)
            {
                return;
            }

            uint now = Time.Ticks;
            if (now < _nextCheckTick || now < _nextActionTick)
            {
                return;
            }
            _nextCheckTick = now + 125;

            var target = World.Get(TargetManager.LastTargetInfo.Serial);
            if (target == null)
            {
                return;
            }

            _followingtarget = target;

            if (_followingtarget.IsDestroyed || _followingtarget.Hits == 0)
            {
                return;
            }

            if (TargetManager.LastAttack != _followingtarget.Serial)
            {
                GameActions.Print("Attacking Target!", 101, MessageType.System);
                GameActions.Attack(_followingtarget.Serial);
                _nextActionTick = now + 250;
                return;
            }

            if (target.Hits <= target.HitsMax && target.Distance < 12 && target.Distance > 1)
            {
                GameActions.Print("Pathfinding to Target for 2 secs!", 101, MessageType.System);
                Pathfinder.WalkTo(_followingtarget.X, _followingtarget.Y, _followingtarget.Z, 0);
                _nextActionTick = now + 2000;
                return;
            }

            if (target.Hits <= target.HitsMax && target.Distance >= 12 && target.Distance <= 30)
            {
                Print("Distance to LastTarget too far: " + target.Distance + " tiles, get closer!");
                _nextActionTick = now + 2000;
            }
        }

        //##Perform Message##//
        public static void Print(string message, MessageColor color = MessageColor.Default)
        {
            GameActions.Print(message, (ushort) color, MessageType.System, 1);
        }
    }
}
