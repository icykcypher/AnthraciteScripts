using UnityEngine;

namespace Anthracite.Game
{
    public class WeaponModificationUI : MonoBehaviour
    {
        public WeaponController weaponController; // Контроллер оружия
        public GameObject modificationMenu; // UI-меню для модификации
        public int selectedSlotIndex = 0; // Индекс выбранного слота

        void Update()
        {
            // Открываем/закрываем меню модификации
            if (Input.GetKeyDown(KeyCode.T))
            {
                modificationMenu.SetActive(!modificationMenu.activeSelf);
            }
        }

        // Выбор модуля для установки
        public void SelectModule(GameObject modulePrefab)
        {
            if (weaponController != null)
            {
                weaponController.AttachModule(selectedSlotIndex, modulePrefab);
            }
        }

        // Выбор слота
        public void SelectSlot(int slotIndex)
        {
            selectedSlotIndex = slotIndex;
        }
    }
}
