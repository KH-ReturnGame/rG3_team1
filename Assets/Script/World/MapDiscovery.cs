using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 맵 발견(fog-of-war). 플레이어가 CameraZone(카메라 구역)에 '진입'하면 그 구역을 발견으로 기록한다.
//  · 미니맵 블립과 일반 지도(MapScanner)는 '발견한 구역'만 보여준다 → 처음엔 안 보이고, 탐험하며 드러남.
//  · 진입 판정은 CameraZone.Contains(위치 포함)로 폴링 — CameraFollow와 동일 방식이라 물리 레이어/태그와 무관.
//  · 씬별로 발견 상태를 메모리에 보관(같은 씬으로 되돌아오면 유지). 세션 동안만(저장은 안 함).
//  · 씬에 CameraZone이 하나도 없으면 전부 발견으로 간주(구역 미설정 맵 호환).
public class MapDiscovery : MonoBehaviour
{
    public static MapDiscovery Instance;

    private static readonly Dictionary<string, HashSet<string>> discovered = new Dictionary<string, HashSet<string>>();
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
        var list = CameraZone.All;
        if (list.Count == 0) return;

        Vector2 p = pc.transform.position;
        string scene = SceneManager.GetActiveScene().name;
        HashSet<string> set;
        if (!discovered.TryGetValue(scene, out set)) { set = new HashSet<string>(); discovered[scene] = set; }

        for (int i = 0; i < list.Count; i++)
        {
            var z = list[i];
            if (z == null || !z.isActiveAndEnabled || !z.Contains(p)) continue;
            if (set.Add(ZoneKey(z))) Version++;   // 새 구역 진입 → 발견
        }
    }

    // 현재 씬에서 발견된 구역들의 Bounds. null이면 '전부 표시'(CameraZone 미설정 씬).
    public static List<Bounds> DiscoveredAreas()
    {
        var list = CameraZone.All;
        if (list.Count == 0) return null;            // 구역 없는 맵: 가리지 않음

        var res = new List<Bounds>();
        string scene = SceneManager.GetActiveScene().name;
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
