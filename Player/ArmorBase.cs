namespace Assets.Scripts.Player
{
    public enum ArmorType
    {
        Helmet,
        Vest,
    }

    public abstract class ArmorBase
    {
        public ArmorType Type { get; protected set; }
        public float DamageReduction { get; protected set; }
        public float Durability { get; protected set; }
        public float MaxDurability { get; protected set; }
        public void TakeDamage(float damage)
        {
            Durability -= damage;
        }
    }

    public class Helmet : ArmorBase
    {
        public Helmet()
        {
            Type = ArmorType.Helmet;
            DamageReduction = 0.5f;
            Durability = 100.0f;
            MaxDurability = 100.0f;
        }
    }

    public class Vest : ArmorBase
    {
        public Vest()
        {
            Type = ArmorType.Vest;
            DamageReduction = 0.7f;
            Durability = 100.0f;
            MaxDurability = 100.0f;
        }
    }
}