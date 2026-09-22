using System.Collections.Generic;
using UnityEngine;

public partial class StoryCutsceneDirector
{
    [Header("Audio placeholders - replace any clip in Inspector")]
    public AudioClip schoolAmbience;
    public AudioClip streetAmbience;
    public AudioClip busInteriorAmbience;
    public AudioClip horrorAmbience;
    public AudioClip busEngineIdle;
    public AudioClip busEngineDriving;
    public AudioClip busDoorSound;
    public AudioClip busDepartureSound;
    public AudioClip footstepsSound;
    public AudioClip crashSound;
    public AudioClip dialogueBlipClip;
    [Range(0, 1)] public float ambientVolume = .35f;
    [Range(0, 1)] public float engineVolume = .45f;
    [Range(0, 1)] public float effectsVolume = .6f;

    AudioSource ambienceSource, engineSource, effectsSource;
    AudioClip generatedWind, generatedEngine, generatedStep, generatedDoor, generatedCrash;
    readonly List<AudioClip> generatedSounds = new List<AudioClip>();

    void PrepareStoryAudio()
    {
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.loop = true; ambienceSource.spatialBlend = 0; ambienceSource.volume = ambientVolume;
        engineSource = existingBus.gameObject.AddComponent<AudioSource>();
        engineSource.loop = true; engineSource.spatialBlend = .55f; engineSource.minDistance = 5;
        engineSource.maxDistance = 80; engineSource.volume = engineVolume;
        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.spatialBlend = 0; effectsSource.volume = effectsVolume;
        generatedWind = MakePlaceholder("Wind and distant night noise", 1.2f, 0);
        generatedEngine = MakePlaceholder("Bus engine hum", 1.2f, 1);
        generatedStep = MakePlaceholder("Footstep", .16f, 2);
        generatedDoor = MakePlaceholder("Bus door creak", .55f, 3);
        generatedCrash = MakePlaceholder("Impact", .8f, 4);
    }

    AudioClip MakePlaceholder(string title, float seconds, int type)
    {
        const int rate = 22050;
        int length = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[length];
        uint random = 99173;
        float smooth = 0;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / rate;
            random = random * 1664525u + 1013904223u;
            float noise = ((random >> 16) / 32768f - 1);
            smooth = Mathf.Lerp(smooth, noise, type == 0 ? .003f : .12f);
            if (type == 0) data[i] = smooth * .18f + Mathf.Sin(t * 6.28f * 75) * .008f;
            else if (type == 1) data[i] = Mathf.Sin(t * 6.28f * 45) * .12f + Mathf.Sin(t * 6.28f * 90) * .04f + smooth * .025f;
            else if (type == 2) data[i] = smooth * .3f * Mathf.Exp(-t * 34);
            else if (type == 3) data[i] = (smooth * .14f + Mathf.Sin(t * 6.28f * (160 - t * 90)) * .04f) * Mathf.Sin(Mathf.PI * t / seconds);
            else data[i] = smooth * .65f * Mathf.Exp(-t * 6);
        }
        var clip = AudioClip.Create(title, length, 1, rate, false);
        clip.SetData(data, 0); generatedSounds.Add(clip); return clip;
    }

    void PlayAmbience(AudioClip assigned)
    {
        if (ambienceSource == null) return;
        AudioClip chosen = assigned != null ? assigned : generatedWind;
        if (ambienceSource.clip == chosen && ambienceSource.isPlaying) return;
        ambienceSource.clip = chosen; ambienceSource.volume = ambientVolume; ambienceSource.Play();
    }
    void PlayEngine(bool driving)
    {
        if (engineSource == null) return;
        engineSource.clip = driving
            ? busEngineDriving != null ? busEngineDriving : generatedEngine
            : busEngineIdle != null ? busEngineIdle : generatedEngine;
        engineSource.volume = engineVolume;
        engineSource.Play();
    }
    void StopEngine() { if (engineSource != null) engineSource.Stop(); }
    void PlayEffect(AudioClip assigned, int placeholder)
    {
        if (effectsSource == null) return;
        AudioClip clip = assigned != null ? assigned : placeholder == 2 ? generatedStep : placeholder == 3 ? generatedDoor : generatedCrash;
        effectsSource.PlayOneShot(clip, effectsVolume);
    }
    void CleanupStoryAudio()
    {
        if (ambienceSource != null) Destroy(ambienceSource);
        if (engineSource != null) Destroy(engineSource);
        if (effectsSource != null) Destroy(effectsSource);
        foreach (var clip in generatedSounds) if (clip != null) Destroy(clip);
        generatedSounds.Clear();
    }
}
