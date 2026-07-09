using System.Collections.Generic;
using UnityEngine;

// 데미지/회복 플로팅 숫자(자동부팅·영구). 월드 위치에서 떠오르며 사라진다.
//  · 적(엔티티)이 피해를 입으면 빨강 — Enemy/DummyMonster.TakeDamage 훅
//  · 플레이어가 회복하면 초록(칸 단위) — GameManager.Heal 훅
//  · 플레이어가 받는 피해는 표시하지 않음(요청) — 넉백·무적 점멸이 이미 피드백
public class DamagePopup : MonoBehaviour
{
    private static DamagePopup inst;

    private struct Pop { public Vector3 wpos; public string text; public Color color; public float t0; public float drift; }
    private readonly List<Pop> pops = new List<Pop>();

    public float life = 0.8f;    // 표시 시간
    public float rise = 1.1f;    // 떠오르는 높이(월드 유닛)

    private GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (inst == null) { var go = new GameObject("DamagePopup"); inst = go.AddComponent<DamagePopup>(); DontDestroyOnLoad(go); }
    }

    // ── 정적 진입점 ──
    public static void Damage(Vector3 worldPos, float amount) => Show(worldPos, Fmt(amount), new Color(1f, 0.30f, 0.25f));
    public static void Heal(Vector3 worldPos, float amount)   => Show(worldPos, "+" + Fmt(amount), new Color(0.38f, 0.95f, 0.45f));
    private static string Fmt(float v) => v.ToString(v % 1f == 0f ? "0" : "0.#");

    public static void Show(Vector3 worldPos, string text, Color c)
    {
        if (inst == null) return;
        inst.pops.Add(new Pop {
            wpos = worldPos + (Vector3)(Random.insideUnitCircle * 0.22f),
            text = text, color = c,
            t0 = Time.unscaledTime,
            drift = Random.Range(-0.35f, 0.35f)
        });
    }

    void OnGUI()
    {
        if (pops.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        float now = Time.unscaledTime;
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            float k = (now - p.t0) / life;
            if (k >= 1f) { pops.RemoveAt(i); continue; }

            float ease = 1f - (1f - k) * (1f - k);                      // 감속하며 떠오름
            Vector3 w = p.wpos + new Vector3(p.drift * ease, 0.5f + rise * ease, 0f);
            Vector3 sp = cam.WorldToScreenPoint(w);
            if (sp.z < 0f) continue;                                     // 카메라 뒤

            float popScale = 1f + 0.45f * Mathf.Exp(-k * 9f);            // 등장 순간 팍 커졌다가 안정
            style.fontSize = Mathf.RoundToInt(Screen.height * 0.023f * popScale);
            float a = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f;         // 끝에서 페이드

            Rect r = new Rect(sp.x - 90f, Screen.height - sp.y - 22f, 180f, 44f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.65f * a);   // 그림자
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), p.text, style);
            style.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, a);
            GUI.Label(r, p.text, style);
        }
    }
}
