using UnityEngine;

namespace DefaultNamespace
{
    public static class LayerHelper
    {
        public static bool IsInLayerMask(this GameObject GO, LayerMask layerMask)
        {
            return ((layerMask.value & (1 << GO.layer)) > 0);
        }
    }
}