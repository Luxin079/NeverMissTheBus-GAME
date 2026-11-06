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
    [Header("Original ambient score")]
    public AudioClip gameplayMusic;
    [Range(0, 1)] public float musicVolume = .12f;
    AudioSource musicSource;
    [Range(0, 1)] public float ambientVolume = .35f;
    [Range(0, 1)] public float engineVolume = .45f;
    [Range(0, 1)] public float effectsVolume = .6f;

    AudioSource ambienceSource, engineSource, effectsSource;
    [Header("Occasional night sounds - original placeholders when empty")]
    public AudioClip owlCall, cricketCall, distantUnsettlingSound;
    public Vector2 nightSoundInterval = new Vector2(18, 38);
    [Range(0,1)] public float nightSoundVolume = .22f;
    AudioSource nightSource;
    AudioClip[] nightClips;
    float nextNightSound;
    int lastNightSound=-1;
    bool nightPaused;
    AudioListener storyListener;
    readonly Dictionary<AudioListener,bool> savedListeners = new Dictionary<AudioListener,bool>();

    void UpdateNightSounds()
    {
        Camera listeningCamera = cinematicCamera && storyCamera != null ? storyCamera : gameCamera;
        if(storyListener != null && listeningCamera != null)
            storyListener.transform.SetPositionAndRotation(listeningCamera.transform.position,listeningCamera.transform.rotation);
        if(nightSource==null)return;
        bool quiet=Time.timeScale<=0 || IsDialogueLinePlaying;
        if(quiet && !nightPaused) { nightSource.Pause();nightPaused=true; }
        if(!quiet && nightPaused) { nightSource.UnPause();nightPaused=false; }
        if(quiet || !arrivedAtLastStop || playerMovement==null || !playerMovement.isActiveAndEnabled || Time.time<nextNightSound || nightSource.isPlaying)return;
        int choice=Random.Range(0,3);
        if(choice==lastNightSound)choice=(choice+1)%3;
        if(cameraIndoors)choice=2;
        lastNightSound=choice;
        Vector2 offset=Random.insideUnitCircle.normalized*Random.Range(6f,12f);
        nightSource.transform.position=existingPlayer.position+new Vector3(offset.x,2,offset.y);
        nightSource.clip=nightClips[choice];nightSource.volume=nightSoundVolume*(cameraIndoors?.55f:1f);
        nightSource.Play();
        nextNightSound=Time.time+Random.Range(Mathf.Max(8,nightSoundInterval.x),Mathf.Max(9,nightSoundInterval.y));
    }

    AudioClip MakeNightSound(int kind)
    {
        const int rate=22050;float seconds=kind==2?4f:2.4f;
        var samples=new float[Mathf.CeilToInt(seconds*rate)];
        for(int i=0;i<samples.Length;i++)
        {
            float t=(float)i/rate;
            float envelope=Mathf.Pow(Mathf.Sin(Mathf.PI*t/seconds),2);
            float value;
            if(kind==0) { float pulse=Mathf.Max(0,Mathf.Sin(t*8));value=Mathf.Sin(2*Mathf.PI*(390*t+8*Mathf.Sin(t*5)))*pulse*.28f; }
            else if(kind==1) { float pulse=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*65)),8);value=Mathf.Sin(2*Mathf.PI*3200*t)*pulse*.12f; }
            else value=(Mathf.Sin(2*Mathf.PI*(71*t+3*t*t))*.22f+Mathf.Sin(2*Mathf.PI*113.4f*t)*.09f)*(.7f+.3f*Mathf.Sin(t*7));
            samples[i]=value*envelope;
        }
        var clip=AudioClip.Create(kind==0?"Placeholder owl":kind==1?"Placeholder crickets":"Original distant resonance",samples.Length,1,rate,false);
        clip.SetData(samples,0);generatedSounds.Add(clip);return clip;
    }
    AudioClip generatedWind, generatedEngine, generatedStep, generatedDoor, generatedCrash;
    readonly List<AudioClip> generatedSounds = new List<AudioClip>();

    void PrepareStoryAudio()
    {
        foreach(var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        { savedListeners[listener]=listener.enabled;listener.enabled=false; }
        var listenerObject=new GameObject("Story audio listener");listenerObject.transform.SetParent(transform);
        storyListener=listenerObject.AddComponent<AudioListener>();
        var nightObject=new GameObject("Occasional spatial night ambience");
        nightObject.transform.SetParent(transform);
        nightSource=nightObject.AddComponent<AudioSource>();nightSource.playOnAwake=false;
        nightSource.spatialBlend=1;nightSource.minDistance=4;nightSource.maxDistance=30;
        nightClips=new[]{owlCall!=null?owlCall:MakeNightSound(0),cricketCall!=null?cricketCall:MakeNightSound(1),distantUnsettlingSound!=null?distantUnsettlingSound:MakeNightSound(2)};
        nextNightSound=Time.time+20;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true; musicSource.spatialBlend = 0; musicSource.playOnAwake = false;
        var score = gameplayMusic;
        if (score == null) { score = OriginalAmbientScore.Create(false); generatedSounds.Add(score); }
        musicSource.clip = score; musicSource.volume = musicVolume; musicSource.Play();
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
        if(storyListener!=null) { storyListener.enabled=false;Destroy(storyListener.gameObject); }
        foreach(var pair in savedListeners)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        savedListeners.Clear();
        if(nightSource!=null)Destroy(nightSource.gameObject);
        if (musicSource != null) Destroy(musicSource);
        if (ambienceSource != null) Destroy(ambienceSource);
        if (engineSource != null) Destroy(engineSource);
        if (effectsSource != null) Destroy(effectsSource);
        foreach (var clip in generatedSounds) if (clip != null) Destroy(clip);
        generatedSounds.Clear();
    }
}

// Original sparse music-box/pad composition, not a transcription of an existing soundtrack.
public static class OriginalAmbientScore
{
    public static AudioClip Create(bool menu)
    {
        const int rate = 22050, seconds = 32;
        var data = new float[rate * seconds];
        int[] notes = menu ? new[] { 62, 69, 65, 64, 57, 65, 60, 69 }
            : new[] { 50, 0, 57, 53, 0, 60, 52, 0 };
        for (int i = 0; i < data.Length; i++)
        {
            double t = (double)i / rate;
            double envelope = System.Math.Min(1, System.Math.Min(t / 2, (seconds - t) / 2));
            double pad = .12 * System.Math.Sin(2 * System.Math.PI * 73.5 * t)
                + .06 * System.Math.Sin(2 * System.Math.PI * 110 * t)
                + .035 * System.Math.Sin(2 * System.Math.PI * 155.5 * t);
            double melody = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                double age = t - n * 4;
                if (notes[n] == 0 || age < 0 || age > 9) continue;
                double hz = 440 * System.Math.Pow(2, (notes[n] - 69) / 12.0);
                double attack = System.Math.Min(1, age / .04);
                melody += attack * System.Math.Exp(-age * .7) *
                    (.28 * System.Math.Sin(2 * System.Math.PI * hz * age)
                    + .07 * System.Math.Sin(2 * System.Math.PI * hz * 2.003 * age));
            }
            data[i] = (float)((pad * (.65 + .25 * System.Math.Sin(t * .7)) + melody) * envelope);
        }
        var clip = AudioClip.Create(menu ? "Last bus - original menu score" : "Empty streets - original ambient score",
            data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
