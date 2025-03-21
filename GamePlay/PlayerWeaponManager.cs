﻿using Anthracite.Game;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine;
using Assets.Scripts.Game;
using Assets.Scripts.Player;
using TMPro;
using System;

namespace Assets.Scripts.GamePlay
{
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerWeaponsManager : MonoBehaviour
    {
        public enum WeaponSwitchState
        {
            Up,
            Down,
            PutDownPrevious,
            PutUpNew,
        }

        [Tooltip("List of weapon the player will start with")]
        public List<WeaponController> StartingWeapons = new List<WeaponController>();

        [Header("References")]
        [Tooltip("Secondary camera used to avoid seeing weapon go throw geometries")]
        public Camera WeaponCamera;

        [Tooltip("Parent transform where all weapon will be added in the hierarchy")]
        public Transform WeaponParentSocket;

        [Tooltip("Position for weapons when active but not actively aiming")]
        public Transform StandingDefaultWeaponPosition;

        [Tooltip("Position for weapons when active but not actively aiming")]
        public Transform CrouchingDefaultWeaponPosition;

        [Tooltip("Position for weapons when aiming")]
        public Transform StandingAimingWeaponPosition;


        [Tooltip("Position for weapons when aiming")]
        public Transform CrouchingAimingWeaponPosition;

        [Tooltip("Position for innactive weapons")]
        public Transform DownWeaponPosition;

        [Header("Weapon Bob")]
        [Tooltip("Frequency at which the weapon will move around in the screen when the player is in movement")]
        public float BobFrequency = 10f;

        [Tooltip("How fast the weapon bob is applied, the bigger value the fastest")]
        public float BobSharpness = 10f;

        [Tooltip("Distance the weapon bobs when not aiming")]
        public float DefaultBobAmount = 0.05f;

        [Tooltip("Distance the weapon bobs when aiming")]
        public float AimingBobAmount = 0.02f;

        [Header("Weapon Recoil")]
        [Tooltip("This will affect how fast the recoil moves the weapon, the bigger the value, the fastest")]
        public float RecoilSharpness = 50f;

        [Tooltip("Maximum distance the recoil can affect the weapon")]
        public float MaxRecoilDistance = 0.5f;

        [Tooltip("How fast the weapon goes back to it's original position after the recoil is finished")]
        public float RecoilRestitutionSharpness = 10f;

        [Header("Misc")]
        [Tooltip("Speed at which the aiming animatoin is played")]
        public float AimingAnimationSpeed = 10f;

        [Tooltip("Field of view when not aiming")]
        public float DefaultFov = 60f;

        [Tooltip("Portion of the regular FOV to apply to the weapon camera")]
        public float WeaponFovMultiplier = 1f;

        [Tooltip("Delay before switching weapon a second time, to avoid recieving multiple inputs from mouse wheel")]
        public float WeaponSwitchDelay = 1f;

        [Tooltip("Layer to set FPS weapon gameObjects to")]
        public LayerMask FpsWeaponLayer;

        public bool IsAiming { get; private set; }
        public bool IsPointingAtEnemy { get; private set; }
        public int ActiveWeaponIndex { get; private set; }

        public UnityAction<WeaponController> OnSwitchedToWeapon;
        public UnityAction<WeaponController, int> OnAddedWeapon;
        public UnityAction<WeaponController, int> OnRemovedWeapon;

        WeaponController[] m_WeaponSlots = new WeaponController[9]; // 9 available weapon slots
        PlayerInputHandler m_InputHandler;
        PlayerCharacterController m_PlayerCharacterController;
        float m_WeaponBobFactor;
        Vector3 m_LastCharacterPosition;
        Vector3 m_WeaponMainLocalPosition;
        Vector3 m_WeaponBobLocalPosition;
        Vector3 m_WeaponRecoilLocalPosition;
        Vector3 m_AccumulatedRecoil;
        float m_TimeStartedWeaponSwitch;
        WeaponSwitchState m_WeaponSwitchState;
        int m_WeaponSwitchNewWeaponIndex;

        void Start()
        {
            ActiveWeaponIndex = -1;
            m_WeaponSwitchState = WeaponSwitchState.Down;

            m_InputHandler = GetComponent<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, PlayerWeaponsManager>(m_InputHandler, this,
                gameObject);

            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerWeaponsManager>(
                m_PlayerCharacterController, this, gameObject);

            SetFov(DefaultFov);

            OnSwitchedToWeapon += OnWeaponSwitched;

            // Add starting weapons
            foreach (var weapon in StartingWeapons)
            {
                AddWeapon(weapon);
            }

            SwitchWeapon(true);

            WeaponParentSocket.localPosition = m_WeaponMainLocalPosition;
        }

        void Update()
        {
            if (Input.GetButtonDown("Interaction"))
            {
                Debug.Log("Trying to pick up weapon");
                TryPickupWeapon();
            }

            // Получаем активное оружие
            WeaponController activeWeapon = GetActiveWeapon();

            if (activeWeapon == null || activeWeapon.IsReloading)
                return;


            if (m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                if (!activeWeapon.AutomaticReload && m_InputHandler.GetReloadButtonDown() && activeWeapon.CurrentAmmoRatio < 1.0f)
                {
                    IsAiming = false;
                    activeWeapon.IsAiming = IsAiming;
                    activeWeapon.StartReloadAnimation();
                    return;
                }
                // handle aiming down sights
                IsAiming = m_InputHandler.GetAimInputHeld();
                activeWeapon.IsAiming = IsAiming;

                // handle shooting
                bool hasFired = activeWeapon.HandleShootInputs(
                    m_InputHandler.GetFireInputDown(),
                    m_InputHandler.GetFireInputHeld(),
                    m_InputHandler.GetFireInputReleased());

                // Handle accumulating recoil
                if (hasFired)
                {
                    m_AccumulatedRecoil += Vector3.back * activeWeapon.RecoilForce;
                    m_AccumulatedRecoil = Vector3.ClampMagnitude(m_AccumulatedRecoil, MaxRecoilDistance);
                }
            }

            // weapon switch handling
            if (!IsAiming && (activeWeapon == null || !activeWeapon.IsCharging) &&
               (m_WeaponSwitchState == WeaponSwitchState.Up || m_WeaponSwitchState == WeaponSwitchState.Down))
            {
                int switchWeaponInput = m_InputHandler.GetSwitchWeaponInput();
                if (switchWeaponInput != 0)
                {
                    bool switchUp = switchWeaponInput > 0;
                    SwitchWeapon(switchUp);
                }
                else
                {
                    switchWeaponInput = m_InputHandler.GetSelectWeaponInput();
                    if (switchWeaponInput != 0)
                    {
                        if (GetWeaponAtSlotIndex(switchWeaponInput - 1) != null)
                            SwitchToWeaponIndex(switchWeaponInput - 1);
                    }
                }
            }

        }

        // REPAIR
        void UpdateAiming()
        {
            WeaponController activeWeapon = GetActiveWeapon();
            if (activeWeapon == null) return;

            // Получаем направление камеры
            Vector3 aimDirection = m_PlayerCharacterController.PlayerCamera.transform.forward;

            // Получаем вертикальный угол камеры
            float cameraVerticalAngle = m_PlayerCharacterController.PlayerCamera.transform.eulerAngles.x;
            if (cameraVerticalAngle > 180) cameraVerticalAngle -= 360; // Корректировка угла

            if (IsAiming) // Прицеливание
            {
                activeWeapon.transform.rotation = Quaternion.Slerp(
                    activeWeapon.transform.rotation,
                    Quaternion.LookRotation(aimDirection, Vector3.up),
                    Time.deltaTime * AimingAnimationSpeed
                );

                float verticalOffset = -cameraVerticalAngle * 0.01f;

                // Получаем позицию прицельного положения с учетом положения игрока (стоя/сидя)
                Transform aimingTransform = m_PlayerCharacterController.IsCrouching
                    ? CrouchingAimingWeaponPosition
                    : StandingAimingWeaponPosition;

                Vector3 targetPosition = aimingTransform.localPosition + activeWeapon.AimOffset + new Vector3(0, verticalOffset, 0);

                // Используем Lerp для плавного движения оружия
                m_WeaponMainLocalPosition = Vector3.Lerp(
                    m_WeaponMainLocalPosition,
                    targetPosition,
                    Time.deltaTime * AimingAnimationSpeed
                );

                // Меняем FOV плавно
                SetFov(Mathf.Lerp(
                    m_PlayerCharacterController.PlayerCamera.fieldOfView,
                    activeWeapon.AimZoomRatio * DefaultFov,
                    AimingAnimationSpeed * Time.deltaTime
                ));
            }
            else // Стрельба от бедра
            {
                // Получаем позицию оружия для стрельбы от бедра с учетом положения игрока
                Transform defaultTransform = m_PlayerCharacterController.IsCrouching
                    ? CrouchingDefaultWeaponPosition
                    : StandingDefaultWeaponPosition;

                Vector3 defaultPosition = defaultTransform.localPosition + activeWeapon.AimOffset;

                // Плавно возвращаем оружие в стандартное положение
                m_WeaponMainLocalPosition = Vector3.Lerp(
                    m_WeaponMainLocalPosition,
                    defaultPosition,
                    Time.deltaTime * AimingAnimationSpeed
                );

                activeWeapon.transform.rotation = Quaternion.Slerp(
                    activeWeapon.transform.rotation,
                    Quaternion.LookRotation(aimDirection, Vector3.up),
                    Time.deltaTime * AimingAnimationSpeed
                );

                // Возвращаем FOV обратно
                SetFov(Mathf.Lerp(
                    m_PlayerCharacterController.PlayerCamera.fieldOfView,
                    DefaultFov,
                    AimingAnimationSpeed * Time.deltaTime
                ));
            }
        }

        // Update various animated features in LateUpdate because it needs to override the animated arm position
        Vector3 smoothVelocity = Vector3.zero; // Глобальная переменная

        void LateUpdate()
        {
            UpdateAiming();
            UpdateWeaponAiming();
            UpdateWeaponSwitching();
            UpdateWeaponBob();
            UpdateWeaponRecoil();
            UpdateWeaponSwitching();

            // Ограничение влияния качания и отдачи
            Vector3 bobOffset = m_WeaponBobLocalPosition;
            bobOffset.y = Mathf.Clamp(bobOffset.y, -0.05f, 0.05f);

            Vector3 recoilOffset = m_WeaponRecoilLocalPosition;
            recoilOffset.y = Mathf.Clamp(recoilOffset.y, -0.02f, 0.02f);

            // Плавное движение оружия с учетом покачивания и отдачи
            Vector3 targetPosition = m_WeaponMainLocalPosition + bobOffset + recoilOffset;

            // Используем SmoothDamp для более плавного движения оружия
            WeaponParentSocket.localPosition = Vector3.SmoothDamp(
                WeaponParentSocket.localPosition, targetPosition, ref smoothVelocity, 0.05f
            );
        }

        // Sets the FOV of the main camera and the weapon camera simultaneously
        public void SetFov(float fov)
        {
            m_PlayerCharacterController.PlayerCamera.fieldOfView = fov;
            WeaponCamera.fieldOfView = fov * WeaponFovMultiplier;
        }

        // Iterate on all weapon slots to find the next valid weapon to switch to
        public void SwitchWeapon(bool ascendingOrder)
        {
            int newWeaponIndex = -1;
            int closestSlotDistance = m_WeaponSlots.Length;
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // If the weapon at this slot is valid, calculate its "distance" from the active slot index (either in ascending or descending order)
                // and select it if it's the closest distance yet
                if (i != ActiveWeaponIndex && GetWeaponAtSlotIndex(i) != null)
                {
                    int distanceToActiveIndex = GetDistanceBetweenWeaponSlots(ActiveWeaponIndex, i, ascendingOrder);

                    if (distanceToActiveIndex < closestSlotDistance)
                    {
                        closestSlotDistance = distanceToActiveIndex;
                        newWeaponIndex = i;
                    }
                }
            }

            // Handle switching to the new weapon index
            SwitchToWeaponIndex(newWeaponIndex);
        }

