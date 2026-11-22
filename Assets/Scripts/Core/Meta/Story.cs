using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    public struct Line { public string who, text; public Line(string w, string t) { who = w; text = t; } }

    /// <summary>Konuşmacılar: ayaz, kaya, mira (kahramanlar), nuri (köy muhtarı), boss (o bölümün patronu), lord (Boşluk Lordu).</summary>
    public class Scene { public string id, title; public List<Line> lines = new List<Line>(); }

    /// <summary>Hikâye: 10 bölüm (her biri 10 seviye, sonunda patron). Her bölümün açılışında, patrondan önce ve patron
    /// yenilince bir sahne oynar. Görülen sahneler kayıtta tutulur, tekrar gösterilmez (Bölümler ekranından yeniden izlenebilir).</summary>
    public static class Story
    {
        public static readonly string[] ChapterNames =
        {
            "Son Sığınak", "Çamurlu Yollar", "Ölülerin Toprağı", "Kum ve Taş", "Dağların Sırrı",
            "Beyaz Sessizlik", "Ateşin Kalbi", "Çürüyen Şehir", "Gölgelerin Vadisi", "Boşluğun Kapısı",
        };

        static readonly Dictionary<string, Scene> scenes = Build();

        public static string SpeakerName(string who, int level)
        {
            switch (who)
            {
                case "ayaz": return "Ayaz";
                case "kaya": return "Kaya";
                case "mira": return "Mira";
                case "nuri": return "Muhtar Nuri";
                case "lord": return "Boşluk Lordu";
                case "boss": return Battle.BattleData.BossNames[System.Math.Max(0, System.Math.Min(9, (level - 1) / 10))];
                default: return who;
            }
        }

        public static int Chapter(int level) => System.Math.Max(0, System.Math.Min(9, (level - 1) / 10));

        /// <summary>Seviyeye başlamadan önce oynanacak sahne (bölüm açılışı veya patron öncesi). Görülmüşse null.</summary>
        public static Scene Before(SaveData d, int level)
        {
            int ch = Chapter(level);
            if (level % 10 == 1) { var s = Get($"intro{ch}"); if (s != null && !Seen(d, s.id)) return s; }
            if (level % 10 == 0) { var s = Get($"boss{ch}"); if (s != null && !Seen(d, s.id)) return s; }
            return null;
        }

        /// <summary>Seviye kazanıldıktan sonra oynanacak sahne (patron yenildi / final). Görülmüşse null.</summary>
        public static Scene After(SaveData d, int level)
        {
            if (level % 10 != 0) return null;
            var s = Get($"win{Chapter(level)}");
            return s != null && !Seen(d, s.id) ? s : null;
        }

        public static Scene Get(string id) => scenes.TryGetValue(id, out var s) ? s : null;
        public static bool Seen(SaveData d, string id) => d.seenScenes != null && d.seenScenes.Contains(id);
        public static void MarkSeen(SaveData d, string id) { if (d.seenScenes == null) d.seenScenes = new List<string>(); if (!d.seenScenes.Contains(id)) d.seenScenes.Add(id); }

        static Dictionary<string, Scene> Build()
        {
            var D = new Dictionary<string, Scene>();
            void S(string id, string title, params string[] lines)
            {
                var sc = new Scene { id = id, title = title };
                foreach (var l in lines) { int i = l.IndexOf('|'); sc.lines.Add(new Line(l.Substring(0, i), l.Substring(i + 1))); }
                D[id] = sc;
            }

            // 1. Bölüm — Son Sığınak (Çayır, Orman) — Şapkalı Zombi
            S("intro0", "1. Bölüm: Son Sığınak",
                "nuri|Uyan evlat! Kuzeyden bir sürü geliyor. Binlercesi... Köyümüz bu ovada kalan son sığınak.",
                "ayaz|Sesleri duyuyorum, muhtar. Boşluğun soğuğu onlarla birlikte geliyor.",
                "nuri|Askerlerimiz az ama yürekleri büyük. Yolda fıçıları topla, her fıçı yeni bir savaşçı demek.",
                "ayaz|O zaman yola çıkalım. Bu köy düşmeyecek.");
            S("boss0", "Ormanın Kenarında",
                "boss|Hıııh... Yeni etler! Şapkam da benim kadar aç!",
                "ayaz|Şu şapkayı da seni de buraya gömeceğiz.");
            S("win0", "İlk Zafer",
                "nuri|Başardın! Köyde herkes senin adını konuşuyor.",
                "ayaz|Bu sadece öncüydü. Sürüyü biri yönetiyor... Bunu hissediyorum.",
                "nuri|Güneydeki yolda zırhlı bir komutanın askerleriyle sıkıştığı söyleniyor. Ona yardım edersen bize katılabilir.");

            // 2. Bölüm — Çamurlu Yollar (Köy Yolu, Bataklık) — Zırhlı Dev
            S("intro1", "2. Bölüm: Çamurlu Yollar",
                "kaya|Sen de kimsin? Ah... Muhtar Nuri'nin büyücüsü! Tam zamanında geldin.",
                "ayaz|Ayaz. Adamlarınla birlikte köye gel, Kaya. Orada surlarımız var.",
                "kaya|Bataklığı geçmeden olmaz. Orada bir dev var; yarısı zırh, yarısı çürük et.",
                "ayaz|Önce o zaman.");
            S("boss1", "Bataklığın Devi",
                "boss|KIRAR... EZERİM... ZIRHIM DELİNMEZ!",
                "kaya|Her zırhın bir dikiş yeri vardır. Askerler, dağılın ve ateş!");
            S("win1", "Kaya Katıldı",
                "kaya|Bu devin zırhında bir işaret var: mor bir göz. Hepsinde aynı damga.",
                "ayaz|Boşluğun işareti. Biri ölüleri ordu gibi kullanıyor.",
                "kaya|O zaman ordumuz onlarınkinden daha büyük olmalı. Kışlayı büyütün, asker eğitelim!");

            // 3. Bölüm — Ölülerin Toprağı (Mezarlık, Çöl Kenarı) — Zehir Kraliçesi
            S("intro2", "3. Bölüm: Ölülerin Toprağı",
                "mira|Kıpırdama. Okum başının yanındaki zombiye nişanlı... Tamam, artık kıpırdayabilirsin.",
                "kaya|Ha! Keskin gözlü Mira. Mezarlığın bekçisi olarak tanınırsın.",
                "mira|Bekçilik bitti. Mezarlar boşaldı. Hepsi doğuya, çöle doğru yürüyor.",
                "ayaz|Bizimle gel. Birlikte daha güçlüyüz.");
            S("boss2", "Zehirli Taht",
                "boss|Tatlı kokulu dirilerim... Zehrimin bir damlası, hepinizi benim yapar.",
                "mira|Uzaktan vururum. Damlan bana ulaşamaz.");
            S("win2", "Mira Katıldı",
                "mira|Kraliçe ölürken bir isim fısıldadı: \"Lord\". Boşluk Lordu.",
                "ayaz|Demek o. Eski kitaplarda adı geçer. Ölüleri boşluktan çağıran bir büyücü.",
                "nuri|Köy büyüdükçe daha çok saldırı gelecek. Surları güçlendirin, Karargâh'ı büyütün!");

            // 4. Bölüm — Kum ve Taş (Çöl, Harabeler) — Buz Golemi
            S("intro3", "4. Bölüm: Kum ve Taş",
                "kaya|Bu sıcakta zombiler bile terler herhalde.",
                "mira|Harabelerde eski bir kale var. Lord'un ilk kalesi olduğu söylenir.",
                "ayaz|Çölün ortasında soğuk bir esinti var... Bu doğal değil.");
            S("boss3", "Kumların Altındaki Buz",
                "boss|...BUZ... SONSUZ... UYKU...",
                "ayaz|Çölde bir buz golemi. Lord onu buraya saklamış. Ateş edin, eriyene kadar!");
            S("win3", "Kalenin Sırrı",
                "mira|Golemin içinde bir harita vardı. Dağ geçidini gösteriyor.",
                "kaya|Lord'un orduları oradan geçiyor. Geçidi tutarsak sürünün yarısını keseriz.");

            // 5. Bölüm — Dağların Sırrı (Kayalık, Dağ Geçidi) — Mezar Bekçisi
            S("intro4", "5. Bölüm: Dağların Sırrı",
                "kaya|Geçit dar. Burada sayı değil, cesaret kazanır.",
                "ayaz|Kayaların arasından fısıltılar geliyor. Bekçi bizi bekliyor.");
            S("boss4", "Geçidin Bekçisi",
                "boss|Bu geçidi yüz yıldır kimse geçmedi. Siz de geçemeyeceksiniz.",
                "kaya|Yüz yıl yeter. Emekliliğin geldi!");
            S("win4", "Geçit Bizim",
                "nuri|Geçidin tutulduğu haberi geldi! Köye kaçan aileler akın akın geliyor.",
                "ayaz|Yarı yoldayız. Ama Lord'un gücü giderek artıyor; kuzeyde kar fırtınaları başladı.");

            // 6. Bölüm — Beyaz Sessizlik (Kar Ovası, Buzul) — Alev Canavarı
            S("intro5", "6. Bölüm: Beyaz Sessizlik",
                "mira|Karda izleri saklamak imkânsız. Zombiler de biz de görünürüz.",
                "kaya|Buz ovasında bir ateş görüyorum... Kar onu söndüremiyor.",
                "ayaz|Lord'un alev canavarı. Kuzeyi eritip yolu açmaya çalışıyor.");
            S("boss5", "Buzdaki Alev",
                "boss|YANSIN! HEPSİ YANSIN! KAR, BUZ, SİZ!",
                "ayaz|Benim büyüm boşluğun soğuğundan gelir. Alevin bile donar.");
            S("win5", "Sessizlik Döndü",
                "kaya|Buzul yeniden dondu. Kuzey yolu kapandı.",
                "mira|Ama güneyde, yanardağın eteğinde bir ordu toplanıyor. Kara zırhlı bir şövalye komuta ediyor.");

            // 7. Bölüm — Ateşin Kalbi (Yanardağ Eteği, Lav Nehri) — Karanlık Şövalye
            S("intro6", "7. Bölüm: Ateşin Kalbi",
                "kaya|O şövalyeyi tanıyorum. Bir zamanlar krallığın en iyi kılıcıydı.",
                "ayaz|Lord onu da almış. Belki içinde hâlâ bir parça insan kalmıştır.",
                "kaya|Kalmışsa, onu ben kurtaracağım.");
            S("boss6", "Lav Köprüsü",
                "boss|Kaya... Eski dostum. Diz çök ve Lord'a katıl.",
                "kaya|Ben dizimi yalnızca düşenleri kaldırmak için çökerim. Hazırlan!");
            S("win6", "Eski Bir Dost",
                "kaya|Son nefesinde gülümsedi. Teşekkür etti... Lanetten kurtuldu.",
                "mira|Zırhının içinde bir anahtar vardı. Üzerinde terk edilmiş şehrin arması var.");

            // 8. Bölüm — Çürüyen Şehir (Terk Şehir, Fabrika) — Çürük Dev
            S("intro7", "8. Bölüm: Çürüyen Şehir",
                "mira|Şehir boş. Ama fabrikadan duman çıkıyor.",
                "ayaz|Zombileri orada üretiyorlar. Bütün bu sürünün kaynağı burası olabilir.",
                "kaya|O zaman fabrikayı yerle bir edelim.");
            S("boss7", "Fabrikanın Kalbi",
                "boss|BEN... ÜRETİLDİM... SİZ... HAMMADDE...",
                "mira|Ne kadar büyükse, o kadar kolay hedef.");
            S("win7", "Kaynak Kurudu",
                "nuri|Fabrika yıkıldı! Saldırılar yarıya indi, köyde ilk kez bayram yapıldı.",
                "ayaz|Lord bunu affetmeyecek. Gölge avcısını üzerimize salacak.");

            // 9. Bölüm — Gölgelerin Vadisi (Zehirli Çorak, Ölüm Vadisi) — Gölge Avcı
            S("intro8", "9. Bölüm: Gölgelerin Vadisi",
                "mira|Biri bizi izliyor. Günlerdir.",
                "kaya|Ben de hissediyorum. Gece nöbetlerini ikiye katlayın.",
                "ayaz|Vadiden geçeceğiz. Boşluğun kapısı vadinin sonunda.");
            S("boss8", "Avcı ve Av",
                "boss|Haftalardır sizi izliyorum. En zayıfınız kim, biliyorum.",
                "mira|Ben de seni izliyordum. Ve ben ıskalamam.");
            S("win8", "Kapının Önünde",
                "ayaz|İşte Boşluğun Kapısı. Lord'un ötede bizi beklediğini hissediyorum.",
                "kaya|Ne olursa olsun, birlikte gireceğiz.",
                "mira|Birlikte çıkacağız da.");

            // 10. Bölüm — Boşluğun Kapısı (Gölge Diyarı, Boşluk) — Boşluk Lordu
            S("intro9", "10. Bölüm: Boşluğun Kapısı",
                "lord|Demek sonunda geldiniz. Köyünüz, surlarınız, askerleriniz... Hepsi boşluğun içinde bir toz tanesi.",
                "ayaz|Bir zamanlar sen de bir köyü korumak için büyü öğrendin. Ne oldu sana?",
                "lord|Gördüm. Her şeyin sonunu gördüm. Ben yalnızca kaçınılmaz olanı hızlandırıyorum.",
                "kaya|Biz de kaçınılmaz olanı erteliyoruz. Hem de çok uzun bir süre!");
            S("boss9", "Son Savaş",
                "lord|Gelin o zaman. Boşluğun gerçek yüzünü görün!",
                "ayaz|Herkes yerine! Bu, son savaş!");
            S("win9", "Son: Şafak",
                "lord|İmkânsız... Boşluk... beni... geri çağırıyor...",
                "ayaz|Kapı kapanıyor. Sürü dağılıyor!",
                "nuri|Köyden haber var: zombiler toz olup savruldu. Çocuklar sokaklarda oynuyor!",
                "kaya|Şimdi ne yapacağız, büyücü?",
                "ayaz|Şimdi mi? Yeniden inşa edeceğiz. Ve bir dahaki sürüye hazır olacağız.");
            return D;
        }
    }
}
