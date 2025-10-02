using UnityEngine;

public class PlayingCardHighlighter : MonoBehaviour, IHighlighter
{
    private Vector3 originalScale;
    private Vector3 targetScale;

    [SerializeField] private float highlightScale = 1.2f; // how much bigger when highlighted
    [SerializeField] private float lerpSpeed = 5f;        // speed of scaling

    private void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        // Smoothly interpolate towards target scale
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }
    public void Highlight(bool enable)
    {
        targetScale = enable ? originalScale * highlightScale : originalScale;
    }
}
