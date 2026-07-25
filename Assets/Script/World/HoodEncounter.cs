using System.Collections;
using UnityEngine;

// [후드 조우 이벤트] — 튜토리얼 후반, 트리거 콜라이더로 배치. 플레이어가 들어오면 1회 재생.
//  감정("이런 후드가 왜 이런 곳에?") → 습격하듯 달라붙음 → 융합 → 귀속 장착 → 독백 → 망토 퀘스트 수주.
//  ⚠️ 배치: 빈 오브젝트 + Collider2D(isTrigger). 떠 있는 후드 비주얼은 자식 SpriteRenderer(hoodVisual)로 지정하거나
//     비우면 아이템 아이콘으로 임시 표시. 스프라이트/이펙트는 아트 나오면 교체.
[RequireComponent(typeof(Collider2D))]
public class HoodEncounter : MonoBehaviour
{
    [Header("귀속 후드 아이템(Resources/Items/artifact/bound_hood)")]
    public ItemData hoodItem;              // 비우면 id="bound_hood"로 자동 로드

    [Header("연출 대상")]
    public SpriteRenderer hoodVisual;      // 떠 있는 후드 스프라이트(비우면 아이템 아이콘으로 자동 생성)
    public float bobAmplitude = 0.12f;

    [TextArea] public string[] appraiseLines = {
        "[놀람]……후드? 이런 게 왜 이런 곳에.",
        "등급이 이상하리만치 높은데. 누가 이런 걸 흘리고 갔지."
    };
    [TextArea] public string[] afterLines = {
        "[흔들림]————뭐, 뭐야!",
        "[떨림]……안 떨어져. 이거, 왜 안 떨어지는 거야."
    };

    public string questId = "mq_hood_bond";   // 있으면 수주(없으면 스킵)

    private bool consumed;
    private float bobT;

    void Reset() { var c = GetComponent<Collider2D>(); if (c != null) c.isTrigger = true; }

    void Start()
    {
        if (hoodItem == null) hoodItem = ItemDatabase.Get("bound_hood");
        EnsureVisual();
    }

    void Update()
    {
        if (hoodVisual != null && !consumed)   // 떠 있는 둥실 연출
            hoodVisual.transform.localPosition = new Vector3(0f, Mathf.Sin(bobT += Time.deltaTime * 2f) * bobAmplitude + 0.3f, 0f);
    }

    private void EnsureVisual()
    {
        if (hoodVisual != null) return;
        var go = new GameObject("HoodVisual");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        hoodVisual = go.AddComponent<SpriteRenderer>();
        if (hoodItem != null && hoodItem.icon != null) hoodVisual.sprite = hoodItem.icon;
        hoodVisual.sortingOrder = 5;
        hoodVisual.transform.localScale = Vector3.one * 1.6f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        consumed = true;
        StartCoroutine(Sequence(pc));
    }

    private IEnumerator Sequence(PlayerController pc)
    {
        pc.cutsceneActive = true;
        pc.ZeroVelocity();

        // ① 감정 대사
        bool done = false;
        DialogueUI.Show("???", null, appraiseLines, () => done = true);
        while (!done) yield return null;

        // ② 습격 — 후드가 플레이어에게 달려듦 + 셰이크/붉은 플래시
        AudioManager.Sfx("boss_roar", 0.7f, 0.1f);
        Juice.Shake(0.4f, 0.5f);
        if (hoodVisual != null)
        {
            Vector3 from = hoodVisual.transform.position;
            Vector3 to = pc.transform.position + Vector3.up * 0.6f;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                hoodVisual.transform.position = Vector3.Lerp(from, to, t / 0.35f);
                hoodVisual.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 0.6f, t / 0.35f);
                yield return null;
            }
        }
        Juice.Flash(new Color(0.7f, 0.08f, 0.06f, 0.55f), 0.25f);
        yield return new WaitForSeconds(0.15f);

        // ③ 융합 — 첫 각성 이펙트(시간 감속 + 금빛/붉은 섬광)
        SlowMoFx.BeginTimed(0.08f, 0.9f);
        Juice.Shake(0.6f, 0.5f);
        Juice.Flash(new Color(1f, 0.85f, 0.4f, 0.6f), 0.4f);
        AudioManager.Sfx("skill_ready");
        if (hoodVisual != null) hoodVisual.enabled = false;   // 흡수됨
        yield return new WaitForSeconds(0.6f);

        // ④ 귀속 장착
        if (hoodItem != null && Equipment.Instance != null)
        {
            Equipment.Instance.Equip(hoodItem);
            HandbookUI.MarkItemSeen(hoodItem);
            if (AcquireBanner.Instance != null)
                AcquireBanner.Show(hoodItem.itemName, "몸에 귀속되었다 — 떨어지지 않는다", hoodItem.icon != null ? hoodItem.icon.texture : null, "귀속");
        }

        // ⑤ 독백
        done = false;
        DialogueUI.Show("???", null, afterLines, () => done = true);
        while (!done) yield return null;

        // ⑥ 망토 퀘스트 수주(있으면)
        if (QuestManager.Instance != null && !string.IsNullOrEmpty(questId) && QuestManager.Instance.Find(questId) != null)
            QuestManager.Instance.AcceptById(questId);

        pc.cutsceneActive = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.7f);
        var col = GetComponent<Collider2D>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
