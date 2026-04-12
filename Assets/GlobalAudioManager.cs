using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(AudioSource))]
public class GlobalAudioManager : MonoBehaviour
{
    public static GlobalAudioManager instance;

    [Header("Track Settings")]
    public string resourcePrefix = "cubehaven";
    public int firstTrackNumber = 11;
    public int lastTrackNumber = 30;

    [Header("Playback Settings")]
    public float fadeOutDuration = 5f;
    public float gapBetweenTracks = 2f;
    public float defaultVolume = 1f;

    [Header("Memory Cleanup")]
    public bool unloadClipAfterPlay = true;
    public bool callUnloadUnusedAssets = true;
    public bool callManualGC = true;

    private AudioSource audioSource;

    // Store PATHS / NAMES, not AudioClip refs
    private List<string> allTrackPaths = new List<string>();
    private List<string> shuffledTrackPaths = new List<string>();
    private int currentTrackIndex = 0;

    private Coroutine playbackRoutine;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.volume = defaultVolume;

        BuildTrackList();
        ReshuffleTracks();
    }

    private void Start()
    {
        if(playbackRoutine == null && shuffledTrackPaths.Count > 0)
        {
            playbackRoutine = StartCoroutine(PlaybackLoop());
        }
    }

    private void BuildTrackList()
    {
        allTrackPaths.Clear();

        for(int i = firstTrackNumber; i <= lastTrackNumber; i++)
        {
            // IMPORTANT: no file extension for Resources.Load
            string clipPath = resourcePrefix + i;
            allTrackPaths.Add(clipPath);
        }

        if(allTrackPaths.Count == 0)
        {
            Debug.LogError("GlobalAudioManager: No track paths were generated.");
        }
    }

    private IEnumerator PlaybackLoop()
    {
        while(true)
        {
            if(shuffledTrackPaths.Count == 0)
            {
                Debug.LogWarning("GlobalAudioManager: No shuffled tracks available.");
                yield break;
            }

            if(currentTrackIndex >= shuffledTrackPaths.Count)
            {
                ReshuffleTracks();
            }

            string nextTrackPath = shuffledTrackPaths[currentTrackIndex];
            currentTrackIndex++;

            yield return StartCoroutine(PlayTrack(nextTrackPath));
            yield return new WaitForSeconds(gapBetweenTracks);
        }
    }

    private IEnumerator PlayTrack(string trackPath)
    {
        if(string.IsNullOrEmpty(trackPath))
            yield break;

        AudioClip clip = Resources.Load<AudioClip>(trackPath);

        if(clip == null)
        {
            Debug.LogWarning($"GlobalAudioManager: Could not find AudioClip in Resources named '{trackPath}'");
            yield break;
        }

        audioSource.clip = clip;
        audioSource.volume = defaultVolume;
        audioSource.Play();

        float clipLength = clip.length;

        if(clipLength <= fadeOutDuration)
        {
            yield return StartCoroutine(FadeOutOverTime(clipLength));
        }
        else
        {
            float playTimeBeforeFade = clipLength - fadeOutDuration;
            yield return new WaitForSeconds(playTimeBeforeFade);
            yield return StartCoroutine(FadeOutOverTime(fadeOutDuration));
        }

        audioSource.Stop();
        audioSource.clip = null;
        audioSource.volume = defaultVolume;

        if(unloadClipAfterPlay)
        {
            // Let one frame pass so Unity fully detaches the clip from the AudioSource
            yield return null;

            Resources.UnloadAsset(clip);
            clip = null;

            if(callUnloadUnusedAssets)
            {
                yield return Resources.UnloadUnusedAssets();
            }

            if(callManualGC)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    private IEnumerator FadeOutOverTime(float duration)
    {
        if(duration <= 0f)
            yield break;

        float startVolume = defaultVolume;
        float timer = 0f;

        while(timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        audioSource.volume = 0f;
    }

    private void ReshuffleTracks()
    {
        shuffledTrackPaths = new List<string>(allTrackPaths);

        for(int i = 0; i < shuffledTrackPaths.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, shuffledTrackPaths.Count);
            string temp = shuffledTrackPaths[i];
            shuffledTrackPaths[i] = shuffledTrackPaths[randomIndex];
            shuffledTrackPaths[randomIndex] = temp;
        }

        currentTrackIndex = 0;
    }
}