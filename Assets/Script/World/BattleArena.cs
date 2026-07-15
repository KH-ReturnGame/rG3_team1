using System.Collections.Generic;
using UnityEngine;

// 전투 방(아레나): 플레이어가 구역에 들어와 '방 안의 적이 플레이어를 인식(전투 개시)'하면
//  적을 다 처치할 때까지 양쪽 문이 닫힌다. (단순 진입이 아니라 실제 교전 시작 시점 기준)
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
    public float doorSlide = 0.55f;   // 문이 스르륵 오르내리는 시간(0=즉시)
    public bool startClosed = false;  // ★처음부터 문이 닫혀 있음(시작 방·진행 게이트) — 클리어해야 열림

    [Header("클리어 보상 — 전부 처치 시 활성화(보물상자·다음 길 등)")]
    public GameObject[] onClearActivate;

    private bool active, cleared, primed;   // primed = 플레이어 진입해 무장됨(적 인식 대기 중)
    private Collider2D zone;
    private Vector3[] doorClosedPos;   // 배치된(닫힌) 위치
    private float[] doorDropH;         // 열릴 때 내려가는 깊이(문 높이)
    public bool IsCleared => cleared;  // 외부(튜토리얼 시퀀스 등)에서 클리어 감시용

    void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;

        // ★배선 실수 방어: doors에 '자기 자신'이 들어가면 Awake에서 자신을 꺼버려 아레나가 영영 죽는다.
        if (doors != null)
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] == gameObject)
                {
                    Debug.LogWarning($"[BattleArena] '{name}' doors에 자기 자신이 들어있어 제외합니다. 존(트리거)과 문(벽)은 서로 다른 오브젝트여야 합니다.");
                    doors[i] = null;
                }

        // 문 원위치·높이 캐시(스르륵 연출용) 후 평소엔 열림(비활성)
        doorClosedPos = new Vector3[doors != null ? doors.Length : 0];
        doorDropH = new float[doorClosedPos.Length];
        for (int i = 0; i < doorClosedPos.Length; i++)
        {
            if (doors[i] == null) continue;
            doorClosedPos[i] = doors[i].transform.position;
            var rend = doors[i].GetComponentInChildren<Renderer>();
            var col = doors[i].GetComponentInChildren<Collider2D>();
            doorDropH[i] = rend != null ? rend.bounds.size.y : (col != null ? col.bounds.size.y : 3f);
            doors[i].SetActive(startClosed);   // 기본=열림(비활성), startClosed면 닫힌 채 시작
        }

        if (onClearActivate != null)
            foreach (var g in onClearActivate) if (g != null) g.SetActive(false);   // 보상은 클리어 전 숨김
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (active || cleared || primed) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        // 이 아레나의 적 목록 확정(수동 지정 우선, 아니면 존 안 자동감지) — 아직 문은 안 닫음
        if (enemies == null || enemies.Count == 0)
        {
            enemies = new List<Enemy>();
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                if (e != null && zone.OverlapPoint(e.transform.position)) enemies.Add(e);
        }
        enemies.RemoveAll(e => e == null);
        if (enemies.Count == 0) { cleared = true; return; }   // 적이 없으면 잠그지 않음

        primed = true;   // 무장 — 적 중 하나가 플레이어를 인식하면 그때 닫힌다(Update에서 감시)
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (active || cleared) return;   // 전투 시작 뒤엔 무시(문에 갇힘)
        if (other.GetComponentInParent<PlayerController>() == null) return;
        primed = false;   // 전투 시작 전에 나가면 무장 해제(다시 들어오면 재무장)
    }

    void Update()
    {
        // 전투 개시 대기: 적 중 하나가 플레이어를 인식하면 문을 닫는다
        if (primed && !active)
        {
            enemies.RemoveAll(e => e == null);
            if (enemies.Count == 0) { primed = false; cleared = true; return; }   // 그새 전멸했으면 스킵
            bool anyAggro = false;
            foreach (var e in enemies) if (e != null && e.IsAggro) { anyAggro = true; break; }
            if (anyAggro)
            {
                active = true; primed = false;
                SetDoors(true);
                Toast.Show("적을 모두 처치하라!", 2f);

                // 첫 아레나 전투: 배틀 아레나 카드(세션 1회) — ★벽이 완전히 떨어져 닫힌(쿵) 직후 표시.
                // (force: 일반 표시는 전투 억제(CombatNearby)에 걸려 전투가 끝난 뒤에야 떴었음)
                if (!arenaHelpShown && !startClosed && HelpPopupUI.Instance != null)
                {
                    arenaHelpShown = true;
                    StartCoroutine(ShowArenaHelpAfterDoors());
                }
            }
            return;
        }

        if (!active) return;
        enemies.RemoveAll(e => e == null);   // 죽은 적은 Destroy되어 null
        if (enemies.Count == 0)
        {
            active = false;
            cleared = oneTime;
            SetDoors(false);
            if (onClearActivate != null)
                foreach (var g in onClearActivate)
                    if (g != null) g.SetActive(true);   // 상자·다음 길 등장
            Toast.Show("구역 클리어!", 2f);
            // (구) 보물상자 도움말 — 폐지. 인벤토리/아이템 사용 안내는 '첫 상자 개봉' 때 TutorialFlow가 담당.
        }
    }

    private static bool arenaHelpShown;   // 배틀 아레나 카드 세션당 1회

    // 문 낙하 연출(doorSlide)이 끝나고 쿵 착지한 직후에 아레나 카드 표시(연출을 카드가 가리지 않게)
    private System.Collections.IEnumerator ShowArenaHelpAfterDoors()
    {
        float wait = Mathf.Max(0.01f, doorSlide) + 0.15f;   // 낙하 시간 + 착지 쿵 직후
        float t = 0f;
        while (t < wait) { t += Time.deltaTime; yield return null; }   // 문 애니와 같은 시계(scaled)
        if (HelpPopupUI.Instance != null)
            HelpPopupUI.Instance.ShowPages(true, new HelpPopupUI.HelpPage("arena", "배틀 아레나",
                "이 구역의 적을 모두 처치할 때까지 문이 닫힙니다 — 물러날 곳은 없습니다.\n전멸시키면 문이 열리고, 때로는 보상이 나타납니다."));
    }

    // 문 개폐 — 닫힘: 땅속에서 스르륵 올라옴 / 열림: 스르륵 내려가고 비활성.
    private void SetDoors(bool closed)
    {
        if (doors == null) return;
        StopAllCoroutines();
        for (int i = 0; i < doors.Length; i++)
            if (doors[i] != null) StartCoroutine(SlideDoor(i, closed));
    }

    // 게이트는 '위 벽(천장) 속'으로 여닫힌다: 열림=위로 스르륵 올라가 사라짐 / 닫힘=위에서 낙하해 쿵 + 흔들림.
    private System.Collections.IEnumerator SlideDoor(int i, bool closed)
    {
        var d = doors[i];
        Vector3 closedPos = doorClosedPos[i];
        Vector3 hidden = closedPos + Vector3.up * doorDropH[i];   // 위 벽 속(숨는 위치)

        // 이미 닫혀 있으면(startClosed 등) 다시 떨어뜨리지 않음
        if (closed && d.activeSelf && (d.transform.position - closedPos).sqrMagnitude < 0.001f) yield break;

        float dur = Mathf.Max(0.01f, doorSlide);
        float t = 0f;

        if (closed)
        {
            // 닫힘: 처음 1/3 거리는 '천천히' 내려오다가 → 그 뒤 급가속해 쾅
            d.SetActive(true);
            d.transform.position = hidden;

            // 1단계: 거리의 1/3을 전체 시간의 60% 동안 일정 속도로(긴장감)
            float slowDur = dur * 0.6f;
            while (t < slowDur)
            {
                float k = (t / slowDur) * (1f / 3f);                       // 0 → 1/3
                d.transform.position = Vector3.Lerp(hidden, closedPos, k);
                t += Time.deltaTime;
                yield return null;
            }
            // 2단계: 남은 2/3 거리를 급가속(ease-in 제곱)으로 쾅
            float fastDur = dur * 0.4f;
            t = 0f;
            while (t < fastDur)
            {
                float k2 = t / fastDur;
                float k = 1f / 3f + (2f / 3f) * (k2 * k2);                  // 1/3 → 1 (가속)
                d.transform.position = Vector3.Lerp(hidden, closedPos, k);
                t += Time.deltaTime;
                yield return null;
            }
            d.transform.position = closedPos;

            // 착지: 카메라 쿵 + 게이트 잔진동
            AudioManager.Sfx("door_slam");
            Juice.Shake(0.22f, 0.18f);
            float st = 0f;
            const float shakeDur = 0.28f;
            while (st < shakeDur)
            {
                float decay = 1f - st / shakeDur;
                float ox = Mathf.Sin(st * 70f) * 0.055f * decay;
                d.transform.position = closedPos + new Vector3(ox, Mathf.Abs(ox) * 0.4f, 0f);
                st += Time.deltaTime;
                yield return null;
            }
            d.transform.position = closedPos;
        }
        else
        {
            // 위로 스르륵 올라가 벽 속으로
            AudioManager.Sfx("door_open");
            Vector3 from = d.transform.position;
            while (t < dur)
            {
                float k = t / dur;
                d.transform.position = Vector3.Lerp(from, hidden, k * k * (3f - 2f * k));   // 스무스 상승
                t += Time.deltaTime;
                yield return null;
            }
            d.SetActive(false);
            d.transform.position = closedPos;   // 다음 닫힘 대비 원위치
        }
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}
