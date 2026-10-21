using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Mover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float sprintMultiplier = 2f;
    private InputAction moveAction;
    private InputAction sprintAction;

    private void Awake()
    {
        moveAction = InputSystem.actions["Move"];
        sprintAction = InputSystem.actions["Sprint"];
    }

    void Start()
    {
        moveAction.Enable();
        sprintAction.Enable();
    }

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool isSprinting = sprintAction.ReadValue<float>() > 0.5f;
        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        transform.Translate(new Vector3(input.x, 0, input.y) * (Time.deltaTime * currentSpeed));
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }
}
