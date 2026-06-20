using System.Collections.Generic;
using UnityEngine;

// 전투 방(아레나): 플레이어가 구역에 들어오면 방 안의 적을 다 처치할 때까지 양쪽 문이 닫힌다.
//  배치: 빈 오브젝트 + 트리거 Collider2D(방 영역)에 이 컴포넌트.
//   - doors[] : 전투 중 닫히는 양쪽 문(장벽 = Collider2D + 스프라이트). 평소엔 자동으로 꺼둠.
//   - enemies[] : 비우면 구역 안의 Enemy를 자동 감지. 직접 지정도 가능.
[RequireComponent(typeof(Collider2D))]
public class BattleArena : MonoBehaviour
{
    [Header("문(장벽) — 전투 중에만 닫힘")]
    public GameObject[] doors;

    [Header("적 (비우면 구역 안에서 자동 감지)")]
    public List<Enemy> enemies = new List<Enemy>();

    [Header("옵션")]
    public bool oneTime = true;   // 한 번 클리어하면 다시 안 닫힘

    private bool active, cleared;
    private Collider2D zone;

    void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;
        SetDoors(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (active || cleared) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (enemies == null || enemies.Count == 0)   // 자동 감지: 구역 안의 적
        {
            enemies = new List<Enemy>();
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                if (e != null && zone.OverlapPoint(e.transform.position)) enemies.Add(e);
        }
        enemies.RemoveAll(e => e == null);
        if (enemies.Count == 0) { cleared = true; return; }   // 적이 없으면 잠그지 않음

        active = true;
        SetDoors(true);
        Toast.Show("적을 모두 처치하라!", 2f);
    }

    void Update()
    {
        if (!active) return;
        enemies.RemoveAll(e => e == null);   // 죽은 적은 Destroy되어 null
        if (enemies.Count == 0)
        {
            active = false;
            cleared = oneTime;
            SetDoors(false);
            Toast.Show("구역 클리어!", 2f);
        }
    }

    private void SetDoors(bool closed)
    {
        if (doors == null) return;
        foreach (var d in doors) if (d != null) d.SetActive(closed);
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
