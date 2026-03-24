using System;
using ClassicUO.Dust765.Shared;
using ClassicUO.Dust765.Lobby.Networking;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Utility;

namespace ClassicUO.Dust765.Autos
{
    internal class AutoLobbyStealthPosition
    {
        public static bool IsEnabled { get; set; }
        private static uint _nextCheckTick;

        //##AutoLobbyStealthPosition Toggle##//
        public static void Toggle()
        {
            GameActions.Print(String.Format("Auto LobbyStealthPosition:{0}abled", (IsEnabled = !IsEnabled) == true ? "En" : "Dis"), 70);
        }

        //##Register Command and Perform Checks##//
        public static void Initialize()
        {
            CommandManager.Register("autohid", args => Toggle());
        }

        //##Default AutoLobbyStealthPosition Status on GameLoad##//
        static AutoLobbyStealthPosition()
        {
            IsEnabled = false;
        }

        //##Perform AutoLobbyStealthPosition Update on Toggle/Player Death##//
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
            _nextCheckTick = now + 2500;

            if (Lobby.Lobby._netState == null || !Lobby.Lobby._netState.IsOpen)
            {
                return;
            }

            if (World.Player.IsHidden)
            {
                SendPacketToLobby();
            }
        }

        //##Perform SendPacketToLobby##//
        private static void SendPacketToLobby()
        {
            string posXY = (World.Player.X.ToString() + "," + World.Player.Y.ToString() + "," + World.Player.Z.ToString());
            Lobby.Lobby._netState.Send(new PHiddenPosition((posXY)));
        }

        //##Perform Message##//
        public static void Print(string message, MessageColor color = MessageColor.Default)
        {
            GameActions.Print(message, (ushort)color, MessageType.System, 1);
        }
    }
}