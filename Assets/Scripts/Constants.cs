using UnityEngine;

namespace DefaultNamespace
{
    public class Constants
    {
        public const string PlayerTag = "Player";
        public const string TaggedTag = "Tagged";
        
        public static LayerMask ObstacleLayer = 1 << 6; // Assuming the Obstacle layer is at index 6
        public static LayerMask LayoutLayer = 1 << 7; // Assuming the Layout layer is at index 7
    }
}