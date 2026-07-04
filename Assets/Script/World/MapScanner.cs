using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;

// '스캔' 모듈용 일반 지도 생성기. 현재 씬의 타일맵을 훑어 지형 실루엣 + 다음 맵 포탈만 담은 텍스처를 만든다.
//  · 플레이어 위치는 표시하지 않음(정적 스캔)
//  · 발견(MapDiscovery)한 카메라 구역만 채워 그림 — 처음엔 비어 있고 탐험하며 드러남(전체 프레임은 고정)
//  · 씬·발견상태가 바뀌면 자동 재생성(캐시 무효화)
public static class MapScanner
{
    private static string builtScene;
    private static int builtVersion = -1;    // 마지막 빌드 시점의 발견 버전(달라지면 재생성)
    private static Texture2D map;
    private const int MaxDim = 320;          // 결과 텍스처 긴 변(px)
    private const long CellCap = 600000;     // 너무 큰 타일맵은 스캔 생략(안전)

    public static Texture2D GetMap()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (map != null && builtScene == scene && builtVersion == MapDiscovery.Version) return map;
        builtScene = scene;
        builtVersion = MapDiscovery.Version;
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

        // 발견한 구역만 그림. null=구역 미설정 씬(전부), 빈 목록=아직 발견 0(지도 없음)
        var areas = MapDiscovery.DiscoveredAreas();
        if (areas != null && areas.Count == 0) return null;

        float worldW = Mathf.Max(1f, maxX - minX), worldH = Mathf.Max(1f, maxY - minY);
        float scale = MaxDim / Mathf.Max(worldW, worldH);
        int texW = Mathf.Clamp(Mathf.CeilToInt(worldW * scale) + 6, 8, 640);
        int texH = Mathf.Clamp(Mathf.CeilToInt(worldH * scale) + 6, 8, 640);

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[texW * texH];     // 기본 투명
        Color terrain = new Color(0.56f, 0.57f, 0.61f, 1f);   // 지형=금속 그레이(테마)
        Color portalC = UITheme.Accent;                        // 다음 포탈=오렌지(테마)
        int cell = Mathf.Max(1, Mathf.RoundToInt(scale * 0.55f));

        foreach (var p in pts) if (MapDiscovery.InAreas(areas, p)) Stamp(px, texW, texH, 3 + Mathf.RoundToInt((p.x - minX) * scale), 3 + Mathf.RoundToInt((p.y - minY) * scale), cell, terrain);
        foreach (var w in doors) if (MapDiscovery.InAreas(areas, w)) Stamp(px, texW, texH, 3 + Mathf.RoundToInt((w.x - minX) * scale), 3 + Mathf.RoundToInt((w.y - minY) * scale), 3, portalC);

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
