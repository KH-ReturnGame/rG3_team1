using UnityEngine;

// 패링 스파크 VFX(절차 생성) — 세키로식 '팅' 불꽃.
//  저스트 패링=금빛 크게+많이 / 일반 쳐내기=백은빛 작게.
//  히트스톱(timeScale 0) 중에도 보이도록 unscaled 시간으로 재생. 코루틴 호스트는 Juice.
public static class ParryFx
{
    private static Sprite dotSprite;    // 소프트 원(중앙 글로우)
    private static Material addMat;     // 가산 블렌딩(진짜 발광)

    public static void Spark(Vector2 pos, bool just)
    {
        if (Juice.Instance == null) return;
        Juice.Instance.StartCoroutine(Run(pos, just));
    }

    private static System.Collections.IEnumerator Run(Vector2 pos, bool just)
    {
        EnsureAssets();
        Color core = just ? new Color(1f, 0.88f, 0.5f) : new Color(0.85f, 0.92f, 1f);
        float scale = just ? 1.5f : 0.85f;
        float dur = just ? 0.28f : 0.18f;
        int lineCount = just ? 9 : 5;

        var root = new GameObject("ParryFx");
        root.transform.position = pos;

        // 중앙 글로우(팍 터졌다 사라짐)
        var glow = NewSprite(root.transform, dotSprite, core, pos, 0f, 90);

        // 방사 스파크 라인(사방으로 튀며 짧아지고 사라짐)
        var dirs = new Vector2[lineCount];
        var spds = new float[lineCount];
        var lines = new SpriteRenderer[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            float ang = Random.Range(0f, 360f);
            dirs[i] = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            spds[i] = Random.Range(7f, 13f) * scale;
            lines[i] = NewSprite(root.transform, null, core, pos, ang, 89);   // 흰 1x1(스트레치)
            lines[i].transform.localScale = new Vector3(Random.Range(0.45f, 0.85f) * scale, 0.06f * scale, 1f);
        }

        float t = 0f;
        while (t < dur)
        {
            float k = t / dur;
            float fade = 1f - k * k;
            if (glow != null)
            {
                glow.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.7f, Mathf.Sqrt(k)) * scale;
                glow.color = new Color(core.r, core.g, core.b, fade);
            }
            for (int i = 0; i < lineCount; i++)
            {
                if (lines[i] == null) continue;
                lines[i].transform.position += (Vector3)(dirs[i] * spds[i] * Time.unscaledDeltaTime);
                var s = lines[i].transform.localScale;
                lines[i].transform.localScale = new Vector3(Mathf.Max(0.02f, s.x - 2.2f * scale * Time.unscaledDeltaTime), s.y, 1f);
                lines[i].color = new Color(core.r, core.g, core.b, fade);
            }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Object.Destroy(root);
    }

    private static SpriteRenderer NewSprite(Transform parent, Sprite sprite, Color c, Vector2 pos, float rotZ, int order)
    {
        var go = new GameObject("fx");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : WhiteSprite();
        sr.color = c;
        sr.sortingOrder = order;   // 모든 것 위
        if (addMat != null) sr.material = addMat;
        return sr;
    }

    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);   // 1유닛
        return whiteSprite;
    }

    private static void EnsureAssets()
    {
        if (dotSprite == null)
        {
            const int N = 48;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x / (float)(N - 1) - 0.5f) * 2f, dy = (y / (float)(N - 1) - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r); a *= a;
                    float coreK = Mathf.Clamp01(1f - r * 3f);
                    px[y * N + x] = new Color(1f, 0.9f + 0.1f * coreK, 0.8f + 0.2f * coreK, a);
                }
            tex.SetPixels(px); tex.Apply();
            dotSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1유닛
        }
        if (addMat == null)
        {
            var sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh != null) addMat = new Material(sh);
        }
    }
}
