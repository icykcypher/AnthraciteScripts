using UnityEngine;

namespace Assets.Scripts.Player
{
    public class RagdollPlayerManager : MonoBehaviour
    {

        private Rigidbody[] _rigidbodies;
        private Collider[] _colliders;
        private Animator _animator;

        void Start()
        {
            _rigidbodies = GetComponentsInChildren<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
            _animator = GetComponent<Animator>();

            SetRagdollState(true);
        }

        // Включить или выключить ragdoll
        public void SetRagdollState(bool state)
        {
            foreach (Rigidbody rb in _rigidbodies)
            {
                rb.isKinematic = !state;
            }

            foreach (Collider col in _colliders)
            {
                col.enabled = state;
            }

            if (_animator != null)
            {
                _animator.enabled = !state; // Отключаем анимацию
            }
        }
    }
}