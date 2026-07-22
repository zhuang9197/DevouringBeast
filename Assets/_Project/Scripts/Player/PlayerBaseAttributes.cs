using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// A character's starting attributes plus flat upgrades. Keeping these values on one
    /// component lets future playable characters share the combat code with different bases.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerBaseAttributes : MonoBehaviour
    {
        [Header("Character Starting Attributes")]
        [SerializeField, Min(0f)] private float initialMoveSpeed = 8f;
        [SerializeField, Min(0f)] private float initialSuction = 100f;
        [SerializeField, Min(0f)] private float initialEnergyBallDamage = 25f;

        public float BonusMoveSpeed { get; set; }
        public float BonusSuction { get; set; }
        public float BonusEnergyBallBaseDamage { get; set; }

        public float MoveSpeed => Mathf.Max(0f, initialMoveSpeed + BonusMoveSpeed);
        public float Suction => Mathf.Max(0f, initialSuction + BonusSuction);
        public float EnergyBallBaseDamage =>
            Mathf.Max(0f, initialEnergyBallDamage + BonusEnergyBallBaseDamage);

        public float InitialMoveSpeed
        {
            get => initialMoveSpeed;
            set => initialMoveSpeed = Mathf.Max(0f, value);
        }

        public float InitialSuction
        {
            get => initialSuction;
            set => initialSuction = Mathf.Max(0f, value);
        }

        public float InitialEnergyBallDamage
        {
            get => initialEnergyBallDamage;
            set => initialEnergyBallDamage = Mathf.Max(0f, value);
        }
    }
}
