using System.Collections;
using UnityEngine;

namespace SpinStay
{
    /// <summary>
    /// Plays game SFX. Subscribes to the walker's fall event and the roulette's stop event,
    /// and drives a tense rope loop while the walker is teetering at the limit. All sources
    /// are 2D so the player always hears cues at full volume regardless of camera position.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class AudioManager : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private TightropeWalker walker;
        [SerializeField] private Roulette roulette;

        [Header("Fall SFX")]
        [SerializeField] private AudioClip fallScream;
        [SerializeField, Range(0f, 1f)] private float fallScreamVolume = 1f;
        [SerializeField] private AudioClip waterSplash;
        [SerializeField, Range(0f, 1f)] private float waterSplashVolume = 1f;
        [Tooltip("Seconds after the fall starts before the splash plays. Match to the walker's fallAnimationDuration.")]
        [SerializeField, Min(0f)] private float waterSplashDelay = 1.0f;

        [Header("Roulette SFX")]
        [SerializeField] private AudioClip rouletteStop;
        [SerializeField, Range(0f, 1f)] private float rouletteStopVolume = 0.7f;

        [Header("Teeter loop")]
        [SerializeField] private AudioClip ropeTense;
        [SerializeField, Range(0f, 1f)] private float ropeTenseVolume = 0.6f;
        [Tooltip("Seconds the rope-tense loop fades in/out as teeter state changes.")]
        [SerializeField, Min(0.01f)] private float ropeFadeDuration = 0.25f;

        AudioSource sfxSource;
        AudioSource ropeSource;
        float ropeCurrentVolume;

        void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            ropeSource = gameObject.AddComponent<AudioSource>();
            ropeSource.playOnAwake = false;
            ropeSource.loop = true;
            ropeSource.spatialBlend = 0f;
            ropeSource.volume = 0f;
            ropeSource.clip = ropeTense;
        }

        void Start()
        {
            if (walker == null) walker = FindFirstObjectByType<TightropeWalker>();
            if (roulette == null) roulette = FindFirstObjectByType<Roulette>();
            if (walker != null) walker.OnFell += HandleFell;
            if (roulette != null) roulette.OnStopped += HandleRouletteStopped;
        }

        void OnDestroy()
        {
            if (walker != null) walker.OnFell -= HandleFell;
            if (roulette != null) roulette.OnStopped -= HandleRouletteStopped;
        }

        void Update()
        {
            if (ropeSource == null || ropeTense == null) return;

            bool teetering = walker != null && !walker.IsFallen && walker.IsTeetering;
            float target = teetering ? ropeTenseVolume : 0f;
            float step = Time.deltaTime / Mathf.Max(0.01f, ropeFadeDuration) * ropeTenseVolume;
            ropeCurrentVolume = Mathf.MoveTowards(ropeCurrentVolume, target, step);

            if (teetering && !ropeSource.isPlaying) ropeSource.Play();
            ropeSource.volume = ropeCurrentVolume;
            if (!teetering && ropeCurrentVolume <= 0.001f && ropeSource.isPlaying) ropeSource.Stop();
        }

        void HandleFell()
        {
            if (ropeSource != null)
            {
                ropeSource.Stop();
                ropeCurrentVolume = 0f;
                ropeSource.volume = 0f;
            }
            if (fallScream != null) sfxSource.PlayOneShot(fallScream, fallScreamVolume);
            if (waterSplash != null)
            {
                if (waterSplashDelay <= 0f) sfxSource.PlayOneShot(waterSplash, waterSplashVolume);
                else StartCoroutine(PlayDelayed(waterSplash, waterSplashVolume, waterSplashDelay));
            }
        }

        void HandleRouletteStopped(RouletteOption opt, int idx)
        {
            if (rouletteStop != null) sfxSource.PlayOneShot(rouletteStop, rouletteStopVolume);
        }

        IEnumerator PlayDelayed(AudioClip clip, float volume, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (sfxSource != null) sfxSource.PlayOneShot(clip, volume);
        }
    }
}
