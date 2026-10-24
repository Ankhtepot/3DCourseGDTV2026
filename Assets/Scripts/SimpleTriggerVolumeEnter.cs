using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class SimpleTriggerVolumeEnter : SimpleTrigger
    {
        [SerializeField] LayerMask triggerLayers = ~0; // Default to all layers
        [SerializeField] string[] triggerTags = Array.Empty<string>();
        [SerializeField] bool triggerOnce = true;
        [SerializeField] bool triggered;


        private void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && triggered) return;
            if ((triggerLayers.value & (1 << other.gameObject.layer)) == 0
                && Array.IndexOf(triggerTags, other.gameObject.tag) < 0)
                return;

            Trigger();
            triggered = true;
        }
    }
}