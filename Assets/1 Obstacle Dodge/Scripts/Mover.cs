using UnityEngine;

public class Mover : MonoBehaviour
{
    [SerializeField] private float xValue;
    [SerializeField] private float yValue;
    [SerializeField] private float zValue;
    
    void Start()
    {
        
    }

    void Update()
    {
        transform.Translate(new Vector3(xValue, yValue, zValue) * Time.deltaTime);
    }
}
