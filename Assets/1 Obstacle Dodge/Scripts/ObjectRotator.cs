using UnityEditor;
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
        
        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            // A unit cube scaled on X has these half-extents in the XZ plane.
            float halfX = Mathf.Abs(transform.lossyScale.x) * 0.5f;
            float halfZ = Mathf.Abs(transform.lossyScale.z) * 0.5f;

            // Farthest corner from the pivot as the cube rotates around Y.
            float radius = Mathf.Sqrt(halfX * halfX + halfZ * halfZ);

            Handles.color = Color.cyan;
            Handles.DrawWireDisc(transform.position, Vector3.up, radius);
#endif
        }
    }
}