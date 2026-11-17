using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Temaya göre hareketli çevre efektleri: kar, kor ve duman, düşen yapraklar, kum esintisi, kelebekler.
    /// Parçacıklar sadece ekranda görünen alanda üretilir; tema değişince sıfırlanır.</summary>
    public partial class BaseMapView
    {
        class Amb { public float x, y, vx, vy, life, max, size, rot, vr, ph; public Color c; public int kind; }
        readonly List<Amb> amb = new List<Amb>();
        readonly List<Vector2> vents = new List<Vector2>(); // volkan temasında duman çıkan yerler
        string ambTheme;
        float ambSpawn;

        // kind: 0 kar, 1 kor, 2 duman, 3 yaprak, 4 kum, 5 kelebek, 6 polen
        void AmbientTick(float dt, string theme)
        {
            if (theme != ambTheme)
            {
                ambTheme = theme; amb.Clear(); ambSpawn = 0;
                vents.Clear();
                var rng = new System.Random(11);
                while (vents.Count < 9)
                {
                    float x = (float)rng.NextDouble() * WW, y = (float)rng.NextDouble() * WH;
                    float e = ((x - CX) / WallRX) * ((x - CX) / WallRX) + ((y - CY) / WallRY) * ((y - CY) / WallRY);
                    if (e > 1.5f && Mathf.Abs(x - CX) > 50) vents.Add(new Vector2(x, y));
                }
                // ilk karede ekran dolu başlasın
                for (int i = 0; i < 60; i++) Spawn(theme, true);
            }

            float area = visW * visH / (540f * 960f); // yakınlaştırınca daha az parçacık
            int want = theme == "winter" ? 140 : theme == "autumn" ? 34 : theme == "desert" ? 26 : theme == "lava" ? 70 : 22;
            want = Mathf.RoundToInt(want * Mathf.Clamp(area, .4f, 1.6f));
            ambSpawn += dt * want / 4f;
            while (ambSpawn >= 1 && amb.Count < want) { ambSpawn -= 1; Spawn(theme, false); }
            if (ambSpawn > 3) ambSpawn = 3;

            float l = camX - visW / 2 - 40, r = camX + visW / 2 + 40, top = camY - visH / 2 - 60, bot = camY + visH / 2 + 60;
            foreach (var a in amb)
            {
                a.life -= dt; a.ph += dt;
                switch (a.kind)
                {
                    case 0: a.x += (a.vx + Mathf.Sin(a.ph * 1.3f + a.rot) * 12) * dt; a.y += a.vy * dt; break;
                    case 1: a.x += (a.vx + Mathf.Sin(a.ph * 4 + a.rot) * 10) * dt; a.y += a.vy * dt; break;
                    case 2: a.x += a.vx * dt; a.y += a.vy * dt; a.size += 14 * dt; break;
                    case 3: a.x += (a.vx + Mathf.Sin(a.ph * 1.6f + a.rot) * 26) * dt; a.y += a.vy * dt; a.rot += a.vr * dt; break;
                    case 4: a.x += a.vx * dt; a.y += Mathf.Sin(a.ph * 2 + a.rot) * 6 * dt; break;
                    case 5:
                        a.vx += (Random.value - .5f) * 120 * dt; a.vy += (Random.value - .5f) * 120 * dt;
                        a.vx = Mathf.Clamp(a.vx, -30, 30); a.vy = Mathf.Clamp(a.vy, -24, 24);
                        a.x += a.vx * dt; a.y += a.vy * dt; break;
                    case 6: a.x += (a.vx + Mathf.Sin(a.ph + a.rot) * 6) * dt; a.y += a.vy * dt; break;
                }
                if (a.x < l - 80 || a.x > r + 80 || a.y < top - 80 || a.y > bot + 80) a.life = 0;
            }
            amb.RemoveAll(a => a.life <= 0);
        }

        void Spawn(string theme, bool anywhere)
        {
            float l = camX - visW / 2, w = visW, top = camY - visH / 2, h = visH;
            float rx = l + Random.value * w, ry = top + Random.value * h;
            var a = new Amb { ph = Random.value * 6, rot = Random.value * 6 };
            switch (theme)
            {
                case "winter":
                    a.kind = 0; a.x = rx - 30; a.y = anywhere ? ry : top - 20;
                    a.size = Random.Range(3.5f, 8f); a.vy = 22 + a.size * 6; a.vx = -10;
                    a.c = new Color(1, 1, 1, Random.Range(.65f, .95f)); a.life = a.max = 40; break;
                case "lava":
                    if (Random.value < .2f && vents.Count > 0)
                    {   // duman
                        var v = vents[Random.Range(0, vents.Count)];
                        a.kind = 2; a.x = v.x + Random.Range(-8f, 8f); a.y = v.y; a.size = Random.Range(16f, 24f);
                        a.vx = Random.Range(4f, 12f); a.vy = Random.Range(-22f, -14f);
                        a.c = new Color(.16f, .13f, .13f, .5f); a.life = a.max = Random.Range(4f, 6f);
                    }
                    else
                    {   // kor
                        a.kind = 1; a.x = rx; a.y = anywhere ? ry : top + h * (.4f + Random.value * .7f);
                        a.size = Random.Range(3f, 6f); a.vx = Random.Range(-6f, 6f); a.vy = Random.Range(-46f, -20f);
                        a.c = Random.value < .5f ? new Color(1, .55f, .12f) : new Color(1, .82f, .3f); a.life = a.max = Random.Range(2f, 4.5f);
                    }
                    break;
                case "autumn":
                    a.kind = 3; a.x = rx - 40; a.y = anywhere ? ry : top - 20;
                    a.size = Random.Range(8f, 12f); a.vy = Random.Range(22f, 38f); a.vx = Random.Range(8f, 20f); a.vr = Random.Range(-160f, 160f);
                    var leaf = new[] { new Color(.86f, .36f, .14f), new Color(.93f, .62f, .16f), new Color(.72f, .2f, .12f), new Color(.95f, .78f, .3f) };
                    a.c = leaf[Random.Range(0, leaf.Length)]; a.life = a.max = 40; break;
                case "desert":
                    a.kind = 4; a.x = anywhere ? rx : l - 60; a.y = ry;
                    a.size = Random.Range(36f, 80f); a.vx = Random.Range(110f, 190f);
                    a.c = new Color(1f, .95f, .8f, Random.Range(.35f, .55f)); a.life = a.max = 12; break;
                default: // yeşil vadi: kelebek ve polen
                    if (Random.value < .45f)
                    {
                        a.kind = 5; a.x = rx; a.y = ry; a.size = Random.Range(8f, 11f);
                        var bf = new[] { new Color(1, 1, 1), new Color(1, .9f, .35f), new Color(.6f, .8f, 1), new Color(1, .6f, .8f) };
                        a.c = bf[Random.Range(0, bf.Length)]; a.life = a.max = Random.Range(10f, 18f);
                    }
                    else
                    {
                        a.kind = 6; a.x = rx; a.y = ry; a.size = Random.Range(2.5f, 4f); a.vx = Random.Range(3f, 9f); a.vy = Random.Range(-8f, -3f);
                        a.c = new Color(1, .97f, .7f, .7f); a.life = a.max = Random.Range(5f, 9f);
                    }
                    break;
            }
            amb.Add(a);
        }

        void AmbientDraw()
        {
            foreach (var a in amb)
            {
                float fade = Mathf.Clamp01(a.life / Mathf.Min(1.2f, a.max)) * Mathf.Clamp01((a.max - a.life) * 3 + .001f);
                var c = a.c; c.a *= fade;
                switch (a.kind)
                {
                    case 1:
                        c.a *= .75f + .25f * Mathf.Sin(a.ph * 18 + a.rot);
                        Draw(circle, new Color(c.r, c.g * .7f, c.b, c.a * .35f), a.x, a.y, a.size * 3, a.size * 3, 789);
                        Draw(circle, c, a.x, a.y, a.size, a.size, 790); break;
                    case 2: c.a *= Mathf.Clamp01(a.life / a.max * 1.4f); Draw(circle, c, a.x, a.y, a.size, a.size * .8f, 788); break;
                    case 3: Draw(square, c, a.x, a.y, a.size, a.size * .5f, 790, a.rot); break;
                    case 4: Draw(square, c, a.x, a.y, a.size, 2.4f, 790); break;
                    case 5:
                        float flap = .25f + .75f * Mathf.Abs(Mathf.Sin(a.ph * 14));
                        Draw(circle, c, a.x - a.size * .45f * flap, a.y, a.size * flap, a.size, 790);
                        Draw(circle, c, a.x + a.size * .45f * flap, a.y, a.size * flap, a.size, 790);
                        Draw(square, new Color(.2f, .15f, .1f, c.a), a.x, a.y, 1.2f, a.size * .8f, 791); break;
                    case 0: // kar tanesi: beyaz zeminde de görünsün diye mavimsi gölge
                        Draw(circle, new Color(.35f, .5f, .7f, c.a * .45f), a.x + 1, a.y + 1.5f, a.size * 1.25f, a.size * 1.25f, 789);
                        Draw(circle, c, a.x, a.y, a.size, a.size, 790); break;
                    default: Draw(circle, c, a.x, a.y, a.size, a.size, 790); break;
                }
            }
        }
    }
}
