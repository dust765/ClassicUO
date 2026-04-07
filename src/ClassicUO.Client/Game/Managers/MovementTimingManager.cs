using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    internal static class MovementTimingManager
    {
        private const int MinDelay = 20;
        private const int MaxDelay = 1000;

        public static int TurnDelay => Clamp(ProfileManager.CurrentProfile?.MovementTurnDelay ?? Constants.TURN_DELAY);
        public static int TurnDelayFast => Clamp(ProfileManager.CurrentProfile?.MovementTurnDelayFast ?? Constants.TURN_DELAY_FAST);
        public static int WalkingDelay => Clamp(ProfileManager.CurrentProfile?.MovementWalkingDelay ?? Constants.WALKING_DELAY);
        public static int PlayerWalkingDelay => Clamp(ProfileManager.CurrentProfile?.MovementPlayerWalkingDelay ?? Constants.PLAYER_WALKING_DELAY);

        private static int Clamp(int value)
        {
            if (value < MinDelay)
            {
                return MinDelay;
            }

            if (value > MaxDelay)
            {
                return MaxDelay;
            }

            return value;
        }
    }
}
