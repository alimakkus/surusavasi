using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Kışla: asker eğitim penceresi.</summary>
    public partial class BaseMapView
    {
        void TrainPopup(float x, float y, float w, float h, GUIStyle head)
        {
            long now = BaseLogic.Now; Troops.Tick(D, now);
            int cap = Troops.Capacity(D), free = Troops.Free(D), fp = Troops.FoodPer(D), gp = Troops.GoldPer(D);
            GUI.Label(new Rect(x + 16, y + 10, w - 70, 36), "Asker Eğitimi", head);
            GUI.Label(new Rect(x + 16, y + 46, w - 32, 44), "Eğitilen askerler bir sonraki savaşa ek asker olarak katılır.", small);

            GUI.Label(new Rect(x + 16, y + 92, w - 32, 30), $"<b>Hazır asker: <color=#8be39d>{D.troops}</color> / {cap}</b>", new GUIStyle(label) { fontSize = 18 });
            float bw = w - 32, f = cap > 0 ? (D.troops + D.trainQueue) / (float)cap : 0, f2 = cap > 0 ? D.troops / (float)cap : 0;
            GUI.DrawTexture(new Rect(x + 16, y + 126, bw, 14), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.05f, .07f, .1f, .9f), 0, 7);
            GUI.DrawTexture(new Rect(x + 18, y + 128, Mathf.Max(0, (bw - 4) * f), 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Hex("#ffb02e"), 0, 5);
            GUI.DrawTexture(new Rect(x + 18, y + 128, Mathf.Max(0, (bw - 4) * f2), 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Hex("#4fc16a"), 0, 5);

            if (D.trainQueue > 0)
            {
                float rem = Troops.RemainingSeconds(D, now); int sc = Troops.SpeedCost(D, now);
                GUI.Label(new Rect(x + 16, y + 146, w - 32, 26), $"Eğitimde: {D.trainQueue} asker · kalan {Fmt(rem)}", label);
                if (GUI.Button(new Rect(x + 16, y + 176, w - 32, 46), sc == 0 ? "Hemen bitir (bedava)" : $"Hemen bitir: {sc} elmas", btn))
                {
                    if (sc == 0 || Wl.Spend(Currency.Diamonds, sc, "speed_train")) { Troops.FinishNow(D); GameServices.Save.Save(); SoundFx.Play("power"); }
                    else flow.ShowToast("Yeterli elmas yok");
                }
            }
            else GUI.Label(new Rect(x + 16, y + 150, w - 32, 60), $"Asker başına <color=#8be39d>{fp} yiyecek</color> + <color=#ffd23f>{gp} altın</color> · {Troops.UnitSeconds(D):0} sn", small);

            float by = y + h - 120, cw = (w - 48) / 3;
            int[] opts = { 5, 10, free };
            for (int i = 0; i < 3; i++)
            {
                int n = Mathf.Min(opts[i], free);
                string lab = i == 2 ? $"Doldur ({free})" : $"+{opts[i]}";
                GUI.enabled = n > 0 && D.food >= n * fp && D.gold >= n * gp;
                if (GUI.Button(new Rect(x + 16 + i * (cw + 8), by, cw, 54), $"{lab}\n<size=12>{n * fp} yiyecek</size>", i == 2 ? UiSkin.ButtonGold : btn))
                {
                    if (Wl.Spend(Currency.Food, n * fp, "train") && Wl.Spend(Currency.Gold, n * gp, "train"))
                    { Troops.Queue(D, n, now); Daily.Add(D, "train"); GameServices.Save.Save(); SoundFx.Play("board"); }
                }
                GUI.enabled = true;
            }
            if (free == 0 && D.trainQueue == 0) GUI.Label(new Rect(x, by + 60, w, 26), "Kışla dolu. Kapasite için Kışla'yı yükselt.", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
