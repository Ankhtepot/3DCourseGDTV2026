using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class ObjectRotator : MonoBehaviour
    {
        [SerializeField] private Vector3 rotationSpeed = new(0f, 100f, 0f);

        private void Update()
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}