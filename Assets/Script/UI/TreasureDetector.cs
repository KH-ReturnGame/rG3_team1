using UnityEngine;

// 보물 탐지기: Z키를 누르면 가장 가까운 '안 연' 보물상자의 방향(8방위 화살표)과 거리를 잠깐 표시.
// 자동 부팅 싱글톤(씬마다 둘 필요 없음). TreasureChest.All 을 참조.
public class TreasureDetector : MonoBehaviour
{
    public static TreasureDetector Instance;

    public KeyCode key = KeyCode.Z;
    public float pingDuration = 3.5f;   // 한 번 누르면 이만큼 표시
    public float nearDistance = 2.5f;   // 이보다 가까우면 "바로 근처" 표시

    private float pingTimer;
    private TreasureChest target;
    private GUIStyle arrowStyle, textStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("TreasureDetector");
        Instance = go.AddComponent<TreasureDetector>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (Inventory.IsUIOpen) return;
        if (Input.GetKeyDown(key))
        {
            target = FindNearestUnopened();
            pingTimer = pingDuration;
        }
        if (pingTimer > 0f) pingTimer -= Time.unscaledDeltaTime;
    }

    private TreasureChest FindNearestUnopened()
    {
        var pc = PlayerController.Instance;
        if (pc == null) return null;
        Vector3 p = pc.transform.position;
        TreasureChest best = null; float bestD = float.MaxValue;
        var list = TreasureChest.All;
        for (int i = 0; i < list.Count; i++)
        {
            TreasureChest c = list[i];
            if (c == null || c.IsOpened) continue;
            float d = ((Vector2)(c.Position - p)).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    private static string ArrowFor(Vector2 dir)
    {
        float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;   // 0=오른쪽, 90=위
        if (a >= -22.5f && a < 22.5f) return "→";
        if (a >= 22.5f && a < 67.5f) return "↗";
        if (a >= 67.5f && a < 112.5f) return "↑";
        if (a >= 112.5f && a < 157.5f) return "↖";
        if (a >= -67.5f && a < -22.5f) return "↘";
        if (a >= -112.5f && a < -67.5f) return "↓";
        if (a >= -157.5f && a < -112.5f) return "↙";
        return "←";
    }

    void OnGUI()
    {
        if (pingTimer <= 0f) return;
        var pc = PlayerController.Instance;
        if (pc == null) return;

        if (arrowStyle == null)
        {
            arrowStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 46, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
        }

        float alpha = Mathf.Clamp01(pingTimer / 0.6f);   // 끝에서 페이드아웃
        Color gold = new Color(1f, 0.82f, 0.25f, alpha);

        // 대상이 사라졌으면 갱신
        if (target == null || target.IsOpened) target = FindNearestUnopened();

        float cx = Screen.width * 0.5f;
        float y = Screen.height - 150f;

        if (target == null)
        {
            textStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, alpha);
            GUI.Label(new Rect(cx - 150f, y, 300f, 30f), "주변에 보물이 없다", textStyle);
            return;
        }

        Vector2 dir = (Vector2)(target.Position - pc.transform.position);
        float dist = dir.magnitude;

        arrowStyle.normal.textColor = gold;
        textStyle.normal.textColor = gold;

        if (dist <= nearDistance)
        {
            GUI.Label(new Rect(cx - 80f, y - 20f, 160f, 60f), "★", arrowStyle);
            GUI.Label(new Rect(cx - 150f, y + 40f, 300f, 30f), "보물이 바로 근처에 있다!", textStyle);
        }
        else
        {
            GUI.Label(new Rect(cx - 60f, y - 20f, 120f, 60f), ArrowFor(dir), arrowStyle);
            GUI.Label(new Rect(cx - 100f, y + 40f, 200f, 30f), "보물  " + Mathf.RoundToInt(dist) + "m", textStyle);
        }
    }
}
