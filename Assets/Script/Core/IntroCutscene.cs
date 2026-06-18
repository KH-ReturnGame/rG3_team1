using System.Collections;
using UnityEngine;

// 튜토리얼 인트로 컷씬. 새 게임으로 진입했을 때(SaveSystem.IntroPending)만 1회 재생.
// 레터박스 등장 → 플레이어가 위에서 떨어져 → 착지 → 바닥에 널부러짐(기존 애니) → 레터박스 해제 → 조작 복귀.
// TutorialScene에 빈 오브젝트로 배치(이 컴포넌트만 있으면 됨).
public class IntroCutscene : MonoBehaviour
{
    [Header("연출")]
    public float dropHeight = 16f;       // 스폰 지점 위로 이만큼 올려서 떨어뜨림(높을수록 더 높이서 추락)
    public float sprawlHold = 1f;      // 널부러진 채 유지(초)
    public float letterboxTime = 0.6f;
    
    [Header("애니 클립명 (기존 에셋)")]
    public string fallState = "JumpFall";
    public string landState = "Land";
    public string sprawlState = "GroundSlam";   // 바닥에 널부러짐(착지 모션)

    void Start()
    {
        if (!SaveSystem.IntroPending) return;   // 새 게임 진입이 아니면 재생 안 함
        SaveSystem.IntroPending = false;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (pc == null) yield break;

        pc.cutsceneActive = true;
        Vector3 landPos = pc.transform.position;                  // 원래 스폰 = 착지 지점
        pc.transform.position = landPos + Vector3.up * dropHeight; // 위로 올림
        pc.ZeroVelocity();
        pc.PlayAnim(fallState);

        if (Letterbox.Instance != null) Letterbox.Instance.Show(letterboxTime);

        // 착지까지 낙하 대기
        yield return null;
        float t = 0f;
        while (!pc.Grounded && t < 5f) { pc.PlayAnim(fallState); t += Time.deltaTime; yield return null; }

        
        
        // 바닥에 널부러짐(착지 모션). 일어나는 런지(Crouch) 자세는 흐름이 어색해 제거 — 바로 조작 복귀.
        pc.PlayAnim(sprawlState);
        yield return new WaitForSeconds(sprawlHold);

        // 정리: 카메라 원위치 + 레터박스 해제
        if (CameraFollow.Instance != null) CameraFollow.Instance.cutsceneOffset = Vector2.zero;
        if (Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
        yield return new WaitForSeconds(0.3f);

        pc.cutsceneActive = false;   // 입력·자동애니 복귀
    }
}
