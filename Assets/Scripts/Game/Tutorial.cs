using System.Collections.Generic;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Rehber: (1) savaşta ilk kez karşılaşılan şeyler için ipucu baloncukları (hareket, fıçı, tabela, güç kutusu,
    /// kapı, yetenek, zayıf nokta) — ipucu sürerken oyun yavaşlar; (2) ekranlar ilk açıldığında kısa tanıtım kartı;
    /// (3) üste yeni bir sistem açıldığında duyuru. Görülenler kayıtta tutulur, bir daha gösterilmez.</summary>
    public static class Tutorial
    {
        static SaveData D => GameServices.Save?.Data;
        public static bool Seen(string id) { var d = D; return d == null || (d.tips != null && d.tips.Contains(id)); }
        public static void Mark(string id) { var d = D; if (d == null) return; if (d.tips == null) d.tips = new List<string>(); if (!d.tips.Contains(id)) { d.tips.Add(id); GameServices.Save.Save(); } }

        // ======================= Savaş ipuçları =======================
        static string active; static float activeT, startTx; static BattleObj target;
        public static bool Slow => active != null && (active == "move" || activeT < 2.2f) && active != "skill";

        public static void ResetBattle() { active = null; target = null; }

        /// <summary>Her karede: etkin ipucu yoksa yenisini seçer, varsa bitip bitmediğine bakar.</summary>
        public static void Tick(BattleSim s, int level, float dt)
        {
            if (D == null || !s.Live) { active = null; return; }
            if (active != null)
            {
                activeT += dt;
                if (Done(s)) { Mark("tip_" + active); active = null; target = null; }
                return;
            }
            if (s.t < 1.2f) return;
            if (!Seen("tip_move")) { Start("move", null); startTx = s.tx; return; }
            foreach (var o in s.objs)
            {
                if (o.done) continue;
                float sy = s.Osy(o); if (sy < 70 || sy > 300) continue;
                if (o.kind == ObjKind.Barrel && !Seen("tip_barrel")) { Start("barrel", o); return; }
                if (o.kind == ObjKind.Board && !Seen("tip_board")) { Start("board", o); return; }
                if (o.kind == ObjKind.Power && !Seen("tip_power")) { Start("power", o); return; }
                if (o.kind == ObjKind.Gate && !Seen("tip_gate")) { Start("gate", o); return; }
            }
            if (s.SkillReady && !Seen("tip_skill") && Seen("tip_barrel")) { Start("skill", null); return; }
            if (s.boss != null && s.boss.weakT > 0 && !Seen("tip_weak")) { Start("weak", null); return; }
        }

        static void Start(string id, BattleObj o) { active = id; activeT = 0; target = o; }

        static bool Done(BattleSim s)
        {
            switch (active)
            {
                case "move": return Mathf.Abs(s.tx - startTx) > 50 || activeT > 9;
                case "skill": return s.cd > 0 || activeT > 12;
                case "weak": return s.boss == null || s.boss.weakT <= 0;
                default: return target == null || target.done || s.Osy(target) > Field.SQY + 10 || activeT > 7;
            }
        }

        static string Text(string id)
        {
            switch (id)
            {
                case "move": return "Parmağını <b>sağa sola sürükle</b>: ordun seni takip eder";
                case "barrel": return "<b>Fıçıyı vur!</b> İçindeki askerler ordunun olur";
                case "board": return "<b>Kırmızı tabela</b> asker götürür: kaç ya da vurarak yık";
                case "power": return "<b>Güç kutusunu vur:</b> çift, hızlı ya da patlayıcı ateş kazan";
                case "gate": return "<b>Kapı seçimi!</b> Hangi yarıdan geçersen o etkiyi alırsın";
                case "skill": return "<b>Yeteneğin hazır!</b> Kalabalık gelince düğmeye dokun";
                case "weak": return "<b>Sarı hedefe nişan al:</b> zayıf noktaya ×3 hasar!";
            }
            return "";
        }

        /// <summary>BattleView.OnGUI içinden: alt bant + hedefe zıplayan ok. toGui: oyun koordinatı → arayüz koordinatı.</summary>
        public static void DrawBattle(BattleSim s, float VW, System.Func<float, float, Vector2> toGui)
        {
            if (active == null) return;
            float t = Time.unscaledTime, a = Mathf.Clamp01(activeT * 4);
            // Hedef
            Vector2? p = null;
            if (target != null && !target.done) p = toGui(target.x, s.Osy(target) - (target.kind == ObjKind.Gate ? 26 : 40));
            else if (active == "skill") p = new Vector2(VW / 2, 800 - 110);
            else if (active == "weak" && s.boss != null) p = toGui(s.boss.x + s.boss.weakX, s.boss.y - 11 * s.boss.sc - 40);
            if (p.HasValue)
            {
                float bob = Mathf.Abs(Mathf.Sin(t * 5)) * 12;
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(p.Value.x - 30, p.Value.y - 40 - bob, 60, 50), "<b><size=40><color=#ffd23f>▼</color></size></b>", new GUIStyle(ModeUi.Center));
                GUI.color = Color.white;
            }
            // Hareket: sağa sola kayan el
            if (active == "move")
            {
                float hx = VW / 2 + Mathf.Sin(t * 2.4f) * 90;
                GUI.DrawTexture(new Rect(hx - 22, 470, 44, 44), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, .85f * a), 0, 22);
                GUI.DrawTexture(new Rect(hx - 12, 480, 24, 24), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f, a), 0, 12);
                GUI.Label(new Rect(VW / 2 - 140, 516, 280, 24), "<b>◄   ►</b>", new GUIStyle(ModeUi.Center) { fontSize = 20 });
            }
            // Bant
            // Bant üstte: ordu ve yetenek düğmesi altta görünür kalsın
            var r = new Rect(14, 118, VW - 28, 62);
            GUI.color = new Color(1, 1, 1, a);
            GUI.Box(r, "", UiSkin.CardGold);
            GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 24, r.height - 8), Text(active), new GUIStyle(ModeUi.Center) { fontSize = 16 });
            GUI.color = Color.white;
        }

        // ======================= Ekran tanıtımları =======================
        class Intro { public string title, body; public Intro(string t, string b) { title = t; body = b; } }
        static readonly Dictionary<GameScreen, Intro> Intros = new Dictionary<GameScreen, Intro>
        {
            { GameScreen.Levels, new Intro("Bölüm Haritası", "Yolu takip et, her seviyeden <b>3 yıldız</b> almaya çalış.\n\nBölüm başlıklarındaki <b>15★ ve 30★ sandıklarını</b> aç. Patron düğümüne dokunarak hikâyeyi yeniden izleyebilirsin.") },
            { GameScreen.Heroes, new Intro("Kahramanlar", "Her kahramanın farklı bir <b>yeteneği</b> ve özelliği var.\n\nKahramanı yükselttikçe <b>yetenek puanı</b> kazanırsın: Yetenek Ağacı'nda kendi tarzını seç. Deniz ve Cem <b>parçayla</b> açılır.") },
            { GameScreen.Talents, new Intro("Yetenek Ağacı", "3 dal var; her dalın sonunda güçlü bir <b>zirve yeteneği</b> bulunur. Zirveyi açmak için o dala önce 3 puan ver.\n\nPuanlar sınırlı: tarzını seç. İlk sıfırlama ücretsiz.") },
            { GameScreen.Modes, new Intro("Oyun Modları", "<b>Kule Tırmanışı:</b> tek can, her kattan sonra güçlendirme seç.\n<b>Patron Avı:</b> 75 saniyede en çok hasarı ver.\n<b>Hayatta Kalma:</b> dalgalara dayan, aralarda mağazadan güçlen.\n\nHepsi lig puanı ve kahraman parçası verir.") },
            { GameScreen.League, new Intro("Haftalık Lig", "30 kişilik grupta yarışırsın. Hafta sonunda <b>ilk 5 yükselir</b>, son 5 düşer.\n\nPuanlar kampanyadan ve bütün modlardan gelir. Sıralamana göre sandık kazanırsın.") },
            { GameScreen.Achievements, new Intro("Başarımlar", "Her başarımın Bronz, Gümüş ve Altın kademesi var. Kademe dolunca <b>elmasını al</b>.\n\nAltın kademeyi aldığın başarımın adını <b>unvan</b> yapabilirsin; ligde adının yanında görünür.") },
            { GameScreen.Daily, new Intro("Günlük", "Her gün <b>giriş ödülü</b> al, 4 görevi tamamla, hepsini bitirince bonus kazan.\n\nGörevlerin ikisi oyun modlarından gelir: her gün farklı bir tarz dene.") },
        };

        public static bool HasIntro(GameScreen s) => D != null && Intros.ContainsKey(s) && !Seen("intro_" + s);

        /// <summary>GameFlow.OnGUI içinden, ekran çizildikten sonra.</summary>
        public static void DrawIntro(GameScreen s, float VW)
        {
            if (!HasIntro(s)) return;
            var it = Intros[s];
            Modal(VW, it.title, it.body, "Anladım", () => Mark("intro_" + s), null, null);
        }

        // ======================= Yeni sistem duyuruları (üste) =======================
        class Unlock { public string id, title, body; public GameScreen to; public System.Func<SaveData, bool> cond; }
        static readonly Unlock[] Unlocks =
        {
            new Unlock { id = "unlock_league", title = "Haftalık Lig açıldı!", body = "Artık her savaş ve mod <b>lig puanı</b> veriyor. Haftanın sonunda sıralamana göre sandık kazan, üst lige çık!", to = GameScreen.League, cond = d => d.unlocked >= 6 },
            new Unlock { id = "unlock_tower", title = "Yeni mod: Kule Tırmanışı", body = "Tek canla kat kat tırman, her kattan sonra <b>3 güçlendirmeden birini seç</b>. Günde 1 koşu ücretsiz!", to = GameScreen.Modes, cond = d => Modes.TowerOpen(d) },
            new Unlock { id = "unlock_surv", title = "Yeni mod: Hayatta Kalma", body = "Dalga dalga gelen zombilere dayan; aralarda <b>asker, hasar ve savunma kulesi</b> al.", to = GameScreen.Modes, cond = d => Modes.SurvOpen(d) },
            new Unlock { id = "unlock_hunt", title = "Yeni mod: Patron Avı", body = "Her gün farklı bir patron! 75 saniyede en çok hasarı ver, <b>Bronz'dan Elmas'a</b> kademe ödülleri topla.", to = GameScreen.Modes, cond = d => Modes.HuntOpen(d) },
            new Unlock { id = "unlock_talent", title = "Yetenek Ağacı", body = "Kahramanın yetenek puanı kazandı! Kahramanlar ekranındaki <b>Yetenek Ağacı</b>'ndan tarzını seç.", to = GameScreen.Heroes, cond = d => Talents.Free(d, Progress.HeroIndex(d.hero)) > 0 && d.unlocked >= 3 },
        };

        /// <summary>Üsteyken: açılmış ama duyurulmamış ilk sistemi gösterir. true dönerse bir pencere açık.</summary>
        public static bool DrawUnlocks(GameFlow flow, float VW)
        {
            var d = D; if (d == null) return false;
            foreach (var u in Unlocks)
            {
                if (Seen(u.id) || !u.cond(d)) continue;
                Modal(VW, u.title, u.body, "Göster", () => { Mark(u.id); flow.Show(u.to); }, "Sonra", () => Mark(u.id));
                return true;
            }
            return false;
        }

        // ======================= Ortak pencere =======================
        static void Modal(float VW, string title, string body, string ok, System.Action onOk, string later, System.Action onLater)
        {
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .6f), 0, 0);
            float w = Mathf.Min(380, VW - 30), x = VW / 2 - w / 2, y = 230, h = 330;
            GUI.Box(new Rect(x, y, w, h), "", UiSkin.CardGold);
            var rib = new Rect(x + 20, y - 16, w - 40, 40); GUI.Box(rib, "", UiSkin.Ribbon);
            GUI.Label(rib, $"<b>{title}</b>", new GUIStyle(ModeUi.Center) { fontSize = 17 });
            GUI.Label(new Rect(x + 22, y + 36, w - 44, h - 120), body, new GUIStyle(UiSkin.Label) { richText = true, wordWrap = true, fontSize = 15 });
            float by = y + h - 72;
            if (later != null)
            {
                float bw = (w - 54) / 2;
                if (GUI.Button(new Rect(x + 20, by, bw, 54), later, UiSkin.ButtonGray)) { SoundFx.Play("click"); onLater?.Invoke(); }
                if (GUI.Button(new Rect(x + 34 + bw, by, bw, 54), ok, UiSkin.ButtonGold)) { SoundFx.Play("click"); onOk?.Invoke(); }
            }
            else if (GUI.Button(new Rect(x + 20, by, w - 40, 54), ok, UiSkin.ButtonGold)) { SoundFx.Play("click"); onOk?.Invoke(); }
            // Arkadaki tıklamaları yut
            GUI.Button(new Rect(0, 0, VW, 800), "", GUIStyle.none);
        }
    }
}
