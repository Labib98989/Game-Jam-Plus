using static CoreDump.Synth;

namespace CoreDump
{
    public enum Sound
    {
        None,
        // The robot's guns and parts.
        Blaster, Rivet, Rocket, Welder, Zap, Fist, BombDrop, Reload, Dash, Spring, Ram, GrappleFire, GrappleHook,
        // The robot moving, and getting hurt.
        Land, Slide, WallRun, Slam, PartLost, PartBack, HullHit, HeadBeep,
        // Fights.
        Explosion, EnemyHit, EnemyDie, BigDie, Glob, EnemyShot, Cannon, Chitter, Screech, BombMark, BombBlast, Spawn, Splat,
        // The room, the run and the screens.
        PadLaunch, Pickup, Place, Remove, Error, Click, StepDone, ExitOpen, WaveStart, RoomClear, Death, Victory,
    }

    // Every sound effect: what it's made of (see Synth), how loud it plays, and whether it's heard
    // from where it happens or the same everywhere. The robot's own sounds are heard everywhere.
    public static class Sounds
    {
        public static (float volume, bool spatial) Mix(Sound sound) => sound switch
        {
            Sound.Blaster => (0.55f, false),
            Sound.Rivet => (0.28f, false),
            Sound.Rocket => (0.5f, false),
            Sound.Welder => (0.22f, false),
            Sound.Zap => (0.4f, false),
            Sound.Fist => (0.5f, false),
            Sound.BombDrop => (0.4f, false),
            Sound.Reload => (0.3f, false),
            Sound.Dash => (0.5f, false),
            Sound.Spring => (0.45f, false),
            Sound.Ram => (0.5f, false),
            Sound.GrappleFire => (0.4f, false),
            Sound.GrappleHook => (0.45f, true),
            Sound.Land => (0.45f, false),
            Sound.Slide => (0.35f, false),
            Sound.WallRun => (0.3f, false),
            Sound.Slam => (0.8f, true),
            Sound.PartLost => (0.8f, false),
            Sound.PartBack => (0.6f, false),
            Sound.HullHit => (0.8f, false),
            Sound.HeadBeep => (0.7f, true),
            Sound.Explosion => (0.9f, true),
            Sound.EnemyHit => (0.3f, false),
            Sound.EnemyDie => (0.6f, true),
            Sound.BigDie => (0.9f, true),
            Sound.Glob => (0.5f, true),
            Sound.EnemyShot => (0.4f, true),
            Sound.Cannon => (0.8f, true),
            Sound.Chitter => (0.45f, true),
            Sound.Screech => (0.6f, true),
            Sound.BombMark => (0.5f, true),
            Sound.BombBlast => (0.8f, true),
            Sound.Spawn => (0.5f, true),
            Sound.Splat => (0.3f, true),
            Sound.PadLaunch => (0.6f, false),
            Sound.Pickup => (0.5f, false),
            Sound.Place => (0.45f, false),
            Sound.Remove => (0.45f, false),
            Sound.Error => (0.4f, false),
            Sound.Click => (0.35f, false),
            Sound.StepDone => (0.55f, false),
            Sound.ExitOpen => (0.6f, false),
            Sound.WaveStart => (0.5f, false),
            Sound.RoomClear => (0.65f, false),
            Sound.Death => (0.8f, false),
            Sound.Victory => (0.8f, false),
            _ => (0f, false),
        };

        // How many of one sound can play at once, so a pack of Skitters doesn't drown everything out.
        public static int Voices(Sound sound) => sound switch
        {
            Sound.EnemyHit or Sound.Splat or Sound.Chitter or Sound.Reload => 3,
            Sound.HeadBeep or Sound.Death or Sound.Victory or Sound.RoomClear or Sound.ExitOpen or Sound.StepDone => 1,
            _ => 4,
        };

        // Sounds that keep their exact pitch: tunes and jingles.
        public static bool Tuned(Sound sound) => sound is Sound.Pickup or Sound.StepDone or Sound.ExitOpen or Sound.RoomClear
            or Sound.Death or Sound.Victory or Sound.WaveStart or Sound.HeadBeep or Sound.Click;

