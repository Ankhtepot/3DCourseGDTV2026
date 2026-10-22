using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class TimedGravityEnabler : MonoBehaviour
    {
        [SerializeField] private float delay = 3f;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (!_rb)
            {
                Debug.LogError("Rigidbody component not found on the GameObject.");
            }
        }

        private void Start()
        {
            if (!_rb) return;
            _rb.useGravity = false;
        }
        
        private void Update()
        {
            if (!_rb || Time.time < delay) return;

            EnableGravity();
            enabled = false; // Disable this script after enabling gravity
        }

        private void EnableGravity()
        {
            if (!_rb) return;
            _rb.useGravity = true;
        }
    }
}