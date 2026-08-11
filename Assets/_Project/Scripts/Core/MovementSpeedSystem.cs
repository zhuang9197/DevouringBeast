using UnityEngine;

namespace DevouringBeast
{
    /// <summary>Converts normalized movement values into world units per second.</summary>
    public static class MovementSpeedSystem
    {
        private static float _playerSpeedUnit = 1f;

        public static float PlayerSpeedUnit => Mathf.Max(0.01f, _playerSpeedUnit);

        public static void SetPlayerSpeedUnit(float worldSpeed)
        {
            _playerSpeedUnit = Mathf.Max(0.01f, worldSpeed);
        }

        public static float EnemyToWorld(float normalizedSpeed)
        {
            float limit = GameBalance.Current != null
                ? GameBalance.Current.Enemy.normalizedSpeedLimit
                : normalizedSpeed;
            return Mathf.Clamp(normalizedSpeed, 0f, limit) * PlayerSpeedUnit;
        }

        public static float PlayerToNormalized(float worldSpeed)
        {
            return Mathf.Max(0f, worldSpeed) / PlayerSpeedUnit;
        }
    }
}
