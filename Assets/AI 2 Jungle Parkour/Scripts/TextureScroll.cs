using UnityEngine;

/// <summary>Scrolls a renderer's base texture, for running water.</summary>
[RequireComponent(typeof(Renderer))]
public class TextureScroll : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [Tooltip("UV units per second.")]
    [SerializeField] private Vector2 speed = new Vector2(0f, -0.6f);

    private Material material;

    private void Awake()
    {
        material = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        material.SetTextureOffset(BaseMapId, speed * Time.time);
    }
}
