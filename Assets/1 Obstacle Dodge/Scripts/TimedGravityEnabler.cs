using System.Collections;
using DefaultNamespace.Interfaces;
using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class TimedGravityEnabler : MonoBehaviour, ISimpleTriggerReceiver
    {
        [SerializeField] private float delay = 3f;
        [SerializeField] private bool triggered;
        
        private Rigidbody _rb;
        private bool _isFunctional;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (!_rb)
            {
                Debug.LogError("Rigidbody component not found on the GameObject.");
                return;
            }
            
            _rb.useGravity = false;
            
            if(!triggered)
            {
                StartCoroutine(RunCoroutine());
            }
        }
        
        private void Update()
        {
            if (!_isFunctional) return;

            EnableGravity();
            enabled = false; // Disable this script after enabling gravity
        }

        private void EnableGravity()
        {
            if (!_isFunctional) return;
            _rb.useGravity = true;
        }

        public void OnTriggered()
        {
            StartCoroutine(RunCoroutine());
        }
        
        private IEnumerator RunCoroutine()
        {
            _isFunctional = true;
            yield return new WaitForSeconds(delay);
            EnableGravity();
        }
    }
}