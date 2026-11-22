using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Müzik kanalı: kodla üretilen döngülü parçalar (dosya gerekmez). Ortam sesinin üstünde, ayrı ses düzeyiyle çalar.
    /// base_day: üs gündüz (majör, tıngırtılı), base_night: üs gece (minör, çan gibi, yavaş), battle / boss_fight: savaş ritmi,
    /// victory: zafer ekranı. Her parça döngü uzunluğuna tam oturan tempoyla yazıldığı için dikiş duyulmaz.</summary>
    public partial class SoundFx
    {
        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt("surusavasi-music", 1) == 1;
            set { PlayerPrefs.SetInt("surusavasi-music", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        const int MSR = 22050;
        AudioSource mus;
        string musWant, musNow;
        readonly Dictionary<string, AudioClip> musClips = new Dictionary<string, AudioClip>();

        /// <summary>Çalacak müzik (null: sus). Geçiş yumuşaktır.</summary>
        public static void Music(string key) { if (I != null) I.musWant = key; }

        static float MusicVolume(string key) => key == "battle" || key == "boss_fight" ? .5f : key == "victory" ? .45f : .32f;

        void MusicUpdate()
        {
            if (mus == null) return;
            float target = Muted || !MusicOn || musWant == null ? 0 : MusicVolume(musWant);
            if (musNow != musWant)
            {
                mus.volume = Mathf.MoveTowards(mus.volume, 0, Time.unscaledDeltaTime * 2f);
                if (mus.volume <= 0.001f)
                {
                    musNow = musWant; mus.Stop();
                    if (musNow != null) { mus.clip = MusicClip(musNow); if (mus.clip != null) mus.Play(); }
                }
                return;
            }
            mus.volume = Mathf.MoveTowards(mus.volume, target, Time.unscaledDeltaTime * .6f);
        }

        // ---------------- Sentez ----------------
        static float NoteHz(int midi) => 440f * Mathf.Pow(2, (midi - 69) / 12f);

        AudioClip MusicClip(string key)
        {
            if (musClips.TryGetValue(key, out var cached)) return cached;
            float len;
            switch (key) { case "battle": case "boss_fight": len = 12f; break; case "victory": len = 9.6f; break; default: len = 24f; break; }
            const float tail = 2f;                 // döngü sonunda taşan sesler başa karıştırılır
            int L = (int)(len * MSR), n = L + (int)(tail * MSR);
            var d = new float[n];
            var r = new System.Random(key.Length * 977);

            void Pluck(float at, float f, float dur, float vol, bool bell = false)
            {
                int s0 = (int)(at * MSR), m = (int)(dur * MSR); double ph = 0, ph2 = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; ph += f / MSR; ph2 += f * (bell ? 2.76f : 2f) / MSR;
                    float p = (float)(ph - System.Math.Floor(ph));
                    float v = bell ? Mathf.Sin((float)(ph * 6.2832)) + .35f * Mathf.Sin((float)(ph2 * 6.2832)) * (1 - k)
                                   : (1 - 4 * Mathf.Abs(p - .5f)) * .7f + .3f * Mathf.Sin((float)(ph2 * 6.2832)) * (1 - k);
                    d[s0 + i] += v * vol * Mathf.Exp(-k * (bell ? 4f : 6f)) * Mathf.Min(1, i / (MSR * .004f));
                }
            }
            void Pad(float at, float[] fs, float dur, float vol)
            {
                int s0 = (int)(at * MSR), m = (int)(dur * MSR);
                var ph = new double[fs.Length * 2];
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m, env = Mathf.Min(1, k * 4) * Mathf.Min(1, (1 - k) * 3), v = 0;
                    for (int j = 0; j < fs.Length; j++)
                    {
                        ph[j * 2] += fs[j] * 1.003f / MSR; ph[j * 2 + 1] += fs[j] * .997f / MSR;   // iki hafif ayrık ses: koro etkisi
                        v += Mathf.Sin((float)(ph[j * 2] * 6.2832)) + Mathf.Sin((float)(ph[j * 2 + 1] * 6.2832));
                    }
                    d[s0 + i] += v / (fs.Length * 2) * vol * env;
                }
            }
            void Bass(float at, float f, float dur, float vol)
            {
                int s0 = (int)(at * MSR), m = (int)(dur * MSR); double ph = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; ph += f / MSR; float p = (float)(ph - System.Math.Floor(ph));
                    d[s0 + i] += ((2 * p - 1) * .35f + Mathf.Sin((float)(ph * 6.2832)) * .65f) * vol * Mathf.Min(1, k * 40) * (1 - k);
                }
            }
            void Kick(float at, float vol)
            {
                int s0 = (int)(at * MSR), m = (int)(.22f * MSR); double ph = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; ph += Mathf.Lerp(130, 42, Mathf.Sqrt(k)) / MSR;
                    d[s0 + i] += Mathf.Sin((float)(ph * 6.2832)) * vol * Mathf.Pow(1 - k, 2) * Mathf.Min(1, k * 60);
                }
            }
            void Hiss(float at, float dur, float vol, float freq)
            {
                int s0 = (int)(at * MSR), m = (int)(dur * MSR); float a = 1 - Mathf.Exp(-6.2832f * freq / MSR), y = 0, prev = 0;
                for (int i = 0; i < m && s0 + i < n; i++)
                {
                    float k = i / (float)m; y += a * ((float)r.NextDouble() * 2 - 1 - y);
                    float hp = y - prev; prev = y;
                    d[s0 + i] += hp * vol * 8f * (1 - k);
                }
            }

            // Akorlar: MIDI notaları (kök, üçlü, beşli)
            switch (key)
            {
                case "base_night":
                {
                    // Am – F – C – G, 64 vuruş/dk: vuruş 0,9375 sn → 24 sn = 25,6 vuruş; bunun yerine akor başına 6 sn
                    int[][] ch = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };
                    for (int c = 0; c < 4; c++)
                    {
                        float t0 = c * 6f; var a = ch[c];
                        Pad(t0, new[] { NoteHz(a[0]), NoteHz(a[1]), NoteHz(a[2]) }, 6.6f, .05f);
                        Bass(t0, NoteHz(a[0] - 12), 5.8f, .05f);
                        // Çan gibi yavaş arpej: 6 sn'de 8 nota
                        int[] pat = { 0, 2, 1, 2, 0, 1, 2, 1 };
                        for (int k = 0; k < 8; k++)
                            if (k != 3 || c % 2 == 0) Pluck(t0 + k * .75f, NoteHz(a[pat[k]] + 12), 1.6f, .05f, true);
                    }
                    // Ara sıra yüksek bir çan: gece gökyüzü
                    for (float t = 3f; t < len; t += 6f) Pluck(t + .37f, NoteHz(84 - (int)(t / 6) * 2), 2.4f, .025f, true);
                    break;
                }
                case "battle":
                case "boss_fight":
                {
                    bool boss = key == "boss_fight";
                    float beat = boss ? .5f : .6f;            // 120 / 100 vuruş/dk: 12 sn'de 24 / 20 vuruş
                    int[] roots = boss ? new[] { 45, 45, 46, 44 } : new[] { 45, 45, 48, 43 };
                    int bi = 0;
                    for (float t = 0; t < len + .01f; t += beat, bi++)
                    {
                        if (t >= len) break;
                        Kick(t, boss ? .5f : .38f);
                        if (bi % 2 == 1) Hiss(t, .14f, boss ? .1f : .07f, 2200);      // trampet
                        Hiss(t + beat / 2, .03f, .035f, 7000);                         // zil
                        if (boss) Hiss(t + beat * .75f, .02f, .03f, 7000);
                        int root = roots[(bi / (boss ? 6 : 5)) % 4];
                        if (bi % 2 == 0) Bass(t, NoteHz(root - 12), beat * 1.8f, boss ? .12f : .09f);
                        // Savaş borusu motifi (4 vuruşta bir)
                        if (bi % 4 == 0) Pluck(t, NoteHz(root + 12), beat * 1.5f, boss ? .05f : .04f);
                        if (bi % 4 == 2) Pluck(t, NoteHz(root + (boss ? 13 : 15)), beat * 1.2f, .03f);
                    }
                    if (boss) for (float t = 0; t < len; t += beat * 8) Pad(t, new[] { NoteHz(57), NoteHz(58) }, beat * 8, .03f); // gerilim
                    break;
                }
                case "victory":
                {
                    // C – F – G – C, 100 vuruş/dk, akor başına 4 vuruş (2,4 sn)
                    int[][] ch = { new[] { 60, 64, 67 }, new[] { 65, 69, 72 }, new[] { 67, 71, 74 }, new[] { 72, 76, 79 } };
                    for (int c = 0; c < 4; c++)
                    {
                        float t0 = c * 2.4f; var a = ch[c];
                        Pad(t0, new[] { NoteHz(a[0] - 12), NoteHz(a[1] - 12), NoteHz(a[2] - 12) }, 2.6f, .045f);
                        Bass(t0, NoteHz(a[0] - 24), 2.3f, .07f);
                        for (int k = 0; k < 8; k++) Pluck(t0 + k * .3f, NoteHz(a[k % 3] + (k >= 4 ? 12 : 0)), .5f, .05f);
                        Kick(t0, .2f); Kick(t0 + 1.2f, .15f);
                    }
                    break;
                }
                default: // base_day
                {
                    // C – G – Am – F, 80 vuruş/dk: akor başına 8 vuruş (6 sn)
                    int[][] ch = { new[] { 60, 64, 67 }, new[] { 55, 59, 62 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 } };
                    int[] mel = { 76, 74, 72, 74, 76, 79, 76, 74, 72, 71, 72, 74, 72, 69, 67, 69 };   // basit, akılda kalan ezgi
                    for (int c = 0; c < 4; c++)
                    {
                        float t0 = c * 6f; var a = ch[c];
                        Pad(t0, new[] { NoteHz(a[0]), NoteHz(a[1]), NoteHz(a[2]) }, 6.4f, .035f);
                        Bass(t0, NoteHz(a[0] - 12), 2.8f, .06f); Bass(t0 + 3f, NoteHz(a[2] - 12), 2.8f, .05f);
                        // Tıngırtı arpej (sekizlik)
                        int[] pat = { 0, 1, 2, 1, 0, 1, 2, 1 };
                        for (int k = 0; k < 16; k++) Pluck(t0 + k * .375f, NoteHz(a[pat[k % 8]] + 12), .45f, .028f);
                        // Ezgi: akor başına 4 nota
                        for (int k = 0; k < 4; k++) Pluck(t0 + k * 1.5f, NoteHz(mel[c * 4 + k]), 1.3f, .045f, true);
                    }
                    break;
                }
            }

            // Döngü dikişi: taşan kuyruk başa eklenir, sonra yumuşak sınırlama
            var outD = new float[L];
            for (int i = 0; i < L; i++) outD[i] = d[i] + (i < n - L ? d[L + i] : 0);
            float peak = .001f; for (int i = 0; i < L; i++) peak = Mathf.Max(peak, Mathf.Abs(outD[i]));
            float g = peak > .9f ? .9f / peak : 1;
            for (int i = 0; i < L; i++) outD[i] = (float)System.Math.Tanh(outD[i] * g * 1.2f);
            var clip = AudioClip.Create("mus_" + key, L, 1, MSR, false); clip.SetData(outD, 0);
            musClips[key] = clip;
            return clip;
        }
    }
}
