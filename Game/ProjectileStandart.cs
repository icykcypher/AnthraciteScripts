using Assets.Scripts.GamePlay;
using Assets.Scripts.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BodyPart = Assets.Scripts.Player.BodyPart;

namespace Assets.Scripts.Game
{
    public class ProjectileStandard : ProjectileBase
    {
        [Header("General")]
        public float Radius = 0.01f;
        public Transform Root;
        public Transform Tip;
        public float MaxLifeTime = 5f;
        public GameObject ImpactVfx;
        public float ImpactVfxLifetime = 5f;
        public float ImpactVfxSpawnOffset = 0.1f;
        public AudioClip ImpactSfxClip;
        public LayerMask HittableLayers = -1;

        [Header("Movement")]
        public float Speed = 20f;
        public float GravityDownAcceleration = 0f;
        public float TrajectoryCorrectionDistance = -1;
        public bool InheritWeaponVelocity = false;

        [Header("Damage")]
        public float Damage = 40f;
        public float BrokenPartProbability = 0.1f;
        public DamageArea AreaOfDamage;

        [Header("Debug")]
        public Color RadiusColor = Color.cyan * 0.2f;

        // Internal states
        private ProjectileBase m_ProjectileBase;
        private Vector3 m_LastRootPosition;
        private Vector3 m_Velocity;
        private bool m_HasTrajectoryOverride;
        private Vector3 m_TrajectoryCorrectionVector;
        private Vector3 m_ConsumedTrajectoryCorrectionVector;
        private List<Collider> m_IgnoredColliders;
        private Rigidbody m_Rigidbody;
        private readonly List<BodyPartType> _bodyPartTypes = new()
        { 
            BodyPartType.Head, BodyPartType.Chest, BodyPartType.Stomach, 
            BodyPartType.LeftArm, BodyPartType.RightArm, BodyPartType.LeftLeg, 
            BodyPartType.RightLeg 
        };

        public BodyPartType HittedBodyPart { get; set; }

        private const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void Start()
        {
            // Исключаем слой Ignore Raycast из маски слоев
            HittableLayers &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

            m_ProjectileBase = GetComponent<ProjectileBase>();
            if (m_ProjectileBase == null)
            {
                Debug.LogError("ProjectileBase is missing!", this);
                return;
            }

            m_ProjectileBase.OnShoot += OnShoot;
            Destroy(gameObject, MaxLifeTime); // Self destruction after max lifetime

            // Get the Rigidbody component
            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody == null)
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            // Set Rigidbody properties
            m_Rigidbody.useGravity = false; // Gravity will be applied manually
            m_Rigidbody.isKinematic = false;
        }

        void OnEnable()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            if (m_ProjectileBase == null)
            {
                Debug.LogError("ProjectileBase is missing!", this);
                return;
            }

            m_ProjectileBase.OnShoot += OnShoot;
            Destroy(gameObject, MaxLifeTime); // Self destruction after max lifetime

