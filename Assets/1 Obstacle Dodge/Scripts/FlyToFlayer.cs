using System;
using DefaultNamespace;
using DefaultNamespace.Interfaces;
using UnityEngine;

public class FlyToFlayer : MonoBehaviour, ISimpleTriggerReceiver
{
    [SerializeField] private Transform target;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float moveToTargetDelay;
    [SerializeField] private bool navigateToPlayer = true;
    [SerializeField] private bool isHoming = true;
    [SerializeField] private bool activateOnTrigger = false;
    
    private bool _isFunctional = true;
    private Vector3 _targetPosition;

    private void Awake()
    {
        if (navigateToPlayer)
        {
            target = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (!target)
            {
                Debug.LogError(
                    "Player not found in the scene. Please ensure there is a GameObject with the 'Player' tag.");
            }
        }

        if (!target)
        {
            Debug.LogError("Target is not assigned. Please assign a target Transform in the inspector.");
            _isFunctional = false;
            return;
        }

        _targetPosition = target.position;
        
        if (activateOnTrigger)
        {
            _isFunctional = false;
        }
    }

    private void Update()
    {
        if (!_isFunctional || Time.time < moveToTargetDelay)
            return;

        if (isHoming)
        {
            _targetPosition = target.position;
        }

        // Move towards the target position
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, speed * Time.deltaTime);
    }


    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.IsInLayerMask(Constants.LayoutLayer))
        {
            _isFunctional = false;
            Rigidbody rb = GetComponent<Rigidbody>();
            
            if (rb == null) return;
            
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            rb.useGravity = true;
        }
    }
    
    public void OnTriggered()
    {
        _isFunctional = true;
    }
}