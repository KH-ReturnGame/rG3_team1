using UnityEngine;

// 제작대 UI(자동부팅·영구). CraftStation에 F로 열림. 재료가 충분하면 제작.
public class CraftingUI : MonoBehaviour
{
    public static CraftingUI Instance { get; private set; }

    private class Recipe { public string[] inIds; public int[] inCounts; public string outId; public int outCount; }
    private Recipe[] recipes;

    private bool open;
    private GUIStyle title, body, small;
    private Texture2D white;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        recipes = new Recipe[] {
            new Recipe { inIds = new[]{"lizard","underground_flower"}, inCounts = new[]{1,1}, outId="heal_potion",   outCount=1 },
            new Recipe { inIds = new[]{"lizard","slime_condensate"},   inCounts = new[]{1,1}, outId="combat_potion", outCount=1 },
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("CraftingUI").AddComponent<CraftingUI>(); }

    public void Open() { open = true; Inventory.IsUIOpen = true; }
    public void Close() { open = false; Inventory.IsUIOpen = false; }
    void Update() { if (open && Input.GetKeyDown(KeyCode.Escape)) Close(); }

    void OnGUI()
    {
        if (!open) return;
        EnsureStyles();
        float w = 480f, h = 90f + recipes.Length * 60f + 40f;
        float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = new Color(0.13f, 0.10f, 0.08f, 0.99f); GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = new Color(0.86f, 0.63f, 0.30f); GUI.DrawTexture(new Rect(x, y, w, 4f), white); GUI.color = Color.white;
        GUI.Label(new Rect(x, y + 16f, w, 34f), "제작대", title);

        var inv = Inventory.Instance;
        float ry = y + 62f;
        foreach (var rc in recipes)
        {
            var outItem = ItemDatabase.Get(rc.outId);
            string req = "";
            bool can = inv != null;
            for (int i = 0; i < rc.inIds.Length; i++)
            {
                var inItem = ItemDatabase.Get(rc.inIds[i]);
                string nm = inItem != null ? inItem.itemName : rc.inIds[i];
                int have = (inv != null && inItem != null) ? inv.CountOf(inItem) : 0;
                if (have < rc.inCounts[i]) can = false;
                req += (i > 0 ? " + " : "") + nm + "(" + have + "/" + rc.inCounts[i] + ")";
            }
            GUI.Label(new Rect(x + 22f, ry, w - 150f, 24f), outItem != null ? outItem.itemName : rc.outId, body);
            GUI.Label(new Rect(x + 22f, ry + 24f, w - 150f, 22f), req, small);
            GUI.enabled = can;
            if (GUI.Button(new Rect(x + w - 122f, ry + 4f, 100f, 40f), "제작")) Craft(rc);
            GUI.enabled = true;
            ry += 60f;
        }
        if (GUI.Button(new Rect(x + w - 130f, y + h - 44f, 110f, 32f), "닫기")) Close();
    }

    private void Craft(Recipe rc)
    {
        var inv = Inventory.Instance; if (inv == null) return;
        for (int i = 0; i < rc.inIds.Length; i++)
        {
            var it = ItemDatabase.Get(rc.inIds[i]);
            if (it == null || inv.CountOf(it) < rc.inCounts[i]) return;
        }
        for (int i = 0; i < rc.inIds.Length; i++)
            inv.Remove(ItemDatabase.Get(rc.inIds[i]), rc.inCounts[i]);
        var outItem = ItemDatabase.Get(rc.outId);
        if (outItem != null) inv.Add(outItem, rc.outCount);
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(1f, 0.85f, 0.4f);
        body = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
        body.normal.textColor = Color.white;
        small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        small.normal.textColor = new Color(0.85f, 0.82f, 0.72f);
    }
}
