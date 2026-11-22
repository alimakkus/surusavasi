using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Ses efektleri kodla üretilir (dosya gerekmez). Prototipteki ses tarifleriyle aynı.</summary>
    public partial class SoundFx : MonoBehaviour
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
        { { "shot", .11f }, { "hit", .07f }, { "kill", .05f }, { "board", .12f }, { "hurt", .08f }, { "spit", .15f }, { "shot_pierce", .11f }, { "shot_flame", .07f }, { "shot_rocket", .2f }, { "coin", .06f }, { "tick", .5f }, { "gate_good", .2f }, { "gate_bad", .2f } };
        static readonly HashSet<string> Vib = new HashSet<string> { "hurt", "boom", "skill", "boss", "lose", "win", "roar", "gate_bad", "chest" };

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
            amb = gameObject.AddComponent<AudioSource>();
            amb.playOnAwake = false; amb.loop = true; amb.volume = 0;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            mus = gameObject.AddComponent<AudioSource>();
            mus.playOnAwake = false; mus.loop = true; mus.volume = 0;
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

        // ---------- Ortam sesi (tema) ----------
        const int ASR = 22050;
        const float AmbVolume = .55f;
        AudioSource amb;
        string ambWant, ambNow;
        readonly Dictionary<string, AudioClip> ambClips = new Dictionary<string, AudioClip>();

        /// <summary>Üs teması için döngülü ortam sesi. null: sus. Geçişler yumuşak (ses azalıp artar).</summary>
        public static void Ambient(string key) { if (I != null) I.ambWant = key; }

        void Update()
        {
            MusicUpdate();
            if (amb == null) return;
            float target = Muted || ambWant == null ? 0 : AmbVolume;
            if (ambNow != ambWant)
            {   // önce eski ses kısılır, sonra yenisi başlar
                amb.volume = Mathf.MoveTowards(amb.volume, 0, Time.unscaledDeltaTime * 1.5f);
                if (amb.volume <= 0.001f)
                {
                    ambNow = ambWant; amb.Stop();
                    if (ambNow != null) { amb.clip = AmbClip(ambNow); if (amb.clip != null) amb.Play(); }
                }
                return;
            }
            amb.volume = Mathf.MoveTowards(amb.volume, target, Time.unscaledDeltaTime * .8f);
        }

        AudioClip AmbClip(string key)
        {
            if (ambClips.TryGetValue(key, out var c)) return c;
            const float len = 12f, fade = 1.5f;
            int n = (int)((len + fade) * ASR);
            var d = new float[n];
            var r = new System.Random(key.GetHashCode());
            float Rnd() => (float)r.NextDouble();
            // Rüzgâr: süzülmüş gürültü, yavaş değişen şiddet ve renk
            void Wind(float vol, float lo, float hi, float gust)
            {
                float y1 = 0, y2 = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)ASR;
                    float g = .55f + .45f * Mathf.Sin(t * .9f + Mathf.Sin(t * .37f) * 2) * gust;
                    float a = 1 - Mathf.Exp(-6.2832f * Mathf.Lerp(lo, hi, g) / ASR);
                    y1 += a * (Rnd() * 2 - 1 - y1); y2 += a * (y1 - y2);
                    d[i] += y2 * vol * g * 6f;
                }
            }
            void Chirp(float at, float f1, float f2, float dur, float vol)
            {
                int s0 = (int)(at * ASR), m = (int)(dur * ASR); double ph = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; ph += Mathf.Lerp(f1, f2, k) / ASR;
                    d[s0 + i] += Mathf.Sin((float)(ph * 6.2832)) * vol * Mathf.Sin(k * Mathf.PI);
                }
            }
            void Burst(float at, float dur, float vol, float freq)
            {
                int s0 = (int)(at * ASR), m = (int)(dur * ASR); float a = 1 - Mathf.Exp(-6.2832f * freq / ASR), y = 0, prev = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; y += a * (Rnd() * 2 - 1 - y);
                    float hp = y - prev; prev = y; // yüksek geçiren: hışırtı
                    d[s0 + i] += hp * vol * 8f * Mathf.Sin(k * Mathf.PI);
                }
            }
            void Blup(float at, float vol)
            {
                int s0 = (int)(at * ASR), m = (int)(.18f * ASR); double ph = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; ph += Mathf.Lerp(150, 55, k) / ASR;
                    d[s0 + i] += Mathf.Sin((float)(ph * 6.2832)) * vol * (1 - k) * Mathf.Min(1, k * 20);
                }
            }
            switch (key)
            {
                case "winter":
                    Wind(.09f, 120, 520, 1);
                    for (float t = 0; t < len; t += 2.5f + Rnd() * 2) Chirp(t, 620 + Rnd() * 160, 760 + Rnd() * 200, 1.2f + Rnd(), .012f); // ıslık
                    break;
                case "lava":
                    Wind(.16f, 30, 90, .6f);              // derin uğultu
                    for (float t = 0; t < len; t += .5f + Rnd() * 1.3f) Blup(t, .07f + Rnd() * .05f);
                    for (float t = 0; t < len; t += .05f + Rnd() * .25f) Burst(t, .006f + Rnd() * .01f, .25f + Rnd() * .2f, 5000); // çıtırtı
                    break;
                case "autumn":
                    Wind(.06f, 150, 600, .8f);
                    for (float t = 0; t < len; t += .6f + Rnd() * 1.4f) Burst(t, .3f + Rnd() * .6f, .05f + Rnd() * .04f, 3500); // yaprak hışırtısı
                    break;
                case "desert":
                    Wind(.12f, 200, 900, 1);
                    for (float t = 0; t < len; t += 1.5f + Rnd() * 2) Burst(t, 1.2f + Rnd(), .04f, 6000); // kum
                    break;
                case "green_night":
                    Wind(.025f, 80, 260, .5f);
                    for (float t = 0; t < len; t += .9f + Rnd() * .6f)          // cırcır böceği
                        for (int j = 0; j < 4; j++) Chirp(t + j * .045f, 4300, 4400, .03f, .02f);
                    break;
                default: // yeşil vadi gündüz: hafif esinti + kuşlar
                    Wind(.035f, 100, 380, .7f);
                    for (float t = .3f; t < len; t += 1.2f + Rnd() * 2.2f)
                    {
                        float f = 2600 + Rnd() * 1800; int cnt = 2 + r.Next(4); bool up = Rnd() < .5f;
                        for (int j = 0; j < cnt; j++) Chirp(t + j * .13f, up ? f : f * 1.3f, up ? f * 1.35f : f, .07f + Rnd() * .05f, .035f);
                    }
                    break;
            }
            // Döngü dikişi: sondaki fazlalık başa karıştırılır
            int L = (int)(len * ASR), F = n - L;
            var outD = new float[L];
            for (int i = 0; i < L; i++) outD[i] = d[i];
            for (int i = 0; i < F; i++) { float k = i / (float)F; outD[i] = d[i] * k + d[L + i] * (1 - k); }
            for (int i = 0; i < L; i++) outD[i] = Mathf.Clamp(outD[i], -1, 1);
            c = AudioClip.Create("amb_" + key, L, 1, ASR, false); c.SetData(outD, 0);
            ambClips[key] = c;
            return c;
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

        /// <summary>Kabaran ton (boru/kükreme): yumuşak giriş, hafif titreşim, yumuşak çıkış.</summary>
        static void Swell(Mix m, float f, float dur, float vol, float delay = 0)
        {
            int start = (int)(delay * SR), n = (int)(dur * SR); m.Ensure(start + n + 1);
            double ph = 0; float amp = vol * 7f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += f * (1 + .012f * Mathf.Sin(t * dur * 6.2832f * 5.5f)) / SR; float p = (float)(ph - System.Math.Floor(ph));
                float env = Mathf.Min(1, t * 6) * Mathf.Min(1, (1 - t) * 4);
                m.d[start + i] += (2 * p - 1) * amp * env * .6f;
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
            // ---- His paketi ----
            Add("gate_good", m => { float[] f = { 660, 880, 1320 }; for (int i = 0; i < 3; i++) Tone(m, f[i], f[i] * 1.06f, .12f, Wave.Square, .04f, i * .05f); Noise(m, .25f, .04f, 5000, .05f); });
            Add("gate_bad", m => { Tone(m, 300, 110, .32f, Wave.Saw, .06f); Tone(m, 240, 90, .32f, Wave.Square, .03f, .03f); });
            Add("wave", m => { Swell(m, 110, .9f, .07f); Swell(m, 165, .9f, .05f); Swell(m, 220, .5f, .03f, .45f); Noise(m, .12f, .06f, 400); });
            Add("roar", m => { Noise(m, 1.0f, .12f, 380); Swell(m, 70, 1.0f, .08f); Swell(m, 52, 1.1f, .07f, .1f); });
            Add("coin", m => { Tone(m, 1320, 1320, .08f, Wave.Square, .03f); Tone(m, 1760, 1760, .18f, Wave.Square, .03f, .07f); });
            Add("chest", m => { float[] f = { 1047, 1319, 1568, 2093, 2637 }; for (int i = 0; i < 5; i++) Tone(m, f[i], f[i], .25f, Wave.Sine, .05f, i * .06f); Noise(m, .4f, .03f, 6000, .05f); });
            Add("levelup", m => { float[] f = { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(m, f[i], f[i], .14f, Wave.Square, .045f, i * .06f); Swell(m, 1047, .35f, .03f, .24f); });
            Add("promote", m => { float[] f = { 392, 523, 659, 784 }; for (int i = 0; i < 4; i++) Tone(m, f[i], f[i], .18f, Wave.Saw, .04f, i * .1f); Swell(m, 523, .8f, .04f, .4f); Swell(m, 659, .8f, .03f, .4f); Swell(m, 784, .8f, .03f, .4f); });
            Add("whoosh", m => { Noise(m, .28f, .06f, 1600); Tone(m, 500, 1400, .2f, Wave.Sine, .015f); });
            Add("tick", m => Tone(m, 1500, 1400, .03f, Wave.Square, .02f));
        }
    }
}
