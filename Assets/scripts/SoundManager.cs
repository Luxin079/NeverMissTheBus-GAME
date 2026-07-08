using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach this to an Empty GameObject in your scene named "SoundManager"
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Collections (Pijltjes naar beneden voor overzicht)")]
    [Tooltip("Sleep hier alle geluiden in die bedoeld zijn om te loopen.")]
    public List<AudioClip> loopSounds = new List<AudioClip>();

    [Tooltip("Sleep hier alle losse effecten/one-shots in.")]
    public List<AudioClip> oneShotSounds = new List<AudioClip>();

    [Tooltip("Sleep hier al je achtergrondmuziek tracks in.")]
    public List<AudioClip> bgmSounds = new List<AudioClip>();

    [Header("Fade Settings")]
    [Tooltip("Hoe lang het duurt voor een geluid om uit te faden.")]
    public float fadeOutTime = 0.3f;

    // Interne speakers die automatisch worden aangemaakt
    private AudioSource loopSource;
    private AudioSource oneShotSource;
    private AudioSource bgmSource;

    private Coroutine loopFadeCoroutine;
    private Coroutine bgmFadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SetupInternalSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupInternalSources()
    {
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.loop = true;

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.loop = false;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
    }

    // --- ONE SHOT SOUNDS ---
    public void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotSource == null) return;

        // Handige extra check: als de clip nog niet in de lijst staat, voeg hem automatisch toe voor het overzicht!
        if (!oneShotSounds.Contains(clip))
        {
            oneShotSounds.Add(clip);
        }

        oneShotSource.PlayOneShot(clip);
    }

    public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        if (!oneShotSounds.Contains(clip))
        {
            oneShotSounds.Add(clip);
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // --- LOOP SOUNDS ---
    public void PlayLoop(AudioClip clip, float pitch = 1f)
    {
        if (clip == null || loopSource == null) return;

        if (!loopSounds.Contains(clip))
        {
            loopSounds.Add(clip);
        }

        if (loopFadeCoroutine != null)
        {
            StopCoroutine(loopFadeCoroutine);
            loopFadeCoroutine = null;
        }

        loopSource.pitch = pitch;
        loopSource.volume = 1f;

        if (loopSource.clip != clip)
        {
            loopSource.clip = clip;
            loopSource.Play();
        }
        else if (!loopSource.isPlaying)
        {
            loopSource.Play();
        }
    }

    public void StopLoopFade()
    {
        if (loopSource == null || !loopSource.isPlaying) return;

        if (loopFadeCoroutine == null)
        {
            loopFadeCoroutine = StartCoroutine(FadeOutLoop());
        }
    }

    public void StopLoopInstant()
    {
        if (loopSource == null) return;

        if (loopFadeCoroutine != null)
        {
            StopCoroutine(loopFadeCoroutine);
            loopFadeCoroutine = null;
        }
        loopSource.Stop();
    }

    private IEnumerator FadeOutLoop()
    {
        float startVolume = loopSource.volume;
        while (loopSource.volume > 0)
        {
            loopSource.volume -= startVolume * Time.deltaTime / fadeOutTime;
            yield return null;
        }
        loopSource.Stop();
        loopSource.volume = startVolume;
        loopFadeCoroutine = null;
    }

    // --- BACKGROUND MUSIC (BGM) ---
    public void PlayBGM(AudioClip clip, float volume = 0.5f)
    {
        if (clip == null || bgmSource == null) return;

        if (!bgmSounds.Contains(clip))
        {
            bgmSounds.Add(clip);
        }

        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }

        bgmSource.volume = volume;

        if (bgmSource.clip != clip)
        {
            bgmSource.clip = clip;
            bgmSource.Play();
        }
        else if (!bgmSource.isPlaying)
        {
            bgmSource.Play();
        }
    }

    public void StopBGM(bool fade = true)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;

        if (fade)
        {
            if (bgmFadeCoroutine == null)
            {
                bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
            }
        }
        else
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }
            bgmSource.Stop();
        }
    }

    private IEnumerator FadeOutBGM()
    {
        float startVolume = bgmSource.volume;
        while (bgmSource.volume > 0)
        {
            bgmSource.volume -= startVolume * Time.deltaTime / fadeOutTime;
            yield return null;
        }
        bgmSource.Stop();
        bgmSource.volume = startVolume;
        bgmFadeCoroutine = null;
    }
}