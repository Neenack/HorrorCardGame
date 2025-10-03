using UnityEngine;
using System.Collections.Generic;
using static SoundFXManager;

[System.Serializable]
public struct NameSoundData
{
    public string key;
    public SoundDataSO sound;
}


[CreateAssetMenu(fileName = "Sound Library", menuName = "Audio/Sound Library")]
public class SoundLibrarySO : ScriptableObject
{
    [Header("All Game Sounds")]
    public List<NameSoundData> sounds = new List<NameSoundData>();

    private Dictionary<string, SoundDataSO> soundDictionary;
    private Dictionary<SoundDataSO, int> soundToIndexMap;

    /// <summary>
    /// Initialize the lookup dictionaries
    /// </summary>
    public void Initialize()
    {
        soundDictionary = new Dictionary<string, SoundDataSO>();

        foreach (var entry in sounds)
        {
            if (entry.sound != null && !string.IsNullOrEmpty(entry.key))
            {
                if (!soundDictionary.ContainsKey(entry.key))
                {
                    soundDictionary[entry.key] = entry.sound;
                }
                else
                {
                    Debug.LogWarning($"Duplicate sound key in library: {entry.key}");
                }
            }
        }
    }

    /// <summary>
    /// Get sound data by name
    /// </summary>
    public SoundDataSO GetSound(string soundName)
    {
        if (soundDictionary == null)
        {
            Initialize();
        }

        soundDictionary.TryGetValue(soundName, out SoundDataSO sound);
        return sound;
    }

    /// <summary>
    /// Get sound data by index (for network sync)
    /// </summary>
    public SoundDataSO GetSoundByIndex(int index)
    {
        if (index >= 0 && index < sounds.Count)
        {
            return sounds[index].sound;
        }
        return null;
    }

    /// <summary>
    /// Get index of a sound data (for network sync)
    /// </summary>
    public int GetSoundIndex(SoundDataSO SoundDataSO)
    {
        if (soundToIndexMap == null)
        {
            Initialize();
        }

        if (soundToIndexMap.TryGetValue(SoundDataSO, out int index))
        {
            return index;
        }
        return -1;
    }

    /// <summary>
    /// Checks if the sound is in the sound library
    /// </summary>
    public bool HasSound(string key)
    {
        if (soundDictionary == null)
            Initialize();

        return soundDictionary.ContainsKey(key);
    }
}