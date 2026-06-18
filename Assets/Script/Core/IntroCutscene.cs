using System.Collections;
using UnityEngine;

// 튜토리얼 인트로 컷씬. 새 게임으로 진입했을 때(SaveSystem.IntroPending)만 1회 재생.
// 레터박스 등장 → 플레이어가 위에서 떨어져 → 착지 → 바닥에 널부러짐(기존 애니) → 일어남 → 레터박스 해제 → 조작 복귀.
// TutorialScene에 빈 오브젝트로 배치(이 컴포넌트만 있으면 됨).
public class IntroCutscene : MonoBehaviour
{
    [Header("연출")]
    public float dropHeight = 16f;       // 스폰 지점 위로 이만큼 올려서 떨어뜨림(높을수록 더 높이서 추락)
    public float sprawlHold = 1.5f;      // 널부러진 채 유지(초)
    public float letterboxTime = 0.6f;

    [Header("중반 연출 (착지 후)")]
    public float bottomCoverFrac = 0.32f;   // 아래 검은 막대를 이 비율까지 키워 플레이어를 가림(0~0.5)
    public float cameraOffsetY = 2.5f;       // 카메라를 이만큼 올려 플레이어가 화면 더 아래에 잡히게(가려지도록)

    [Header("애니 클립명 (기존 에셋)")]
    public string fallState = "JumpFall";
    public string landState = "Land";
    public string sprawlState = "Die";   // 바닥에 널부러짐
    public string getUpState = "Crouch";

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

        // 착지 충격 — 중반 연출: 아래 막대 확대(플레이어 가림) + 카메라 하강(플레이어가 화면 더 아래에)
        pc.ZeroVelocity();
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.AddShake(0.45f, 0.35f);
            CameraFollow.Instance.cutsceneOffset = new Vector2(0f, cameraOffsetY);
        }
        if (Letterbox.Instance != null) Letterbox.Instance.SetBottom(bottomCoverFrac, 0.45f);
        pc.PlayAnim(landState);
        yield return new WaitForSeconds(0.22f);

        // 바닥에 널부러짐
        pc.PlayAnim(sprawlState);
        yield return new WaitForSeconds(sprawlHold);

        // 일어남
        pc.PlayAnim(getUpState);
        yield return new WaitForSeconds(0.45f);

        // 정리: 카메라 원위치 + 레터박스 해제
        if (CameraFollow.Instance != null) CameraFollow.Instance.cutsceneOffset = Vector2.zero;
        if (Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
        yield return new WaitForSeconds(0.3f);

        pc.cutsceneActive = false;   // 입력·자동애니 복귀
    }
}