        public static float[] Make(Sound sound) => sound switch
        {
            Sound.Blaster => Render(0.35f, L(Wave.Noise, 7000, 1500, 0.3f, 1f, decay: 3f, lowpass: 3500), L(Wave.Sine, 150, 45, 0.2f, 0.9f), L(Wave.Square, 320, 80, 0.08f, 0.3f)),
            Sound.Rivet => Render(0.08f, L(Wave.Square, 1500, 700, 0.06f, 0.5f, decay: 3f, duty: 0.3f), L(Wave.Noise, 9000, 9000, 0.03f, 0.4f, decay: 4f)),
            Sound.Rocket => Render(0.6f, L(Wave.Noise, 3000, 800, 0.55f, 1f, attack: 0.03f, decay: 1.5f, lowpass: 1800), L(Wave.Saw, 120, 60, 0.4f, 0.4f, lowpass: 600)),
            Sound.Welder => Render(0.12f, L(Wave.Noise, 7000, 5000, 0.12f, 0.8f, attack: 0.01f, decay: 1f, lowpass: 4000)),
            Sound.Zap => Render(0.2f, L(Wave.Square, 1800, 600, 0.18f, 0.6f, vibratoRate: 60, vibratoDepth: 0.3f, duty: 0.25f), L(Wave.Noise, 12000, 4000, 0.1f, 0.3f, decay: 3f)),
            Sound.Fist => Render(0.35f, L(Wave.Noise, 2000, 400, 0.3f, 0.7f, attack: 0.02f, decay: 1.5f, lowpass: 1200), L(Wave.Sine, 200, 70, 0.15f, 0.8f)),
            Sound.BombDrop => Render(0.2f, L(Wave.Square, 600, 200, 0.18f, 0.5f, decay: 1.5f)),
            Sound.Reload => Render(0.3f, L(Wave.Square, 2200, 1800, 0.03f, 0.6f, decay: 3f, duty: 0.2f),
                L(Wave.Square, 1600, 1300, 0.04f, 0.6f, start: 0.14f, decay: 3f, duty: 0.2f), L(Wave.Noise, 5000, 5000, 0.02f, 0.4f, start: 0.14f, decay: 4f)),
            Sound.Dash => Render(0.3f, L(Wave.Noise, 2000, 6000, 0.28f, 0.8f, attack: 0.03f, decay: 1.2f, lowpass: 3000), L(Wave.Sine, 300, 600, 0.15f, 0.3f)),
            Sound.Spring => Render(0.35f, L(Wave.Triangle, 180, 520, 0.3f, 1f, decay: 1.5f, vibratoRate: 18, vibratoDepth: 0.08f), L(Wave.Sine, 90, 140, 0.1f, 0.5f)),
            Sound.Ram => Render(0.4f, L(Wave.Noise, 1500, 5000, 0.35f, 0.8f, attack: 0.02f, decay: 1.3f, lowpass: 3000), L(Wave.Saw, 90, 160, 0.3f, 0.4f, lowpass: 800)),
            Sound.GrappleFire => Render(0.18f, L(Wave.Square, 500, 2000, 0.15f, 0.5f, decay: 1f, duty: 0.3f)),
            Sound.GrappleHook => Render(0.15f, L(Wave.Square, 1800, 1200, 0.05f, 0.6f, decay: 3f), L(Wave.Noise, 6000, 3000, 0.08f, 0.4f, decay: 3f)),
            Sound.Land => Render(0.2f, L(Wave.Sine, 120, 45, 0.18f, 1f), L(Wave.Noise, 1500, 300, 0.12f, 0.5f, decay: 3f, lowpass: 900)),
            Sound.Slide => Render(0.45f, L(Wave.Noise, 3000, 1500, 0.45f, 0.8f, attack: 0.03f, decay: 1.2f, lowpass: 2200)),
            Sound.WallRun => Render(0.35f, L(Wave.Noise, 5000, 4000, 0.35f, 0.5f, attack: 0.02f, decay: 1f, lowpass: 3500),
                L(Wave.Square, 900, 1100, 0.3f, 0.12f, vibratoRate: 30, vibratoDepth: 0.03f, duty: 0.15f)),
            Sound.Slam => Render(0.6f, L(Wave.Sine, 80, 30, 0.5f, 1f, decay: 1.6f), L(Wave.Noise, 2000, 200, 0.5f, 0.8f, lowpass: 1000)),
            // A clang: a few out-of-tune ringing tones, like struck metal, and a sharp click on top.
            Sound.PartLost => Render(0.7f, L(Wave.Sine, 523, 515, 0.6f, 0.6f, decay: 2.5f), L(Wave.Sine, 1307, 1300, 0.5f, 0.45f, decay: 3f),
                L(Wave.Sine, 2011, 2000, 0.4f, 0.35f, decay: 3.5f), L(Wave.Sine, 3150, 3140, 0.3f, 0.25f, decay: 4f), L(Wave.Noise, 8000, 3000, 0.05f, 0.8f, decay: 4f)),
            Sound.PartBack => Render(0.16f, L(Wave.Square, 900, 1800, 0.08f, 0.5f, decay: 1.5f, duty: 0.25f),
                L(Wave.Square, 1800, 1800, 0.06f, 0.4f, start: 0.07f, duty: 0.25f), L(Wave.Noise, 9000, 9000, 0.02f, 0.5f, decay: 4f)),
            Sound.HullHit => Render(0.35f, L(Wave.Sine, 100, 40, 0.3f, 1f, decay: 1.5f), L(Wave.Noise, 3000, 400, 0.25f, 0.8f, lowpass: 2000), L(Wave.Saw, 220, 110, 0.2f, 0.3f, lowpass: 900)),
            Sound.HeadBeep => Render(0.1f, L(Wave.Sine, 1500, 1500, 0.09f, 0.8f, decay: 0.5f), L(Wave.Square, 1500, 1500, 0.09f, 0.15f, decay: 0.5f)),
            Sound.Explosion => Render(1f, L(Wave.Noise, 4000, 300, 0.9f, 1f, decay: 2.2f, lowpass: 1500), L(Wave.Sine, 90, 30, 0.6f, 1f, decay: 1.5f),
                L(Wave.Noise, 800, 100, 1f, 0.6f, decay: 1.2f, lowpass: 400)),
            Sound.EnemyHit => Render(0.06f, L(Wave.Square, 1200, 700, 0.05f, 0.4f, duty: 0.3f), L(Wave.Noise, 6000, 6000, 0.04f, 0.5f, decay: 3f)),
            // A squelch: wet noise and a wobbling drop.
            Sound.EnemyDie => Render(0.4f, L(Wave.Noise, 1800, 300, 0.35f, 0.8f, decay: 1.8f, lowpass: 1400), L(Wave.Sine, 420, 80, 0.3f, 0.7f, decay: 1.5f, vibratoRate: 25, vibratoDepth: 0.1f)),
            Sound.BigDie => Render(0.9f, L(Wave.Noise, 2500, 150, 0.8f, 1f, decay: 1.8f, lowpass: 1600), L(Wave.Sine, 160, 35, 0.7f, 1f, decay: 1.4f), L(Wave.Saw, 300, 60, 0.5f, 0.4f, decay: 1.5f, lowpass: 700)),
            Sound.Glob => Render(0.25f, L(Wave.Sine, 260, 520, 0.08f, 0.7f, decay: 1f), L(Wave.Sine, 520, 180, 0.15f, 0.7f, start: 0.07f, decay: 1.5f, vibratoRate: 20, vibratoDepth: 0.1f)),
            Sound.EnemyShot => Render(0.12f, L(Wave.Square, 1100, 350, 0.1f, 0.5f, decay: 1.5f, duty: 0.35f)),
            Sound.Cannon => Render(0.45f, L(Wave.Noise, 2500, 300, 0.4f, 0.9f, lowpass: 1200), L(Wave.Sine, 110, 40, 0.35f, 1f, decay: 1.5f)),
            Sound.Chitter => Render(0.15f, L(Wave.Noise, 3000, 2500, 0.14f, 0.6f, attack: 0.01f, decay: 1f, vibratoRate: 40, vibratoDepth: 0.3f),
                L(Wave.Square, 1400, 1800, 0.12f, 0.2f, vibratoRate: 45, vibratoDepth: 0.2f, duty: 0.2f)),
            Sound.Screech => Render(0.45f, L(Wave.Saw, 700, 1300, 0.4f, 0.5f, attack: 0.05f, decay: 1f, lowpass: 3000, vibratoRate: 12, vibratoDepth: 0.05f),
                L(Wave.Noise, 4000, 4000, 0.35f, 0.3f, decay: 1f, lowpass: 3000)),
            Sound.BombMark => Render(0.55f, L(Wave.Square, 700, 1200, 0.5f, 0.35f, attack: 0.02f, decay: 0.6f, vibratoRate: 8, vibratoDepth: 0.04f, duty: 0.4f)),
            Sound.BombBlast => Render(0.6f, L(Wave.Noise, 4000, 300, 0.5f, 1f, decay: 2.2f, lowpass: 1800), L(Wave.Sine, 110, 40, 0.4f, 0.9f, decay: 1.6f)),
            Sound.Spawn => Render(0.65f, L(Wave.Sine, 180, 900, 0.6f, 0.6f, attack: 0.1f, decay: 1f, vibratoRate: 9, vibratoDepth: 0.06f),
                L(Wave.Noise, 1000, 6000, 0.6f, 0.25f, attack: 0.2f, decay: 1f, lowpass: 2500)),
            Sound.Splat => Render(0.14f, L(Wave.Noise, 2000, 600, 0.12f, 0.6f, lowpass: 1500), L(Wave.Sine, 300, 120, 0.08f, 0.4f)),
            Sound.PadLaunch => Render(0.45f, L(Wave.Sine, 140, 700, 0.35f, 0.9f, decay: 1.2f, vibratoRate: 14, vibratoDepth: 0.05f),
                L(Wave.Noise, 1500, 6000, 0.4f, 0.5f, attack: 0.03f, decay: 1.2f, lowpass: 3500)),
            Sound.Pickup => Arpeggio(new[] { 76f, 81f, 88f }, 0.07f, 0.18f, Wave.Square),
            Sound.Place => Render(0.07f, L(Wave.Square, 700, 500, 0.05f, 0.5f, duty: 0.3f), L(Wave.Noise, 4000, 2000, 0.04f, 0.4f, decay: 3f)),
            Sound.Remove => Render(0.08f, L(Wave.Square, 500, 300, 0.06f, 0.5f, duty: 0.3f)),
            Sound.Error => Render(0.24f, L(Wave.Square, 140, 120, 0.22f, 0.5f, decay: 0.6f), L(Wave.Square, 147, 125, 0.22f, 0.4f, decay: 0.6f)),
            Sound.Click => Render(0.04f, L(Wave.Square, 1600, 1400, 0.03f, 0.4f, duty: 0.2f)),
            Sound.StepDone => Render(0.55f, L(Wave.Sine, 1319, 1319, 0.5f, 0.6f, decay: 2.5f), L(Wave.Sine, 1976, 1976, 0.4f, 0.35f, start: 0.06f, decay: 3f)),
            Sound.ExitOpen => Arpeggio(new[] { 84f, 88f, 91f, 96f }, 0.09f, 0.35f, Wave.Sine),
            Sound.WaveStart => Render(0.45f, L(Wave.Square, 587, 587, 0.2f, 0.5f, decay: 0.4f, lowpass: 2500, duty: 0.4f),
                L(Wave.Square, 440, 440, 0.22f, 0.5f, start: 0.21f, decay: 0.6f, lowpass: 2500, duty: 0.4f)),
            Sound.RoomClear => Arpeggio(new[] { 72f, 76f, 79f, 84f }, 0.09f, 0.4f, Wave.Square),
            Sound.Death => Render(1.3f, L(Wave.Saw, 440, 40, 1.2f, 0.7f, decay: 1.2f, lowpass: 2000), L(Wave.Noise, 3000, 200, 0.6f, 0.4f, lowpass: 1200)),
            Sound.Victory => Render(1.3f, L(Wave.Square, Note(72), Note(72), 0.12f, 0.4f, decay: 0.8f, duty: 0.25f),
                L(Wave.Square, Note(76), Note(76), 0.12f, 0.4f, start: 0.13f, decay: 0.8f, duty: 0.25f),
                L(Wave.Square, Note(79), Note(79), 0.12f, 0.4f, start: 0.26f, decay: 0.8f, duty: 0.25f),
                L(Wave.Square, Note(84), Note(84), 0.9f, 0.45f, start: 0.39f, decay: 1.5f, vibratoRate: 6, vibratoDepth: 0.01f, duty: 0.25f),
                L(Wave.Triangle, Note(72), Note(72), 0.9f, 0.5f, start: 0.39f, decay: 1.5f)),
            _ => new float[1],
        };

        // Notes (as MIDI numbers) one after another, `gap` seconds apart, the last one ringing on.
        static float[] Arpeggio(float[] notes, float gap, float ring, Wave wave)
        {
            var layers = new Layer[notes.Length];
            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;
                layers[i] = L(wave, Note(notes[i]), Note(notes[i]), last ? ring : gap * 1.6f, 0.5f, start: i * gap, decay: last ? 2f : 1f, duty: 0.25f);
            }
            return Render(gap * (notes.Length - 1) + ring + 0.02f, layers);
        }
    }
}
