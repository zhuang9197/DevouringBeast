using UnityEngine;

namespace DevouringBeast
{
    public enum FoodKind
    {
        RiceBall,
        Baozi,
        HotDog,
        Sushi
    }

    [DisallowMultipleComponent]
    public sealed class FoodItem : MonoBehaviour
    {
        public FoodKind Kind { get; private set; }
        public float BaseMass { get; private set; }

        public void Configure(FoodKind kind, float baseMass)
        {
            Kind = kind;
            BaseMass = Mathf.Max(0f, baseMass);

            InhaleableItem item = GetComponent<InhaleableItem>();
            if (item == null) return;
            item.Mass = BaseMass;
            item.DeadInhaleThreshold = 0f;
            item.IsAlive = false;
            item.IgnoreSuctionThreshold = true;
        }

        public float Consume(PlayerHealth health, PlayerController controller, RogueSkillManager skills)
        {
            int chefLevel = skills != null ? skills.GetLevel(RogueSkillId.Chef) : 0;
            float mass = BaseMass + (chefLevel > 0 ? 4f + chefLevel : 0f);

            if (Kind == FoodKind.HotDog)
            {
                int level = skills != null ? skills.GetLevel(RogueSkillId.HotDogLover) : 0;
                if (level > 0)
                    controller?.ApplyFoodSpeedBoost(0.25f + level * 0.05f, 5f);
            }
            else if (Kind == FoodKind.Sushi)
            {
                int level = skills != null ? skills.GetLevel(RogueSkillId.SushiMaster) : 0;
                if (level > 0 && health != null)
                {
                    float healChance = 0.45f + level * 0.05f;
                    float maxHealthChance = 0.07f + level * 0.03f;
                    if (Random.value < healChance) health.Heal(1);
                    if (Random.value < maxHealthChance) health.IncreaseMaxHealth(1);
                }
            }

            return mass;
        }
    }
}
