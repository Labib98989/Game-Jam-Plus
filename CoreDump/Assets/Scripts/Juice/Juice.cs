using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // Game feel in one place: sound effects, music, screen shake, hit-stop (a split-second freeze on
    // a big hit), and the flashes and puffs that go with them. It lives on one object in the scene;
    // with none there, every call quietly does nothing. Every sound and track is made in code when
    // the game starts (see Sounds and Music). The volumes are saved between sessions.
    [DefaultExecutionOrder(-200)]
    public class Juice : MonoBehaviour
    {
        [Tooltip("Muzzle flashes and snap-back flashes.")]
        public Material flashMaterial;
        [Tooltip("Sparks from slides, wall-runs and hits.")]
        public Material sparkMaterial;
        [Tooltip("Dust from hard landings.")]
        public Material dustMaterial;
        [Tooltip("What's left when an alien bursts.")]
        public Material gooMaterial;
        [Tooltip("Rings from pads and slams.")]
        public Material ringMaterial;

        const int Voices = 32;
        const string SoundKey = "CoreDump.SoundVolume", MusicKey = "CoreDump.MusicVolume";
        const float MusicLevel = 0.35f, MusicFade = 1.2f;
        const float StopScale = 0.05f;
        // How fast a shake dies down, per second, from the strongest.
        const float ShakeFade = 1.6f;

        static Juice instance;
        static float soundVolume = 1f, musicVolume = 1f;

        AudioClip[] clips;
        readonly Dictionary<Tune, AudioClip> tracks = new();
        AudioSource[] voices;
        Sound[] playing;
        float[] startedAt, lastPlayed;
        AudioSource music, fading;
        Tune track;
        float trauma, stopUntil;
        bool stopping;

        public static float SoundVolume
        {
            get => soundVolume;
            set
            {
                soundVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SoundKey, soundVolume);
            }
        }

        public static float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, musicVolume);
            }
        }

        // How hard the camera shakes right now, from 0 to 1. Grows with the square of the trauma,
        // so small knocks stay small.
        public static float ShakeAmount => instance ? instance.trauma * instance.trauma : 0f;
        // When the robot was last hurt and how badly (0 to 1), and when it last hit and killed
        // something, in unscaled time. The HUD flashes on these.
        public static float HurtAt { get; private set; } = -10f;
        public static float HurtStrength { get; private set; }
        public static float HitAt { get; private set; } = -10f;
        public static float KillAt { get; private set; } = -10f;

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            soundVolume = PlayerPrefs.GetFloat(SoundKey, 1f);
            musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);

            var sounds = (Sound[])System.Enum.GetValues(typeof(Sound));
            clips = new AudioClip[sounds.Length];
            foreach (var sound in sounds)
                if (sound != Sound.None) clips[(int)sound] = Synth.Clip(sound.ToString(), Sounds.Make(sound));
            foreach (var t in new[] { Tune.Calm, Tune.Combat, Tune.Alarm }) tracks[t] = Music.Make(t);

            voices = new AudioSource[Voices];
            playing = new Sound[Voices];
            startedAt = new float[Voices];
            lastPlayed = new float[sounds.Length];
            for (int i = 0; i < Voices; i++)
            {
                var voice = new GameObject($"Voice {i}").AddComponent<AudioSource>();
                voice.transform.SetParent(transform, false);
                voice.playOnAwake = false;
                voice.dopplerLevel = 0f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.minDistance = 12f;
                voice.maxDistance = 160f;
                voices[i] = voice;
            }
            music = NewMusicSource();
            fading = NewMusicSource();
        }

        AudioSource NewMusicSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            return source;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            trauma = Mathf.MoveTowards(trauma, 0f, ShakeFade * dt);
            if (stopping && Time.unscaledTime >= stopUntil)
            {
                stopping = false;
                // Only undo our own slow-down: a pause or a death screen that came in meanwhile wins.
                if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
            }
            music.volume = Mathf.MoveTowards(music.volume, track == Tune.None ? 0f : MusicLevel * musicVolume, dt / MusicFade);
            fading.volume = Mathf.MoveTowards(fading.volume, 0f, dt / MusicFade);
            if (fading.isPlaying && fading.volume <= 0f) fading.Stop();
        }

        // ---- Sound ----

        // Plays a sound: from `at` if it's one heard from where it happens, otherwise the same everywhere.
        public static void Play(Sound sound, Vector3? at = null, float volume = 1f)
        {
            if (instance && sound != Sound.None) instance.PlayVoice(sound, at, volume);
        }

        void PlayVoice(Sound sound, Vector3? at, float volume)
        {
            int id = (int)sound;
            if (Time.unscaledTime - lastPlayed[id] < 0.03f) return;
            int same = 0, free = -1, oldest = 0;
            for (int i = 0; i < Voices; i++)
            {
                if (!voices[i].isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (playing[i] == sound) same++;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            if (same >= Sounds.Voices(sound)) return;

            int v = free >= 0 ? free : oldest;
            var voice = voices[v];
            var (level, spatial) = Sounds.Mix(sound);
            voice.Stop();
            voice.clip = clips[id];
            voice.volume = level * volume * soundVolume;
            voice.pitch = Sounds.Tuned(sound) ? 1f : Random.Range(0.93f, 1.07f);
            bool placed = spatial && at.HasValue;
            voice.spatialBlend = placed ? 1f : 0f;
            if (placed) voice.transform.position = at.Value;
            voice.Play();
            playing[v] = sound;
            startedAt[v] = lastPlayed[id] = Time.unscaledTime;
        }

        // Fades over to another track, or to silence.
        public static void PlayMusic(Tune next)
        {
            if (!instance || next == instance.track) return;
            instance.Switch(next);
        }

        void Switch(Tune next)
        {
            track = next;
            (music, fading) = (fading, music);
            if (next == Tune.None) return;
            music.clip = tracks[next];
            music.volume = 0f;
            music.Play();
        }

        // ---- Shake, hit-stop and the HUD's flashes ----

        public static void Shake(float amount)
        {
            if (instance) instance.trauma = Mathf.Min(1f, instance.trauma + amount);
        }

        // A shake from something at `point`, weaker the further it is from the camera, and nothing past `range`.
        public static void ShakeAt(Vector3 point, float amount, float range)
        {
            var cam = Camera.main;
            if (cam) Shake(amount * Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, point) / range));
        }

        // Slows the action almost to a stop for a moment, so a big hit lands. Not while paused or already stopped.
        public static void Stop(float seconds)
        {
            if (!instance || !Mathf.Approximately(Time.timeScale, 1f)) return;
            Time.timeScale = StopScale;
            instance.stopUntil = Time.unscaledTime + seconds;
            instance.stopping = true;
        }

        public static void Hurt(float strength)
        {
            HurtAt = Time.unscaledTime;
            HurtStrength = Mathf.Clamp01(strength);
        }

        public static void Hit(bool kill)
        {
            HitAt = Time.unscaledTime;
            if (kill) KillAt = HitAt;
        }

        // ---- Effects ----

        public static void Flash(Vector3 at, float size)
        {
            if (instance && instance.flashMaterial) FxGrow.Spawn(PrimitiveType.Sphere, at, Vector3.one * size, 0.07f, instance.flashMaterial);
        }

        public static void Sparks(Vector3 at, int count, float speed, float size = 0.08f)
        {
            if (instance && instance.sparkMaterial) FxSpark.Burst(at, count, speed, instance.sparkMaterial, size);
        }

        // A flat puff on the ground and a few specks of dust, `size` meters across.
        public static void Dust(Vector3 at, float size)
        {
            if (!instance || !instance.dustMaterial) return;
            FxGrow.Spawn(PrimitiveType.Cylinder, at, new Vector3(size, 0.06f, size), 0.3f, instance.dustMaterial);
            FxSpark.Burst(at + Vector3.up * 0.2f, Mathf.RoundToInt(4f + size * 2f), 2f + size, instance.dustMaterial, 0.15f);
        }

        // An alien bursting: a splash and flying blobs, bigger for bigger aliens.
        public static void Goo(Vector3 at, float size)
        {
            if (!instance || !instance.gooMaterial) return;
            FxGrow.Spawn(PrimitiveType.Sphere, at, Vector3.one * size * 1.6f, 0.25f, instance.gooMaterial);
            FxSpark.Burst(at, Mathf.RoundToInt(8f + size * 4f), 6f + size * 2f, instance.gooMaterial, 0.12f * size);
        }

        // A flat ring spreading out over the ground.
        public static void Ring(Vector3 at, float size)
        {
            if (instance && instance.ringMaterial) FxGrow.Spawn(PrimitiveType.Cylinder, at, new Vector3(size, 0.05f, size), 0.35f, instance.ringMaterial);
        }
    }
}
