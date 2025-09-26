using UnityEngine;

public class PlayingCardHighlighter : MonoBehaviour, IHighlighter
{
    private Transform card;
    private Vector3 originalScale;
    private Vector3 targetScale;

    [SerializeField] private float highlightScale = 1.2f; // how much bigger when highlighted
    [SerializeField] private float lerpSpeed = 5f;        // speed of scaling

    private void Start()
    {
        card = transform.GetChild(0);
        originalScale = card.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        // Smoothly interpolate towards target scale
        card.localScale = Vector3.Lerp(card.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }
    public void Highlight(bool enable)
    {
        targetScale = enable ? originalScale * highlightScale : originalScale;
    }
}
