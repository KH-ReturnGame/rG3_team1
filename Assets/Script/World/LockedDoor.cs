using UnityEngine;

// 잠긴 문: 지정한 '열쇠'(희귀도별 3종 중 하나)를 가지고 있어야 F로 열 수 있다.
//  - 열쇠가 있으면(consumeKey면 소모) 지정 동작 수행: 장벽 열기 / 씬 이동 / 다음 스테이지 진행.
//  - 진전을 막는 장벽(Collider2D + 스프라이트)과 함께 배치하면 '열쇠가 있어야 진행되는 스테이지'가 됨.
[RequireComponent(typeof(Collider2D))]
public class LockedDoor : MonoBehaviour, IInteractable
{
    public enum DoorAction { OpenBarrier, GoToScene, AdvanceRunStage }

    [Header("열쇠 요구")]
    public ItemData requiredKey;        // 필요한 열쇠(희귀도별 key_common/key_rare/key_epic) — 인스펙터에서 드래그
    public int requiredCount = 1;
    public bool consumeKey = true;      // 열면 열쇠 소모

    [Header("열렸을 때")]
    public DoorAction action = DoorAction.OpenBarrier;
    public GameObject barrier;          // OpenBarrier: 비활성화할 장벽(비우면 이 오브젝트 자신)
    public string targetScene = "";     // GoToScene 모드에서 이동할 씬

    public string Prompt
    {
        get { return "F: 잠긴 문 (" + (requiredKey != null ? requiredKey.itemName : "열쇠") + " 필요)"; }
    }

    public void Interact()
    {
        // 열쇠가 필요하면 자동 소모 대신 '직접 꽂기' UI를 연다(소지품에서 열쇠를 집어 구멍에 클릭).
        if (requiredKey != null) { LockedDoorUI.Open(this); return; }
        Open();
    }

    // LockedDoorUI가 맞는 열쇠 삽입을 확인한 뒤 호출 — 개방 + 토스트
    public void Unlock()
    {
        AudioManager.Sfx("unlock");
        Toast.Show(requiredKey != null ? requiredKey.itemName + "(으)로 문을 열었다!" : "문이 열렸다!", 2f);
        Open();
    }

    private void Open()
    {
        switch (action)
        {
            case DoorAction.GoToScene:
                if (GameFlow.Instance != null && !string.IsNullOrEmpty(targetScene)) GameFlow.Instance.GoToScene(targetScene);
                break;
            case DoorAction.AdvanceRunStage:
                if (GameFlow.Instance != null) GameFlow.Instance.AdvanceStage();
                break;
            default:
                var b = barrier != null ? barrier : gameObject;
                StartCoroutine(SlideOpen(b));   // 배틀 아레나처럼 위로 스르륵 열림
                break;
        }
    }

    // 장벽 개방 연출: 위 벽 속으로 스무스 상승 + 시작 시 쿵(셰이크) → 비활성
    private System.Collections.IEnumerator SlideOpen(GameObject b)
    {
        var rend = b.GetComponentInChildren<Renderer>();
        float height = rend != null ? rend.bounds.size.y : 3f;
        Vector3 from = b.transform.position;
        Vector3 to = from + Vector3.up * height;
        Juice.Shake(0.18f, 0.2f);
        float dur = 0.9f, t = 0f;
        while (t < dur)
        {
            float k = t / dur;
            b.transform.position = Vector3.Lerp(from, to, k * k * (3f - 2f * k));   // 스무스 상승
            t += Time.deltaTime;
            yield return null;
        }
        b.transform.position = from;   // 재사용 대비 원위치(비활성 전에 — 자기 자신이면 코루틴이 여기서 끝남)
        b.SetActive(false);
    }
}
