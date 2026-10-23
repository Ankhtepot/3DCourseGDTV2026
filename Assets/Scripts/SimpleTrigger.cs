using System.Collections.Generic;
using DefaultNamespace.Interfaces;
using UnityEngine;

namespace DefaultNamespace
{
    public class SimpleTrigger : MonoBehaviour
    {
        [SerializeField] private List<MonoBehaviour> receivers = new();
        
        private void Awake()
        {
            ValidateReceivers();
        }

        private void ValidateReceivers()
        {
            List<int> indicesToRemove = new();
            // Validate that all receivers implement ISimpleTriggerReceiver
            for (int i = 0; i < receivers.Count; i++)
            {
                if (receivers[i] is not ISimpleTriggerReceiver)
                {
                    Debug.LogError($"Receiver at index {i} does not implement ISimpleTriggerReceiver. Removing it from the list.");
                    indicesToRemove.Add(i);
                }
            }
            
            // Remove invalid receivers in reverse order to avoid index shifting
            for (int i = indicesToRemove.Count - 1; i >= 0; i--)
            {
                receivers.RemoveAt(indicesToRemove[i]);
            }
        }

        protected void Trigger()
        {
            if (receivers is not {Count: > 0}) return;

            foreach (ISimpleTriggerReceiver receiver in receivers)
            {
                receiver.OnTriggered();
            }
        }
    }
}