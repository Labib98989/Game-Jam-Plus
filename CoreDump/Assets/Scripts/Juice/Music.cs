using UnityEngine;
using static CoreDump.Synth;

namespace CoreDump
{
    public enum Tune { None, Calm, Combat, Alarm }

    // The music, made in code like the sound effects (see Synth): an 8-bar loop per track, of drums,
    // bass, chords and an arpeggio. Calm plays over the menu, the workshop and the tutorial; Combat
    // in the rooms; Alarm in the escape shaft, with a siren over the top.
    public static class Music
    {
        const int Bars = 8, Steps = 16;

        // Each track's speed, and the chord under each bar: root notes as MIDI numbers, minor or major.
        public static AudioClip Make(Tune track)
        {
            return track switch
            {
                Tune.Calm => Loop("Calm", 92f, new[] { 45, 41, 48, 43, 45, 41, 43, 40 }, new[] { true, false, false, false, true, false, false, false },
                    drums: false, arpGain: 0.12f, padGain: 0.1f, bassGain: 0.3f, siren: false),
                Tune.Combat => Loop("Combat", 132f, new[] { 45, 45, 41, 43, 45, 45, 41, 40 }, new[] { true, true, false, false, true, true, false, false },
                    drums: true, arpGain: 0.1f, padGain: 0.05f, bassGain: 0.4f, siren: false),
                Tune.Alarm => Loop("Alarm", 150f, new[] { 45, 45, 46, 45, 45, 45, 46, 40 }, new[] { true, true, false, true, true, true, false, false },
                    drums: true, arpGain: 0.06f, padGain: 0.04f, bassGain: 0.4f, siren: true),
                _ => null,
            };
        }

        static AudioClip Loop(string name, float bpm, int[] roots, bool[] minor, bool drums, float arpGain, float padGain, float bassGain, bool siren)
        {
            float step = 60f / bpm / 4f;
            float length = step * Steps * Bars;
            var data = new float[Mathf.RoundToInt(length * Rate)];
            var random = new System.Random(name.Length);

            for (int bar = 0; bar < Bars; bar++)
            {
                int root = roots[bar];
                var chord = minor[bar] ? new[] { 0, 3, 7 } : new[] { 0, 4, 7 };
                float barStart = bar * Steps * step;

                // A soft chord held across the bar.
                foreach (int tone in chord)
                    Add(data, L(Wave.Triangle, Note(root + 24 + tone), Note(root + 24 + tone), step * Steps, padGain, start: barStart,
                        attack: step * 4f, decay: 0.6f, lowpass: 1500), random, true);

                for (int s = 0; s < Steps; s++)
                {
                    float at = barStart + s * step;
                    // Bass: eighth notes on the root, jumping an octave on the off-beats when it drives.
                    if (s % 2 == 0)
                    {
                        float note = Note(root + (drums && s % 4 == 2 ? 12 : 0));
                        Add(data, L(drums ? Wave.Saw : Wave.Sine, note, note, step * 1.8f, bassGain, start: at, decay: 1.2f, lowpass: 700), random, true);
                    }
                    // Arpeggio: up through the chord, an octave higher every other pass.
                    if (drums || s % 2 == 0)
                    {
                        int i = drums ? s : s / 2;
                        float note = Note(root + 36 + chord[i % 3] + 12 * (i / 3 % 2));
                        Add(data, L(drums ? Wave.Square : Wave.Triangle, note, note, step * (drums ? 0.8f : 1.8f), arpGain, start: at, decay: 1.5f,
                            lowpass: 3000, duty: 0.25f), random, true);
                    }
                    if (!drums)
                    {
                        if (s % 4 == 2) Add(data, L(Wave.Noise, 11000, 11000, 0.03f, 0.04f, start: at, decay: 3f), random, true);
                        continue;
                    }
                    if (s % 4 == 0) Add(data, L(Wave.Sine, 150, 42, 0.18f, 0.9f, start: at, decay: 2.5f), random, true);
                    if (s % 8 == 4)
                    {
                        Add(data, L(Wave.Noise, 7000, 3000, 0.16f, 0.4f, start: at, decay: 2f, lowpass: 5000), random, true);
                        Add(data, L(Wave.Triangle, 210, 160, 0.08f, 0.3f, start: at), random, true);
                    }
                    if (s % 2 == 1) Add(data, L(Wave.Noise, 12000, 12000, 0.03f, s % 4 == 3 ? 0.1f : 0.06f, start: at, decay: 3f), random, true);
                }

                // The siren: a wailing tone sweeping up and down twice a bar.
                if (siren)
                    for (int half = 0; half < 2; half++)
                    {
                        float at = barStart + half * Steps * step * 0.5f;
                        float time = Steps * step * 0.5f;
                        Add(data, L(Wave.Square, 620, 920, time * 0.5f, 0.07f, start: at, attack: 0.05f, decay: 0.2f, lowpass: 2200, duty: 0.4f), random, true);
                        Add(data, L(Wave.Square, 920, 620, time * 0.5f, 0.07f, start: at + time * 0.5f, attack: 0.02f, decay: 0.5f, lowpass: 2200, duty: 0.4f), random, true);
                    }
            }

            Normalize(data, 0.8f);
            return Clip(name, data);
        }
    }
}
