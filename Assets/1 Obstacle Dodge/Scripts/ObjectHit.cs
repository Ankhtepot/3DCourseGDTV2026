using System;
using UnityEngine;

public class ObjectHit : MonoBehaviour
{
    [SerializeField] private Color hitColor = Color.black;
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            GetComponent<Renderer>().material.color = hitColor;
        }
    }
}
