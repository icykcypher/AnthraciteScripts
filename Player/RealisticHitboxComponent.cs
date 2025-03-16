using UnityEngine;
using Assets.Scripts.Game;
using System.Collections.Generic;

namespace Assets.Scripts.Player
{
    public class RealisticHitboxComponent : MonoBehaviour
    {
        public Collider HeadCollider { get; private set; }
        public Collider ChestCollider { get; private set; }
        public Collider StomachCollider { get; private set; }
        public Collider LeftArmCollider { get; private set; }
        public Collider RightArmCollider { get; private set; }
        public Collider LeftLegCollider { get; private set; }
        public Collider RightLegCollider { get; private set; }

        private Dictionary<BodyPartType, BodyPart> bodyParts = new();

        public float HeadDamageMultiplier = 2.0f;
        public float ChestDamageMultiplier = 1.0f;
        public float StomachDamageMultiplier = 1.0f;
        public float ArmDamageMultiplier = 0.75f;
        public float LegDamageMultiplier = 0.75f;

        public float HeadMaxHealth = 35.0f;
        public float ChestMaxHealth = 85.0f;
        public float StomachMaxHealth = 70.0f;
        public float ArmMaxHealth = 60.0f;
        public float LegMaxHealth = 65.0f;

        // Use this for initialization
        void Start()
        {
            // Инициализация частей тела
            InitializeBodyPart(BodyPartType.Head, HeadMaxHealth, false);
            InitializeBodyPart(BodyPartType.Chest, ChestMaxHealth, false);
            InitializeBodyPart(BodyPartType.Stomach, StomachMaxHealth, false);
            InitializeBodyPart(BodyPartType.LeftArm, ArmMaxHealth, true);
            InitializeBodyPart(BodyPartType.RightArm, ArmMaxHealth, true);
            InitializeBodyPart(BodyPartType.LeftLeg, LegMaxHealth, true);
            InitializeBodyPart(BodyPartType.RightLeg, LegMaxHealth, true);

            // Функция поиска во всех вложенных объектах
            Collider FindCollider(string name)
            {
                Transform partTransform = FindDeepChild(transform, name);
                if (partTransform == null)
                {
                    return null;
                }
                Collider collider = partTransform.GetComponent<Collider>();
                return collider;
            }

            HeadCollider = FindCollider("head");
            ChestCollider = FindCollider("chest");
            StomachCollider = FindCollider("stomach");
            LeftArmCollider = FindCollider("leftarm");
            RightArmCollider = FindCollider("rightarm");
            LeftLegCollider = FindCollider("leftleg");
            RightLegCollider = FindCollider("rightleg");
        }


        public static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals("Armature", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform result = FindDeepChild(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }



        void OnCollisionEnter(Collision collision)
        {
            // Проверяем, если объект имеет тег "Bullet"
            if (collision.gameObject.CompareTag("Bullet"))
            {
                // Получаем коллайдер, с которым произошло столкновение
                Collider collidedCollider = collision.collider;

                // Получаем компонент ProjectileStandard из объекта, с которым произошло столкновение
                var projectile = collision.gameObject.GetComponent<ProjectileStandard>();

                if (projectile == null)
                {
                    return;
                }

                if (projectile != null)
                {
                    // Если компонент найден, извлекаем урон
                    float damage = projectile.Damage;

                    // Определяем, с какой частью тела произошел контакт
                    BodyPartType? bodyPartType = collidedCollider.GetComponent<ProjectileStandard>().HittedBodyPart;

                    if (bodyPartType.HasValue)
                    {
                        // Наносим урон части тела
                        TakeDamage(bodyPartType.Value, damage);
                        Debug.Log($"Hit body part: {bodyPartType.Value}");
                    }
                    else
                    {
                        Debug.Log("Unknown body part collider hit");
                    }
                }
            }
        }

       
        // Метод для нанесения урона части тела
        public void TakeDamage(BodyPartType bodyPart, float damage)
        {
            if (bodyParts.ContainsKey(bodyPart))
            {
                var part = bodyParts[bodyPart];
                part.TakeDamage(damage);
            }
        }


        // Инициализация частей тела с максимальным здоровьем
        private void InitializeBodyPart(BodyPartType type, float maxHealth, bool canBeBroken)
        {
            var bodyPart = new BodyPart
            (
                type,
                maxHealth,
                canBeBroken
            );
            bodyParts[type] = bodyPart;
        }

        // Метод для получения текущего здоровья всех частей тела
        public float GetCurrentHp()
        {
            float totalHealth = 0;
            foreach (var part in bodyParts.Values)
            {
                totalHealth += part.CurrentHealth;
            }
            return totalHealth;
        }

        // Метод для проверки, уничтожена ли часть тела
        public bool IsBodyPartDestroyed(BodyPartType bodyPart)
        {
            return bodyParts.ContainsKey(bodyPart) && bodyParts[bodyPart].IsDestroyed;
        }

        // Метод для лечения части тела
        public void Heal(BodyPartType bodyPart, float healAmount)
        {
            if (bodyParts.ContainsKey(bodyPart))
            {
                bodyParts[bodyPart].Heal(healAmount);
            }
            else
            {
                Debug.LogWarning($"Unknown body part for healing: {bodyPart}");
            }
        }

        // Метод для получения коллайдера части тела
        public Collider GetCollider(BodyPartType bodyPart)
        {
            switch (bodyPart)
            {
                case BodyPartType.Head:
                    return transform.Find("head").GetComponent<Collider>();
                case BodyPartType.Chest:
                    return transform.Find("chest").GetComponent<Collider>();
                case BodyPartType.Stomach:
                    return transform.Find("stomach").GetComponent<Collider>();
                case BodyPartType.LeftArm:
                    return transform.Find("leftarm").GetComponent<Collider>();
                case BodyPartType.RightArm:
                    return transform.Find("rightarm").GetComponent<Collider>();
                case BodyPartType.LeftLeg:
                    return transform.Find("leftleg").GetComponent<Collider>();
                case BodyPartType.RightLeg:
                    return transform.Find("rightleg").GetComponent<Collider>();
                default:
                    Debug.LogWarning("Unknown body part for collider");
                    return null;
            }
        }

        // Метод для обработки кровотечений
        public void ApplyBleeding(BodyPartType bodyPart, Bleeding bleeding)
        {
            if (bodyParts.ContainsKey(bodyPart))
            {
                bodyParts[bodyPart].Bleedings.Add(bleeding);
            }
            else
            {
                Debug.LogWarning($"Unknown body part for bleeding: {bodyPart}");
            }
        }

        // Метод для обновления кровотечений (например, каждый кадр или по таймеру)
        public void UpdateBleeding(float deltaTime)
        {
            foreach (var part in bodyParts.Values)
            {
                foreach (var bleeding in part.Bleedings)
                {
                    part.CurrentHealth -= bleeding.BleedDamage * deltaTime;
                    if (part.CurrentHealth <= 0)
                    {
                        part.IsDestroyed = true;
                        part.CurrentHealth = 0;
                    }
                }
            }
        }
    }
}