            // Get the Rigidbody component
            m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Rigidbody == null)
            {
                m_Rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            // Set Rigidbody properties
            m_Rigidbody.useGravity = false; // Gravity will be applied manually
            m_Rigidbody.isKinematic = false;
        }


        new void OnShoot()
        {
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders = new List<Collider>();
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Ignore colliders of the owner
            Collider[] ownerColliders = m_ProjectileBase.Owner.GetComponentsInChildren<Collider>();
            m_IgnoredColliders.AddRange(ownerColliders);

            HandlePlayerWeaponTrajectory();

            // Apply initial velocity to Rigidbody
            m_Rigidbody.linearVelocity = m_Velocity;
        }

        void HandlePlayerWeaponTrajectory()
        {
            PlayerWeaponsManager playerWeaponsManager = m_ProjectileBase.Owner.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager == null) return;

            m_HasTrajectoryOverride = true;
            Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition - playerWeaponsManager.WeaponCamera.transform.position);
            m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle, playerWeaponsManager.WeaponCamera.transform.forward);

            if (TrajectoryCorrectionDistance == 0)
            {
                transform.position += m_TrajectoryCorrectionVector;
                m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
            }
            else if (TrajectoryCorrectionDistance < 0)
            {
                m_HasTrajectoryOverride = false;
            }

            if (Physics.Raycast(playerWeaponsManager.WeaponCamera.transform.position, cameraToMuzzle.normalized,
                out RaycastHit hit, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
            {
                if (IsHitValid(hit)) OnHit(hit.point, hit.normal, hit.collider);
            }
        }

        void Update()
        {
            MoveProjectile();
            ApplyGravity();
            HandleTrajectoryOverride();
            DetectHits();
        }

        void MoveProjectile()
        {
            // Apply the calculated velocity
            m_Rigidbody.linearVelocity = m_Velocity;

            // Orient towards velocity direction
            if (m_Rigidbody.linearVelocity.sqrMagnitude > 0.1f) // Avoid div-by-zero error
                transform.forward = m_Rigidbody.linearVelocity.normalized;
        }

        void ApplyGravity()
        {
            if (GravityDownAcceleration > 0)
            {
                m_Rigidbody.linearVelocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;
            }
        }

        void HandleTrajectoryOverride()
        {
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude < m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame = (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                {
                    m_HasTrajectoryOverride = false;
                }

                transform.position += correctionThisFrame;
            }
        }

        void DetectHits()
        {
            RaycastHit closestHit = new RaycastHit { distance = Mathf.Infinity };
            bool foundHit = false;

            // Sphere cast to detect collisions
            Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
            RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius, displacementSinceLastFrame.normalized,
                displacementSinceLastFrame.magnitude, HittableLayers, k_TriggerInteraction);

            foreach (var hit in hits)
            {
                if (IsHitValid(hit) && hit.distance < closestHit.distance)
                {
                    foundHit = true;
                    closestHit = hit;
                }
            }

            if (foundHit)
            {
                if (closestHit.distance <= 0f)
                {
                    closestHit.point = Root.position;
                    closestHit.normal = -transform.forward;
                }

                OnHit(closestHit.point, closestHit.normal, closestHit.collider);
            }
        }

        bool IsHitValid(RaycastHit hit)
        {
            // Ignore hits with an IgnoreHitDetection component
            if (hit.collider.GetComponent<IgnoreHitDetection>()) return false;

            // Ignore hits with triggers that don't have a Damageable component
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null) return false;

            // Ignore hits with specific ignored colliders (self colliders, by default)
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider)) return false;

            return true;
        }

        void OnHit(Vector3 point, Vector3 normal, Collider collider)
        {
            // Apply damage
            ApplyDamage(collider);

            // Impact effects (VFX and SFX)
            SpawnImpactEffects(point, normal);

            // Destroy the projectile after the impact
            Destroy(gameObject);
        }

        void ApplyDamage(Collider collider)
        {
            if (AreaOfDamage)
            {
                AreaOfDamage.InflictDamageInArea(Damage, Root.position, HittableLayers, k_TriggerInteraction, m_ProjectileBase.Owner);
            }
            else
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable)
                {
                    damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
                }
            }
        }

        void SpawnImpactEffects(Vector3 point, Vector3 normal)
        {
            if (ImpactVfx)
            {
                GameObject impactVfxInstance = Instantiate(ImpactVfx, point + (normal * ImpactVfxSpawnOffset), Quaternion.LookRotation(normal));
                if (ImpactVfxLifetime > 0)
                {
                    Destroy(impactVfxInstance.gameObject, ImpactVfxLifetime);
                }
            }

            if (ImpactSfxClip)
            {
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var colliderName = collision.collider.name;

            if (Enum.TryParse(colliderName, true, out BodyPartType bodyPart))
            {
                HittedBodyPart = bodyPart;
            }

            Destroy(gameObject);
        }
    }
}