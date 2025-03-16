using System.Collections.Generic;

namespace Assets.Scripts.Player
{
    public enum BodyPartType
    {
        Head,
        Chest,
        Stomach,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    public enum BleedingType
    {
        Light,
        Heavy
    }

    public abstract class Bleeding
    {
        public BleedingType Type { get; protected set; }
        public float BleedRate { get; protected set; }
        public float BleedDamage { get; protected set; }
    }

    public class LightBleeding : Bleeding
    {
        public LightBleeding()
        {
            Type = BleedingType.Light;
            BleedRate = 0.5f;
            BleedDamage = 1.0f;
        }
    }

    public class HeavyBleeding : Bleeding
    {
        public HeavyBleeding()
        {
            Type = BleedingType.Heavy;
            BleedRate = 1.0f;
            BleedDamage = 2.0f;
        }
    }

    public class BodyPart
    {
        public BodyPartType Type { get; set; }
        public float MaxHealth { get; set; }
        public float CurrentHealth { get; set; }
        public bool IsDestroyed { get; set; }
        public bool IsBroken 
        { 
            get => _isBroken; 
            set
            {
                if (!CanBeBroken) _isBroken = false;
                _isBroken = value;
            }
        }

        public readonly bool CanBeBroken;

        public List<Bleeding> Bleedings = new();

        private bool _isBroken;

        public BodyPart(BodyPartType type, float maxHealth, bool canBeBroken)
        {
            this.Type = type;
            this.MaxHealth = maxHealth;
            this.CurrentHealth = maxHealth;
            this.IsDestroyed = false;
            this.CanBeBroken = canBeBroken;
        }

        public void TakeDamage(float damage)
        {
            CurrentHealth -= damage;
            if (CurrentHealth <= 0.0f)
            {
                IsDestroyed = true;
                CurrentHealth = 0.0f;
            }
        }
        public void Heal(float healAmount)
        {
            CurrentHealth += healAmount;
            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        }
    }
}