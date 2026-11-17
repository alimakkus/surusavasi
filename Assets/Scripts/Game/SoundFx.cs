using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Ses efektleri kodla üretilir (dosya gerekmez). Prototipteki ses tarifleriyle aynı.</summary>
    public class SoundFx : MonoBehaviour
    {
        public static SoundFx I { get; private set; }
        public static bool Muted
        {
            get => PlayerPrefs.GetInt("surusavasi-mute", 0) == 1;
            set { PlayerPrefs.SetInt("surusavasi-mute", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool Vibrate
        {
            get => PlayerPrefs.GetInt("surusavasi-vib", 1) == 1;
            set { PlayerPrefs.SetInt("surusavasi-vib", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        const int SR = 44100;
        AudioSource src;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> last = new Dictionary<string, float>();
        static readonly Dictionary<string, float> Gap = new Dictionary<string, float>
        { { "shot", .11f }, { "hit", .07f }, { "kill", .05f }, { "board", .12f }, { "hurt", .08f }, { "spit", .15f }, { "shot_pierce", .11f }, { "shot_flame", .07f }, { "shot_rocket", .2f } };
        static readonly HashSet<string> Vib = new HashSet<string> { "hurt", "boom", "skill", "boss", "lose", "win" };

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Sound"); DontDestroyOnLoad(go);
            I = go.AddComponent<SoundFx>();
        }

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            Build();
        }

        public static void Play(string name)
        {
            if (I == null) return;
            if (Vibrate && Vib.Contains(name)) { /* Telefonda kısa titreşim */
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }
            if (Muted) return;
            float now = Time.unscaledTime;
            if (Gap.TryGetValue(name, out var g) && I.last.TryGetValue(name, out var l) && now - l < g) return;
            I.last[name] = now;
            if (I.clips.TryGetValue(name, out var c)) I.src.PlayOneShot(c);
        }

        // ---------- Sentez ----------
        enum Wave { Square, Triangle, Saw, Sine }
        class Mix { public float[] d = new float[1]; public void Ensure(int n) { if (d.Length < n) System.Array.Resize(ref d, n); } }

        static void Tone(Mix m, float f1, float f2, float dur, Wave w, float vol, float delay = 0)
        {
            int start = (int)(delay * SR), n = (int)(dur * SR); m.Ensure(start + n + 1);
            double ph = 0; float amp = vol * 7f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = f2 > 0 ? f1 * Mathf.Pow(Mathf.Max(30, f2) / f1, t) : f1;
                ph += f / SR; float p = (float)(ph - System.Math.Floor(ph));
                float v = w == Wave.Square ? (p < .5f ? 1 : -1) : w == Wave.Triangle ? 1 - 4 * Mathf.Abs(p - .5f) : w == Wave.Saw ? 2 * p - 1 : Mathf.Sin(p * 6.2832f);
                float env = Mathf.Pow(.0001f, t); // üstel sönüm
                m.d[start + i] += v * amp * env;
            }
        }

        static void Noise(Mix m, float dur, float vol, float freq, float delay = 0)
        {
            int start = (int)(delay * SR), n = (int)(dur * SR); m.Ensure(start + n + 1);
            float a = 1 - Mathf.Exp(-2 * Mathf.PI * freq / SR), y = 0, amp = vol * 7f;
            var r = new System.Random(n);
            for (int i = 0; i < n; i++)
            {
                y += a * ((float)r.NextDouble() * 2 - 1 - y);
                m.d[start + i] += y * amp * Mathf.Pow(.0001f, i / (float)n) * 2.2f;
            }
        }

        void Add(string name, System.Action<Mix> fn)
        {
            var m = new Mix(); fn(m);
            for (int i = 0; i < m.d.Length; i++) m.d[i] = Mathf.Clamp(m.d[i], -1, 1);
            var c = AudioClip.Create(name, m.d.Length, 1, SR, false); c.SetData(m.d, 0); clips[name] = c;
        }

        void Build()
        {
            Add("shot", m => Tone(m, 880, 300, .06f, Wave.Square, .014f));
            Add("shot_pierce", m => { Tone(m, 1500, 420, .08f, Wave.Saw, .02f); Noise(m, .07f, .03f, 3200); });
            Add("shot_flame", m => { Noise(m, .14f, .05f, 2400); Tone(m, 260, 140, .12f, Wave.Saw, .012f); });
            Add("shot_rocket", m => { Tone(m, 210, 80, .3f, Wave.Saw, .05f); Noise(m, .28f, .07f, 1400); Tone(m, 95, 50, .22f, Wave.Square, .04f, .04f); });
            Add("hit", m => Tone(m, 220, 110, .05f, Wave.Triangle, .035f));
            Add("kill", m => { Noise(m, .18f, .07f, 900); Tone(m, 160, 60, .16f, Wave.Saw, .04f); });
            Add("pickup", m => { Tone(m, 520, 780, .09f, Wave.Square, .045f); Tone(m, 780, 1040, .1f, Wave.Square, .045f, .07f); });
            Add("power", m => { Tone(m, 400, 800, .12f, Wave.Saw, .05f); Tone(m, 600, 1200, .16f, Wave.Saw, .05f, .1f); Tone(m, 900, 1500, .2f, Wave.Square, .04f, .2f); });
            Add("hurt", m => { Noise(m, .2f, .09f, 600); Tone(m, 140, 60, .22f, Wave.Saw, .06f); });
            Add("board", m => Tone(m, 300, 200, .05f, Wave.Square, .02f));
            Add("boom", m => { Noise(m, .3f, .1f, 700); Tone(m, 110, 40, .3f, Wave.Saw, .07f); });
            Add("skill", m => { Tone(m, 300, 1200, .35f, Wave.Saw, .06f); Noise(m, .35f, .05f, 2500); });
            Add("boss", m => { Tone(m, 90, 55, .7f, Wave.Saw, .09f); Tone(m, 135, 80, .7f, Wave.Square, .05f, .05f); });
            Add("spit", m => { Tone(m, 420, 220, .12f, Wave.Sine, .035f); Noise(m, .08f, .03f, 1800); });
            Add("win", m => { float[] f = { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(m, f[i], f[i], .22f, Wave.Square, .05f, i * .12f); });
            Add("lose", m => { float[] f = { 392, 330, 262, 196 }; for (int i = 0; i < 4; i++) Tone(m, f[i], f[i] * .9f, .3f, Wave.Triangle, .07f, i * .16f); });
            Add("click", m => Tone(m, 660, 880, .05f, Wave.Square, .02f));
        }
    }
}
