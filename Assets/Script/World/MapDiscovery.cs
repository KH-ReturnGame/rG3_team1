using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 발견(fog-of-war). 두 가지 모드로 동작:
//  · 구역 모드: 씬에 CameraZone이 있으면, 플레이어가 '진입'한 구역을 발견으로 기록(기존 방식).
//  · ★그리드 모드(구역 없는 씬 — Metroidvania 메인 맵 등): 씬을 gridCell 크기 칸으로 나눠
//    플레이어가 지나간 칸(+상하좌우)을 발견으로 기록 → 씬 수정 없이 fog-of-war가 생긴다.
//  미니맵 블립·일반 지도(MapScanner)는 '발견한 영역'만 보여준다. 세션 동안만 유지(저장은 안 함).
public class MapDiscovery : MonoBehaviour
{
    public static MapDiscovery Instance;

    public float gridCell = 10f;   // 그리드 모드 칸 크기(유닛) — 작을수록 시야가 좁고 정밀

    private static readonly Dictionary<string, HashSet<string>> discovered = new Dictionary<string, HashSet<string>>();
    private static readonly Dictionary<string, HashSet<long>> gridFound = new Dictionary<string, HashSet<long>>();   // 그리드 모드 발견 칸
    public static int Version { get; private set; }   // 발견이 늘 때마다 +1 (지도 캐시 무효화용)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("MapDiscovery");
        Instance = go.AddComponent<MapDiscovery>();
        DontDestroyOnLoad(go);
    }

    // 구역의 안정적인 키(같은 씬에서 위치가 고정이므로 중심 좌표로 식별)
    private static string ZoneKey(CameraZone z)
    {
        Vector3 c = z.Area.center;
        return Mathf.RoundToInt(c.x * 2f) + "," + Mathf.RoundToInt(c.y * 2f);
    }

    void Update()
    {
        var pc = PlayerController.Instance;
        if (pc == null) return;
        Vector2 p = pc.transform.position;
        string scene = SceneManager.GetActiveScene().name;
        var list = CameraZone.All;

        if (list.Count == 0)   // ★그리드 모드: 지나간 칸 + 상하좌우 이웃을 발견
        {
            HashSet<long> cells;
            if (!gridFound.TryGetValue(scene, out cells)) { cells = new HashSet<long>(); gridFound[scene] = cells; }
            int cx = Mathf.FloorToInt(p.x / gridCell), cy = Mathf.FloorToInt(p.y / gridCell);
            if (cells.Add(CellKey(cx, cy))) Version++;
            if (cells.Add(CellKey(cx + 1, cy))) Version++;
            if (cells.Add(CellKey(cx - 1, cy))) Version++;
            if (cells.Add(CellKey(cx, cy + 1))) Version++;
            if (cells.Add(CellKey(cx, cy - 1))) Version++;
            return;
        }

        // 구역 모드(기존)
        HashSet<string> set;
        if (!discovered.TryGetValue(scene, out set)) { set = new HashSet<string>(); discovered[scene] = set; }
        for (int i = 0; i < list.Count; i++)
        {
            var z = list[i];
            if (z == null || !z.isActiveAndEnabled || !z.Contains(p)) continue;
            if (set.Add(ZoneKey(z))) Version++;   // 새 구역 진입 → 발견
        }
    }

    private static long CellKey(int x, int y) => ((long)x << 32) ^ (uint)y;

    // 현재 씬에서 발견된 영역들의 Bounds(구역 or 그리드 칸). 빈 목록 = 아직 아무것도 발견 안 함.
    public static List<Bounds> DiscoveredAreas()
    {
        var res = new List<Bounds>();
        string scene = SceneManager.GetActiveScene().name;
        var list = CameraZone.All;

        if (list.Count == 0)   // 그리드 모드: 발견한 칸들의 Bounds
        {
            HashSet<long> cells;
            float cell = Instance != null ? Instance.gridCell : 10f;
            if (gridFound.TryGetValue(scene, out cells))
                foreach (long k in cells)
                {
                    int cx = (int)(k >> 32), cy = (int)(uint)(k & 0xFFFFFFFF);
                    res.Add(new Bounds(new Vector3((cx + 0.5f) * cell, (cy + 0.5f) * cell, 0f), new Vector3(cell, cell, 0f)));
                }
            return res;
        }

        HashSet<string> set;
        if (!discovered.TryGetValue(scene, out set) || set.Count == 0) return res;   // 아직 아무 구역도 발견 안함 → 빈 목록
        for (int i = 0; i < list.Count; i++)
        {
            var z = list[i];
            if (z != null && set.Contains(ZoneKey(z))) res.Add(z.Area);
        }
        return res;
    }

    // 위치 p가 발견된 구역 안인가. areas==null(구역 없음)이면 항상 true.
    public static bool InAreas(List<Bounds> areas, Vector2 p)
    {
        if (areas == null) return true;
        for (int i = 0; i < areas.Count; i++)
        {
            Bounds b = areas[i];
            if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y) return true;
        }
        return false;
    }

    public static bool IsDiscovered(Vector2 world) => InAreas(DiscoveredAreas(), world);
}