        // Switches to the given weapon index in weapon slots if the new index is a valid weapon that is different from our current one
        public void SwitchToWeaponIndex(int newWeaponIndex, bool force = false)
        {
            if (force || (newWeaponIndex != ActiveWeaponIndex && newWeaponIndex >= 0))
            {
                // Store data related to weapon switching animation
                m_WeaponSwitchNewWeaponIndex = newWeaponIndex;
                m_TimeStartedWeaponSwitch = Time.time;

                // Handle case of switching to a valid weapon for the first time (simply put it up without putting anything down first)
                if (GetActiveWeapon() == null)
                {
                    m_WeaponMainLocalPosition = DownWeaponPosition.localPosition;
                    m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;

                    WeaponController newWeapon = GetWeaponAtSlotIndex(m_WeaponSwitchNewWeaponIndex);
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon.Invoke(newWeapon);
                    }
                }
                // otherwise, remember we are putting down our current weapon for switching to the next one
                else
                {
                    m_WeaponSwitchState = WeaponSwitchState.PutDownPrevious;
                }
            }
        }

        public WeaponController HasWeapon(WeaponController weapon)
        {
            foreach (var slot in m_WeaponSlots)
            {
                if (slot != null && slot.WeaponName == weapon.WeaponName) // Сравнение по имени
                {
                    return slot;
                }
            }
            return null;
        }


        // Updates weapon position and camera FoV for the aiming transition
        void UpdateWeaponAiming()
        {
            if (m_WeaponSwitchState == WeaponSwitchState.Up)
            {
                WeaponController activeWeapon = GetActiveWeapon();
                if (IsAiming && activeWeapon)
                {
                    m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                        StandingAimingWeaponPosition.localPosition + activeWeapon.AimOffset,
                        AimingAnimationSpeed * Time.deltaTime);
                    SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView,
                        activeWeapon.AimZoomRatio * DefaultFov, AimingAnimationSpeed * Time.deltaTime));
                }
                else
                {
                    //m_WeaponMainLocalPosition = Vector3.Lerp(m_WeaponMainLocalPosition,
                    //    DefaultWeaponPosition.localPosition, AimingAnimationSpeed * Time.deltaTime);
                    //SetFov(Mathf.Lerp(m_PlayerCharacterController.PlayerCamera.fieldOfView, DefaultFov,
                    //    AimingAnimationSpeed * Time.deltaTime));
                }
            }
        }

        // Updates the weapon bob animation based on character speed
        void UpdateWeaponBob()
        {
            if (Time.deltaTime > 0f)
            {
                Vector3 playerCharacterVelocity =
                    (m_PlayerCharacterController.transform.position - m_LastCharacterPosition) / Time.deltaTime;

                // calculate a smoothed weapon bob amount based on how close to our max grounded movement velocity we are
                float characterMovementFactor = 0f;
                if (m_PlayerCharacterController.IsGrounded)
                {
                    characterMovementFactor =
                        Mathf.Clamp01(playerCharacterVelocity.magnitude /
                                      (m_PlayerCharacterController.MaxSpeedOnGround *
                                       m_PlayerCharacterController.SprintSpeedModifier));
                }

                m_WeaponBobFactor =
                    Mathf.Lerp(m_WeaponBobFactor, characterMovementFactor, BobSharpness * Time.deltaTime);

                // Calculate vertical and horizontal weapon bob values based on a sine function
                float bobAmount = IsAiming ? AimingBobAmount : DefaultBobAmount;
                float frequency = BobFrequency;
                float hBobValue = Mathf.Sin(Time.time * frequency) * bobAmount * m_WeaponBobFactor;
                float vBobValue = ((Mathf.Sin(Time.time * frequency * 2f) * 0.5f) + 0.5f) * bobAmount *
                                  m_WeaponBobFactor;

                // Apply weapon bob
                m_WeaponBobLocalPosition.x = hBobValue;
                m_WeaponBobLocalPosition.y = Mathf.Abs(vBobValue);

                m_LastCharacterPosition = m_PlayerCharacterController.transform.position;
            }
        }

        // Updates the weapon recoil animation
        void UpdateWeaponRecoil()
        {
            // if the accumulated recoil is further away from the current position, make the current position move towards the recoil target
            if (m_WeaponRecoilLocalPosition.z >= m_AccumulatedRecoil.z * 0.99f)
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, m_AccumulatedRecoil,
                    RecoilSharpness * Time.deltaTime);
            }
            // otherwise, move recoil position to make it recover towards its resting pose
            else
            {
                m_WeaponRecoilLocalPosition = Vector3.Lerp(m_WeaponRecoilLocalPosition, Vector3.zero,
                    RecoilRestitutionSharpness * Time.deltaTime);
                m_AccumulatedRecoil = m_WeaponRecoilLocalPosition;
            }
        }

        // Updates the animated transition of switching weapons
        void UpdateWeaponSwitching()
        {
            // Calculate the time ratio (0 to 1) since weapon switch was triggered
            float switchingTimeFactor = 0f;
            if (WeaponSwitchDelay == 0f)
            {
                switchingTimeFactor = 1f;
            }
            else
            {
                switchingTimeFactor = Mathf.Clamp01((Time.time - m_TimeStartedWeaponSwitch) / WeaponSwitchDelay);
            }

            // Handle transiting to new switch state
            if (switchingTimeFactor >= 1f)
            {
                if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
                {
                    // Deactivate old weapon
                    WeaponController oldWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    if (oldWeapon != null)
                    {
                        oldWeapon.ShowWeapon(false);
                    }

                    ActiveWeaponIndex = m_WeaponSwitchNewWeaponIndex;
                    switchingTimeFactor = 0f;

                    // Activate new weapon
                    WeaponController newWeapon = GetWeaponAtSlotIndex(ActiveWeaponIndex);
                    if (OnSwitchedToWeapon != null)
                    {
                        OnSwitchedToWeapon.Invoke(newWeapon);
                    }

                    if (newWeapon)
                    {
                        m_TimeStartedWeaponSwitch = Time.time;
                        m_WeaponSwitchState = WeaponSwitchState.PutUpNew;
                    }
                    else
                    {
                        // if new weapon is null, don't follow through with putting weapon back up
                        m_WeaponSwitchState = WeaponSwitchState.Down;
                    }
                }
                else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
                {
                    m_WeaponSwitchState = WeaponSwitchState.Up;
                }
            }

            // Handle moving the weapon socket position for the animated weapon switching
            if (m_WeaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(CrouchingDefaultWeaponPosition.localPosition,
                    DownWeaponPosition.localPosition, switchingTimeFactor);
            }
            else if (m_WeaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                m_WeaponMainLocalPosition = Vector3.Lerp(DownWeaponPosition.localPosition,
                    CrouchingDefaultWeaponPosition.localPosition, switchingTimeFactor);
            }
        }

        void TryPickupWeapon()
        {
            Debug.Log("Trying to pick up weapon.");

            // Получаем все коллайдеры рядом
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 4f);
            Debug.Log("Found " + hitColliders.Length + " colliders nearby.");

            foreach (var hitCollider in hitColliders)
            {
                Debug.Log("Checking collider: " + hitCollider.name);

                // Проверяем наличие компонента WeaponController
                WeaponController weapon = hitCollider.GetComponent<WeaponController>();
                if (weapon == null)
                {
                    Debug.Log("Collider does not have WeaponController component.");
                    continue;
                }

                Debug.Log("Weapon found: " + weapon.WeaponName);

                // Проверяем, активен ли объект
                if (!weapon.gameObject.activeInHierarchy)
                {
                    Debug.LogError($"Weapon {weapon.WeaponName} is inactive, cannot be picked up!");
                    continue;
                }

                // Подбираем оружие
                if (AddWeapon(weapon))
                {
                    Debug.Log("Weapon added to inventory: " + weapon.WeaponName);

                    int weaponIndex = GetWeaponIndex(weapon);
                    Debug.Log("Weapon index: " + weaponIndex);

                    if (weaponIndex != -1)
                    {
                        SwitchToWeaponIndex(weaponIndex);
                    }

                    Destroy(hitCollider.gameObject); // Удаляем оружие с уровня
                    break; // Подбираем только одно оружие за раз
                }
                else
                {
                    Debug.Log("Failed to add weapon to inventory. No free slots?");
                }
            }
        }

        // Получаем индекс оружия в слотах
        int GetWeaponIndex(WeaponController weapon)
        {
            if (weapon == null)
            {
                Debug.LogError("GetWeaponIndex called with null weapon.");
                return -1;
            }

            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (m_WeaponSlots[i] == null)
                {
                    Debug.Log($"Slot {i} is empty.");
                    continue;
                }

                if (string.Equals(m_WeaponSlots[i].WeaponName, weapon.WeaponName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            Debug.Log("Weapon not found in slots.");
            return -1;
        }

        // Добавляем оружие в инвентарь
        public bool AddWeapon(WeaponController weaponPrefab)
        {
            if (weaponPrefab == null)
            {
                Debug.LogError("Cannot add a null weapon to inventory.");
                return false;
            }

            // Проверяем, есть ли уже такое оружие в инвентаре
            if (HasWeapon(weaponPrefab) != null)
            {
                Debug.Log("Weapon already in inventory.");
                return false;
            }

            // Ищем свободный слот
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                if (m_WeaponSlots[i] == null)
                {
                    // Создаем экземпляр оружия
                    WeaponController weaponInstance = Instantiate(weaponPrefab, WeaponParentSocket);
                    weaponInstance.transform.localPosition = Vector3.zero;
                    weaponInstance.transform.localRotation = Quaternion.identity;

                    // Настраиваем оружие
                    weaponInstance.Owner = gameObject;
                    weaponInstance.SourcePrefab = weaponPrefab.gameObject; // Присваиваем SourcePrefab правильно
                    weaponInstance.ShowWeapon(false);

                    Debug.Log($"Setting SourcePrefab: {weaponInstance.SourcePrefab?.name ?? "null"}");

                    // Добавляем оружие в слот
                    m_WeaponSlots[i] = weaponInstance;

                    Debug.Log("Weapon added to slot " + i);

                    OnAddedWeapon?.Invoke(weaponInstance, i);

                    return true;
                }
            }

            Debug.Log("No free slots available.");
            return false;
        }



        public bool RemoveWeapon(WeaponController weaponInstance)
        {
            // Look through our slots for that weapon
            for (int i = 0; i < m_WeaponSlots.Length; i++)
            {
                // when weapon found, remove it
                if (m_WeaponSlots[i] == weaponInstance)
                {
                    m_WeaponSlots[i] = null;

                    if (OnRemovedWeapon != null)
                    {
                        OnRemovedWeapon.Invoke(weaponInstance, i);
                    }

                    Destroy(weaponInstance.gameObject);

                    // Handle case of removing active weapon (switch to next weapon)
                    if (i == ActiveWeaponIndex)
                    {
                        SwitchWeapon(true);
                    }

                    return true;
                }
            }

            return false;
        }

        public WeaponController GetActiveWeapon()
        {
            return GetWeaponAtSlotIndex(ActiveWeaponIndex);
        }

        public WeaponController GetWeaponAtSlotIndex(int index)
        {
            // find the active weapon in our weapon slots based on our active weapon index
            if (index >= 0 &&
                index < m_WeaponSlots.Length)
            {
                return m_WeaponSlots[index];
            }

            // if we didn't find a valid active weapon in our weapon slots, return null
            return null;
        }

        // Calculates the "distance" between two weapon slot indexes
        // For example: if we had 5 weapon slots, the distance between slots #2 and #4 would be 2 in ascending order, and 3 in descending order
        int GetDistanceBetweenWeaponSlots(int fromSlotIndex, int toSlotIndex, bool ascendingOrder)
        {
            int distanceBetweenSlots = 0;

            if (ascendingOrder)
            {
                distanceBetweenSlots = toSlotIndex - fromSlotIndex;
            }
            else
            {
                distanceBetweenSlots = -1 * (toSlotIndex - fromSlotIndex);
            }

            if (distanceBetweenSlots < 0)
            {
                distanceBetweenSlots = m_WeaponSlots.Length + distanceBetweenSlots;
            }

            return distanceBetweenSlots;
        }

        void OnWeaponSwitched(WeaponController newWeapon)
        {
            if (newWeapon != null)
            {
                newWeapon.ShowWeapon(true);
            }
        }
    }
}