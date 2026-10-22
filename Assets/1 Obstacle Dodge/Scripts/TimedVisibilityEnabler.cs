using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class TimedVisibilityEnabler : MonoBehaviour
    {
        [SerializeField] private float delay = 3f;
        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (!_renderer)
            {
                Debug.LogError("Renderer component not found on the GameObject.");
            }
            
            _renderer.enabled = false; // Ensure the renderer is disabled at the start
        }

        private void Start()
        {
            if (!_renderer) return;
            _renderer.enabled = false;
        }
        
        private void Update()
        {
            if (!_renderer || Time.time < delay) return;

            EnableVisibility();
            enabled = false; // Disable this script after enabling visibility
        }

        private void EnableVisibility()
        {
            if (!_renderer) return;
            _renderer.enabled = true;
        }
    }
}