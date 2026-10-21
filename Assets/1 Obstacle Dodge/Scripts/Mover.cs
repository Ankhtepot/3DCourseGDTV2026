using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Mover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    private InputAction moveAction;

    private void Awake()
    {
        moveAction = InputSystem.actions["Move"];
    }

    void Start()
    {
        moveAction.Enable();
    }

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        transform.Translate(new Vector3(input.x, 0, input.y) * (Time.deltaTime * moveSpeed));
    }

    private void OnDisable()
    {
        moveAction?.Disable();
    }
}
