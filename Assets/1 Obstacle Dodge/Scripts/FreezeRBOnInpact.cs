using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class FreezeRBOnInpact : MonoBehaviour
    {
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (!_rb)
            {
                Debug.LogError("Rigidbody component not found on the GameObject.");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_rb) return;
            
            FreezeRigidbody();
        }

        private void FreezeRigidbody()
        {
            if (!_rb) return;

            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
}