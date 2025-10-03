using UnityEngine;

[CreateAssetMenu(fileName = "New Sound", menuName = "Audio/Sound Data")]
public class SoundDataSO : ScriptableObject
{
    [Header("Audio")]
    public AudioClip clip;
    public AudioClip[] clipVariations; // Optional: for random variations

    [Header("Settings")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;

    [Header("Randomization")]
    public bool randomizePitch = false;
    [Range(0f, 0.5f)] public float pitchVariation = 0.1f;

    [Header("Spatial")]
    public bool is3D = true;
    [Range(0f, 500f)] public float maxDistance = 50f;

    /// <summary>
    /// Get a clip (random if variations exist, otherwise the main clip)
    /// </summary>
    public AudioClip GetClip()
    {
        if (clipVariations != null && clipVariations.Length > 0)
        {
            return clipVariations[Random.Range(0, clipVariations.Length)];
        }
        return clip;
    }

    /// <summary>
    /// Get pitch with optional randomization
    /// </summary>
    public float GetPitch()
    {
        if (randomizePitch)
        {
            return pitch + Random.Range(-pitchVariation, pitchVariation);
        }
        return pitch;
    }
}