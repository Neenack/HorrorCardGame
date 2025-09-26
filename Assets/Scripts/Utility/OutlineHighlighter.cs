using UnityEngine;

public class OutlineHighlighter : MonoBehaviour, IHighlighter
{
    [Tooltip("Rendering Layer index (0-31) that the outline feature is set to use in URP.")]
    [SerializeField] private int outlineLayerIndex = 1;

    [Header("Optional: Assign multiple renderers if object has children")]
    [SerializeField] private Renderer[] renderers;

    private Renderer mainRenderer;
    private uint[] originalMasks;
    private uint outlineMask;

    void Awake()
    {
        // Check if array is null or empty
        if (renderers != null && renderers.Length > 0)
        {
            originalMasks = new uint[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    originalMasks[i] = renderers[i].renderingLayerMask;
            }
        }
        else
        {
            mainRenderer = GetComponent<Renderer>();
            if (mainRenderer == null)
            {
                Debug.LogError("OutlineHighlighter: No Renderer found on object or in array!");
                return;
            }
            originalMasks = new uint[1];
            originalMasks[0] = mainRenderer.renderingLayerMask;
        }

        outlineMask = 1u << outlineLayerIndex;
    }

    public void Highlight(bool enable)
    {
        if (renderers != null && renderers.Length > 0)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].renderingLayerMask = enable ? outlineMask : originalMasks[i];
            }
        }
        else if (mainRenderer != null)
        {
            mainRenderer.renderingLayerMask = enable ? outlineMask : originalMasks[0];
        }
    }
}
