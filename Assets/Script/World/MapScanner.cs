using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

// '스캔' 모듈용 일반 지도 생성기. 현재 씬의 타일맵을 훑어 지형 실루엣 + 다음 맵 포탈만 담은 텍스처를 만든다.
//  · 플레이어 위치는 표시하지 않음(정적 스캔)
//  · 씬별로 1회 생성 후 캐시(씬이 바뀌면 자동 재생성)
public static class MapScanner
{
    private static string builtScene;
    private static Texture2D map;
    private const int MaxDim = 220;          // 결과 텍스처 긴 변(px)
    private const long CellCap = 600000;     // 너무 큰 타일맵은 스캔 생략(안전)

    public static Texture2D GetMap()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (map != null && builtScene == scene) return map;
        builtScene = scene;
        map = Build();
        return map;
    }

    private static Texture2D Build()
    {
        var pts = new List<Vector2>();
        var doors = new List<Vector2>();
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm == null) continue;
            BoundsInt b = tm.cellBounds;
            if ((long)b.size.x * b.size.y > CellCap) continue;
            for (int yy = b.yMin; yy < b.yMax; yy++)
                for (int xx = b.xMin; xx < b.xMax; xx++)
                {
                    var cp = new Vector3Int(xx, yy, 0);
                    if (!tm.HasTile(cp)) continue;
                    Vector3 w = tm.GetCellCenterWorld(cp);
                    pts.Add(new Vector2(w.x, w.y));
                    if (w.x < minX) minX = w.x; if (w.x > maxX) maxX = w.x;
                    if (w.y < minY) minY = w.y; if (w.y > maxY) maxY = w.y;
                }
        }
        if (pts.Count == 0) return null;

        // 포탈(다음 맵으로 가는 문)도 지도 범위에 포함
        foreach (var d in Object.FindObjectsByType<SceneDoor>(FindObjectsSortMode.None))
        {
            Vector2 w = d.transform.position;
            doors.Add(w);
            if (w.x < minX) minX = w.x; if (w.x > maxX) maxX = w.x;
            if (w.y < minY) minY = w.y; if (w.y > maxY) maxY = w.y;
        }

        float worldW = Mathf.Max(1f, maxX - minX), worldH = Mathf.Max(1f, maxY - minY);
        float scale = MaxDim / Mathf.Max(worldW, worldH);
        int texW = Mathf.Clamp(Mathf.CeilToInt(worldW * scale) + 6, 8, 512);
        int texH = Mathf.Clamp(Mathf.CeilToInt(worldH * scale) + 6, 8, 512);

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[texW * texH];     // 기본 투명
        Color terrain = new Color(0.34f, 0.44f, 0.54f, 1f);
        Color portalC = new Color(0.30f, 0.85f, 1f, 1f);
        int cell = Mathf.Max(1, Mathf.RoundToInt(scale * 0.55f));

        foreach (var p in pts) Stamp(px, texW, texH, 3 + Mathf.RoundToInt((p.x - minX) * scale), 3 + Mathf.RoundToInt((p.y - minY) * scale), cell, terrain);
        foreach (var w in doors) Stamp(px, texW, texH, 3 + Mathf.RoundToInt((w.x - minX) * scale), 3 + Mathf.RoundToInt((w.y - minY) * scale), 3, portalC);

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void Stamp(Color[] px, int w, int h, int cx, int cy, int r, Color c)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c;
            }
    }
}
