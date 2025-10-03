using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundMixerManager : MonoSingleton<SoundMixerManager>
{
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private AudioMixerGroup soundEffectMixer;

    public AudioMixer GetAudioMixer() => audioMixer;
    public AudioMixerGroup GetSoundFXMixer() => soundEffectMixer;

    public void SetMasterVolume(float level)
    {
        float volume = ConvertLevel(level);
        Debug.Log("Master Volume Set: " + volume);
        audioMixer.SetFloat("masterVolume", Mathf.Log10(volume) * 20f);
    }

    public void SetSoundFXVolume(float level)
    {
        float volume = ConvertLevel(level);
        Debug.Log("SoundFX Volume Set: " + volume);
        audioMixer.SetFloat("soundFXVolume", Mathf.Log10(volume) * 20f);
    }

    public void SetMusicVolume(float level)
    {
        float volume = ConvertLevel(level);
        Debug.Log("Music Volume Set: " + volume);
        audioMixer.SetFloat("musicVolume", Mathf.Log10(volume) * 20f);
    }

    private float ConvertLevel(float value)
    {
        return Mathf.Clamp(value, 0.0001f, 1f);
    }
}
