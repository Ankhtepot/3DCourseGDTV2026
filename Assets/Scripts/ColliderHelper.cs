using UnityEngine;

namespace DefaultNamespace
{
    public static class ColliderHelper
    {
        public static Vector3 ClosestPointOn(Collider other, Vector3 position)
        {
            // Collider.ClosestPoint is unsupported for non-convex mesh colliders
            if (other is MeshCollider { convex: false })
                return other.bounds.ClosestPoint(position);
            return other.ClosestPoint(position);
        }
        
        public static void GetCapsuleWorldPoints(CapsuleCollider targetCollider, out Vector3 point0, out Vector3 point1,
            out float radius)
        {
            Transform t = targetCollider.transform;
            Vector3 scale = t.lossyScale;
            Vector3 axis;
            float axisScale;
            float radiusScale;

            switch (targetCollider.direction)
            {
                case 0:
                    axis = t.right;
                    axisScale = Mathf.Abs(scale.x);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    break;
                case 2:
                    axis = t.forward;
                    axisScale = Mathf.Abs(scale.z);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    break;
                default:
                    axis = t.up;
                    axisScale = Mathf.Abs(scale.y);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    break;
            }

            radius = targetCollider.radius * radiusScale;
            float halfSegment = Mathf.Max(0f, targetCollider.height * axisScale * 0.5f - radius);
            Vector3 center = t.TransformPoint(targetCollider.center);
            point0 = center + axis * halfSegment;
            point1 = center - axis * halfSegment;
        }
    }
}