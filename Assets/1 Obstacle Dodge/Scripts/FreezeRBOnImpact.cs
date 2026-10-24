using System.Collections;
using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class FreezeRBOnImpact : MonoBehaviour
    {
        private Rigidbody _rb;
        [SerializeField] private bool destroyOnImpact;
        [SerializeField] private float destroyDelay = 2f;
        [SerializeField] private float destroyScaleSpeed = 10f;

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
            
            if (destroyOnImpact)
            {
                StartCoroutine(HandleDestroy());
            }
        }

        private void FreezeRigidbody()
        {
            if (!_rb) return;

            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        private IEnumerator HandleDestroy()
        {
            yield return new WaitForSeconds(destroyDelay);
            
            while(gameObject.transform.localScale != Vector3.zero)
            {
                gameObject.transform.localScale = Vector3.Lerp(gameObject.transform.localScale, Vector3.zero, Time.deltaTime * destroyScaleSpeed);
                
                if (gameObject.transform.localScale.x < 0.2)
                {
                    gameObject.transform.localScale = Vector3.zero;
                }
                
                yield return null;
            }
            
            Destroy(gameObject);
        }
    }
}