using UnityEngine;
using UnityEngine.InputSystem;

public class Mover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float sprintMultiplier = 2f;
    
    private InputAction _moveAction;
    private InputAction _sprintAction;
    private Transform _transform;

    private void Awake()
    {
        _moveAction = InputSystem.actions["Move"];
        _sprintAction = InputSystem.actions["Sprint"];
        _transform = transform;
    }

    void Start()
    {
        _moveAction.Enable();
        _sprintAction.Enable();
    }

    void Update()
    {
        HandlePlayerMovement();
    }

    private void HandlePlayerMovement()
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        bool isSprinting = _sprintAction.ReadValue<float>() > 0.5f;
        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        // Move relative to the player's facing (the camera rotates the player), not in world space
        Vector3 direction = _transform.rotation * new Vector3(input.x, 0, input.y);
        Vector3 newPosition = _transform.position + direction * (Time.deltaTime * currentSpeed);
        _transform.position = newPosition;
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _sprintAction?.Disable();
    }
}
