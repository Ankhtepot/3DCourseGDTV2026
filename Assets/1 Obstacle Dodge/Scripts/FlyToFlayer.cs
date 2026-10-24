using System.Collections;
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
    [SerializeField] private bool activateOnTrigger;

    private bool _isFunctional;
    private Vector3 _targetPosition;

    private void Awake()
    {
        Initiate();
    }

    private void Update()
    {
        if (!_isFunctional)
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
        StartCoroutine(Run());
    }

    private void Initiate()
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
            return;
        }

        // It's not triggered, so we can start moving towards the target after the delay
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(moveToTargetDelay);
        _isFunctional = true;
    }
}