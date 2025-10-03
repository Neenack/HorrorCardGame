using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class SoundFXManager : NetworkSingleton<SoundFXManager>
{
    [Header("Sound Library")]
    [SerializeField] private List<SoundLibrarySO> soundLibraries;

    private Dictionary<string, SoundDataSO> soundLookup = new Dictionary<string, SoundDataSO>();
    private Dictionary<SoundDataSO, int> soundToIndexMap = new Dictionary<SoundDataSO, int>();
    private List<SoundDataSO> soundList = new List<SoundDataSO>();

    [Header("Pooling Settings")]
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private int maxPoolSize = 50;

    private Queue<AudioSource> audioSourcePool;
    private List<AudioSource> activeAudioSources;
    private Transform poolParent;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        foreach (var lib in soundLibraries)
        {
            if (lib == null) continue;
            lib.Initialize();

            foreach (var soundNameKey in lib.sounds)
            {
                if (soundNameKey.sound == null || string.IsNullOrEmpty(soundNameKey.key)) continue;

                if (!soundLookup.ContainsKey(soundNameKey.key))
                    soundLookup[soundNameKey.key] = soundNameKey.sound;
                else
                    Debug.LogWarning($"Duplicate sound name: {soundNameKey.key} in library {lib.name}");

                if (!soundToIndexMap.ContainsKey(soundNameKey.sound))
                {
                    int index = soundList.Count;
                    soundToIndexMap[soundNameKey.sound] = index;
                    soundList.Add(soundNameKey.sound);
                }
            }

            InitializePool();
        }
    }

    private SoundDataSO GetSound(string name)
    {
        if (soundLookup.TryGetValue(name, out SoundDataSO sound))
            return sound;

        Debug.LogWarning($"Sound not found: {name}");
        return null;
    }

    private int GetSoundIndex(SoundDataSO data)
    {
        if (soundToIndexMap.TryGetValue(data, out int index))
            return index;

        Debug.LogWarning($"Sound not found: {name}");
        return -1;
    }

    private void InitializePool()
    {
        poolParent = new GameObject("AudioSource Pool").transform;
        poolParent.SetParent(transform);
        audioSourcePool = new Queue<AudioSource>();
        activeAudioSources = new List<AudioSource>();

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledAudioSource();
        }
    }

    private AudioSource CreatePooledAudioSource()
    {
        GameObject obj = new GameObject("PooledAudioSource");
        obj.transform.SetParent(poolParent);
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        audioSourcePool.Enqueue(source);
        return source;
    }

    private AudioSource GetAudioSource()
    {
        if (audioSourcePool.Count == 0)
        {
            if (activeAudioSources.Count < maxPoolSize)
            {
                return CreatePooledAudioSource();
            }
            else
            {
                Debug.LogWarning("Audio pool at max capacity. Reusing oldest source.");
                AudioSource oldest = activeAudioSources[0];
                oldest.Stop();
                activeAudioSources.RemoveAt(0);
                return oldest;
            }
        }

        return audioSourcePool.Dequeue();
    }

    private void ReturnAudioSource(AudioSource source)
    {
        if (source == null) return;

        source.Stop();
        source.clip = null;
        activeAudioSources.Remove(source);
        audioSourcePool.Enqueue(source);
    }

    private IEnumerator ReturnAfterPlaying(AudioSource source, float duration)
    {
        yield return new WaitForSeconds(duration);
        ReturnAudioSource(source);
    }

    private void PlaySoundLocal(SoundDataSO SoundDataSO, Vector3 position, float volumeMultiplier)
    {
        if (SoundDataSO == null)
        {
            Debug.LogWarning("SoundDataSO is null");
            return;
        }

        AudioClip clip = SoundDataSO.GetClip();
        if (clip == null)
        {
            Debug.LogWarning($"No audio clip assigned to sound: {SoundDataSO.name}");
            return;
        }

        AudioSource audioSource = GetAudioSource();
        activeAudioSources.Add(audioSource);

        audioSource.transform.position = position;
        audioSource.clip = clip;
        audioSource.volume = Mathf.Clamp01(SoundDataSO.volume * volumeMultiplier);
        audioSource.pitch = SoundDataSO.GetPitch();
        audioSource.spatialBlend = SoundDataSO.is3D ? 1f : 0f;
        audioSource.maxDistance = SoundDataSO.maxDistance;

        if (SoundMixerManager.Instance != null)
        {
            audioSource.outputAudioMixerGroup = SoundMixerManager.Instance.GetSoundFXMixer();
        }

        audioSource.Play();

        float clipLength = clip.length / audioSource.pitch;
        StartCoroutine(ReturnAfterPlaying(audioSource, clipLength));
    }

    // RPC to play sound on all clients
    [ClientRpc]
    private void PlaySoundClientRpc(int soundIndex, Vector3 position, float volumeMultiplier)
    {
        SoundDataSO SoundDataSO = soundList[soundIndex];
        if (SoundDataSO != null)
        {
            PlaySoundLocal(SoundDataSO, position, volumeMultiplier);
        }
    }

    // RPC to play sound on specific client
    [ClientRpc]
    private void PlaySoundSingleClientRpc(int soundIndex, Vector3 position, float volumeMultiplier, ClientRpcParams clientRpcParams = default)
    {
        SoundDataSO SoundDataSO = soundList[soundIndex];
        if (SoundDataSO != null)
        {
            PlaySoundLocal(SoundDataSO, position, volumeMultiplier);
        }
    }

    // ===== STATIC API - PLAY BY STRING NAME =====

    /// <summary>
    /// Play sound locally by name (no network sync)
    /// Use for UI sounds, local player feedback
    /// </summary>
    public static void PlaySound(string soundName, float volumeMultiplier = 1f)
    {
        PlaySoundAtPosition(soundName, Vector3.zero, volumeMultiplier);
    }

    /// <summary>
    /// Play sound locally by name at specific position
    /// </summary>
    public static void PlaySoundAtPosition(string soundName, Vector3 position, float volumeMultiplier = 1f)
    {
        if (Instance == null || Instance.soundLibraries.Count == 0)
        {
            Debug.LogError("SoundFXManager not initialized!");
            return;
        }

        SoundDataSO SoundDataSO = Instance.GetSound(soundName);
        if (SoundDataSO == null)
        {
            Debug.LogWarning($"Sound not found: {soundName}");
            return;
        }

        Instance.PlaySoundLocal(SoundDataSO, position, volumeMultiplier);
    }

    /// <summary>
    /// Play sound on all clients by name (networked)
    /// Must be called from server/host
    /// </summary>
    public static void PlaySoundServer(string soundName, Vector3 position, float volumeMultiplier = 1f)
    {
        if (Instance == null || !Instance.IsServer)
        {
            Debug.LogWarning("PlaySoundNetworked can only be called on the server!");
            return;
        }

        SoundDataSO SoundDataSO = Instance.GetSound(soundName);
        if (SoundDataSO == null)
        {
            Debug.LogWarning($"Sound not found: {soundName}");
            return;
        }

        int soundIndex = Instance.GetSoundIndex(SoundDataSO);
        if (soundIndex >= 0)
        {
            Instance.PlaySoundClientRpc(soundIndex, position, volumeMultiplier);
        }
    }

    /// <summary>
    /// Play sound for specific client by name (networked)
    /// Must be called from server/host
    /// </summary>
    public static void PlaySoundForClient(string soundName, ulong clientId, Vector3 position, float volumeMultiplier = 1f)
    {
        if (Instance == null || !Instance.IsServer)
        {
            Debug.LogWarning("PlaySoundForClient can only be called on the server!");
            return;
        }

        SoundDataSO SoundDataSO = Instance.GetSound(soundName);
        if (SoundDataSO == null)
        {
            Debug.LogWarning($"Sound not found: {soundName}");
            return;
        }

        int soundIndex = Instance.GetSoundIndex(SoundDataSO);
        if (soundIndex >= 0)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };

            Instance.PlaySoundSingleClientRpc(soundIndex, position, volumeMultiplier, clientRpcParams);
        }
    }

    // ===== STATIC API - PLAY BY SoundDataSO REFERENCE =====

    /// <summary>
    /// Play sound locally by direct SoundDataSO reference
    /// </summary>
    public static void PlaySound(SoundDataSO SoundDataSO, float volumeMultiplier = 1f)
    {
        PlaySoundAtPosition(SoundDataSO, Vector3.zero, volumeMultiplier);
    }

    /// <summary>
    /// Play sound locally by direct SoundDataSO reference at position
    /// </summary>
    public static void PlaySoundAtPosition(SoundDataSO SoundDataSO, Vector3 position, float volumeMultiplier = 1f)
    {
        if (Instance == null)
        {
            Debug.LogError("SoundFXManager not initialized!");
            return;
        }

        Instance.PlaySoundLocal(SoundDataSO, position, volumeMultiplier);
    }

    /// <summary>
    /// Play sound networked by direct SoundDataSO reference
    /// Must be called from server/host
    /// </summary>
    public static void PlaySoundServer(SoundDataSO SoundDataSO, Vector3 position, float volumeMultiplier = 1f)
    {
        if (Instance == null || !Instance.IsServer)
        {
            Debug.LogWarning("PlaySoundNetworked can only be called on the server!");
            return;
        }

        int soundIndex = Instance.GetSoundIndex(SoundDataSO);
        if (soundIndex >= 0)
        {
            Instance.PlaySoundClientRpc(soundIndex, position, volumeMultiplier);
        }
        else
        {
            Debug.LogWarning($"SoundDataSO not found in library: {SoundDataSO.name}");
        }
    }

    // ===== LEGACY SUPPORT - AudioClip API =====

    /// <summary>
    /// Legacy support: Play AudioClip directly (local only)
    /// Note: This bypasses SoundDataSO settings
    /// </summary>
    public static GameObject PlaySoundClip(AudioClip audioClip, float volume = 1f, bool destroyOnFinish = true, bool loop = false, float pitch = 1f)
    {
        return PlaySoundClipAtPosition(audioClip, Vector3.zero, volume, destroyOnFinish, loop, pitch);
    }

    /// <summary>
    /// Legacy support: Play AudioClip directly at position (local only)
    /// </summary>
    public static GameObject PlaySoundClipAtPosition(AudioClip audioClip, Vector3 position, float volume = 1f, bool destroyOnFinish = true, bool loop = false, float pitch = 1f)
    {
        if (audioClip == null || Instance == null)
        {
            return null;
        }

        AudioSource audioSource = Instance.GetAudioSource();
        Instance.activeAudioSources.Add(audioSource);

        audioSource.transform.position = position;
        audioSource.clip = audioClip;
        audioSource.volume = Mathf.Clamp01(volume);
        audioSource.loop = loop;
        audioSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);

        if (SoundMixerManager.Instance != null)
        {
            audioSource.outputAudioMixerGroup = SoundMixerManager.Instance.GetSoundFXMixer();
        }

        audioSource.Play();

        if (destroyOnFinish && !loop)
        {
            float clipLength = audioClip.length / audioSource.pitch;
            Instance.StartCoroutine(Instance.ReturnAfterPlaying(audioSource, clipLength));
        }

        return audioSource.gameObject;
    }

    /// <summary>
    /// Legacy support: Play random AudioClip
    /// </summary>
    public static GameObject PlayRandomSoundClip(AudioClip[] audioClips, float volume = 1f, bool destroyOnFinish = true, bool loop = false, float pitch = 1f)
    {
        if (audioClips == null || audioClips.Length == 0) return null;
        return PlaySoundClip(audioClips[Random.Range(0, audioClips.Length)], volume, destroyOnFinish, loop, pitch);
    }

    public static void StopAllSounds()
    {
        if (Instance == null) return;

        foreach (var source in Instance.activeAudioSources.ToArray())
        {
            Instance.ReturnAudioSource(source);
        }
    }
}