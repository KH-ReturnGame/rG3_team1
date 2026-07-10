using System.Collections;
using UnityEngine;

// 무너지는 발판: 플레이어가 위에 올라서면 잠깐 흔들리다 무너져(사라짐) 떨어진 뒤, 일정 시간 후 복구.
//  솔리드 콜라이더 + 스프라이트로 배치.
[RequireComponent(typeof(Collider2D))]
public class CrumblingPlatform : MonoBehaviour
{
    public float crumbleDelay = 0.7f;    // 밟은 뒤 무너지기까지(흔들리는 시간)
    public float respawnDelay = 2.5f;    // 무너진 뒤 복구까지
    public float shakeAmount = 0.06f;

    private Collider2D col;
    private SpriteRenderer[] srs;
    private Vector3 home;
    private bool triggered;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        srs = GetComponentsInChildren<SpriteRenderer>();
        home = transform.position;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (triggered) return;
        var pc = c.collider.GetComponentInParent<PlayerController>();
        if (pc != null && pc.transform.position.y > transform.position.y) StartCoroutine(Crumble());
    }

    private IEnumerator Crumble()
    {
        triggered = true;
        float t = 0f;
        while (t < crumbleDelay)
        {
            transform.position = home + (Vector3)(Random.insideUnitCircle * shakeAmount);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = home;
        SetOn(false);                                  // 무너짐(콜라이더·스프라이트 off)
        yield return new WaitForSeconds(respawnDelay);
        SetOn(true);                                   // 복구
        triggered = false;
    }

    private void SetOn(bool on)
    {
        if (col != null) col.enabled = on;
        foreach (var s in srs) if (s != null) s.enabled = on;
    }
}
