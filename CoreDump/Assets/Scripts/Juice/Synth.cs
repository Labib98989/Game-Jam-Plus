using UnityEngine;

namespace CoreDump
{
    // A tiny synthesizer, so every sound in the game is made in code: nothing recorded or
    // downloaded. A sound is a few layers mixed together. Each layer is a wave (sine, square, saw,
    // triangle or noise) gliding from one pitch to another under a volume envelope, with optional
    // vibrato and a low-pass filter. Noise is "sample and hold": its pitch is how often it picks a
    // new random level, so low pitches crunch and high ones hiss.
    public static class Synth
    {
        public const int Rate = 44100;
        // Samples between updates of a layer's glide and fade (under a millisecond).
        const int Block = 32;

        public enum Wave { Sine, Square, Saw, Triangle, Noise }

        public struct Layer
        {
            public Wave wave;
            // Pitch in Hz at the start and the end; it glides between them.
            public float from, to;
            public float start, length;
            public float gain;
            // Seconds to full volume, then a fall to silence: 1 is a straight line, higher falls faster.
            public float attack, decay;
            // In Hz; 0 for none.
            public float lowpass;
            public float vibratoRate, vibratoDepth;
            // A square wave's share of time spent high.
            public float duty;
        }

        public static Layer L(Wave wave, float from, float to, float length, float gain = 1f, float start = 0f, float attack = 0.004f,
            float decay = 2f, float lowpass = 0f, float vibratoRate = 0f, float vibratoDepth = 0f, float duty = 0.5f) =>
            new() { wave = wave, from = from, to = to, length = length, gain = gain, start = start, attack = attack, decay = decay,
                lowpass = lowpass, vibratoRate = vibratoRate, vibratoDepth = vibratoDepth, duty = duty };

        public static float Note(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        // Mixes the layers into one sound `length` seconds long. Every sound comes out equally loud at
        // its peak; how loud each one plays is set in Sounds.
        public static float[] Render(float length, params Layer[] layers)
        {
            var data = new float[Mathf.CeilToInt(length * Rate)];
            var random = new System.Random(data.Length);
            foreach (var layer in layers) Add(data, layer, random, false);
            Normalize(data, 0.85f);
            return data;
        }

        // Adds one layer into `data`. With `wrap`, whatever runs past the end comes in again at the
        // start, so a loop has no gap.
        public static void Add(float[] data, Layer l, System.Random random, bool wrap)
        {
            int first = Mathf.RoundToInt(l.start * Rate);
            int count = Mathf.CeilToInt(l.length * Rate);
            float smooth = l.lowpass > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * l.lowpass / Rate) : 1f;
            float ratio = l.to / l.from;
            double phase = 0;
            long step = -1;
            float held = 0f, filtered = 0f;
            // The glide and the fade change slowly, so they're worked out once a block and blended between.
            float glide = l.from, fadeFrom = 1f, fadeTo = 1f;
            for (int i = 0; i < count; i++)
            {
                int at = first + i;
                if (at >= data.Length)
                {
                    if (!wrap) break;
                    at %= data.Length;
                }
                float t = i / (float)Rate, u = i / (float)count;
                if (i % Block == 0)
                {
                    glide = ratio == 1f ? l.from : l.from * Mathf.Pow(ratio, u);
                    fadeFrom = Mathf.Pow(1f - u, l.decay);
                    fadeTo = Mathf.Pow(Mathf.Max(0f, 1f - (i + Block) / (float)count), l.decay);
                }
                float fade = Mathf.Lerp(fadeFrom, fadeTo, i % Block / (float)Block);
                float pitch = glide;
                if (l.vibratoDepth > 0f) pitch *= 1f + l.vibratoDepth * Mathf.Sin(2f * Mathf.PI * l.vibratoRate * t);
                phase += pitch / Rate;
                float p = (float)(phase - System.Math.Floor(phase));
                float v;
                switch (l.wave)
                {
                    case Wave.Sine: v = Mathf.Sin(2f * Mathf.PI * p); break;
                    case Wave.Square: v = p < l.duty ? 1f : -1f; break;
                    case Wave.Saw: v = 2f * p - 1f; break;
                    case Wave.Triangle: v = 1f - 4f * Mathf.Abs(p - 0.5f); break;
                    default:
                        long now = (long)phase;
                        if (now != step)
                        {
                            step = now;
                            held = (float)random.NextDouble() * 2f - 1f;
                        }
                        v = held;
                        break;
                }
                filtered += smooth * (v - filtered);
                float envelope = (t < l.attack ? t / l.attack : 1f) * fade;
                data[at] += filtered * envelope * l.gain;
            }
        }

        public static void Normalize(float[] data, float peak)
        {
            float loudest = 0f;
            foreach (float v in data) loudest = Mathf.Max(loudest, Mathf.Abs(v));
            if (loudest <= 0f) return;
            float scale = peak / loudest;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        public static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
