using System.Collections;
using UnityEngine;

// 튜토리얼 인트로 컷씬(새 게임 SaveSystem.IntroPending일 때 1회). 캐논:
//  지상으로 통하는 통로에서 철사(몬헌 밧줄벌레식)로 위로 오르다 → 실수로 추락(카메라 고정, 화면 아래로 사라짐)
//  → 암전 → 튜토리얼 바닥에서 시작 → 독백(많이 다쳤고 기억이 흐릿함).
// TutorialScene에 빈 오브젝트로 배치.
public class IntroCutscene : MonoBehaviour
{
    [Header("연출 위치/타이밍")]
    public float climbHeight = 11f;    // 스폰(바닥) 위 '통로' 높이 — 여기서 시작
    public float riseAmount = 2.6f;    // 철사로 끌려 올라가는 거리
    public float riseTime = 1.4f;      // 오르는 시간
    public float wireLen = 4.5f;       // 철사 앵커(플레이어 위)까지 길이
    public float camUp = 1.2f;         // 고정 카메라를 통로보다 위로
    public float fallWatch = 3.5f;     // 낙하 → 화면 밖 사라질 때까지 최대 대기

    [Header("페이드/레터박스")]
    public float fadeTime = 0.7f;
    public float blackHold = 0.9f;
    public float letterboxTime = 0.6f;

    [Header("애니 클립명")]
    public string airState = "JumpFall";
    public string sprawlState = "GroundSlam";
    public string wakeState = "Crouch";

    private float fadeAlpha;
    private Texture2D black;
    private string caption = "";
    private float capAlpha = 0f;
    private GUIStyle capStyle;

    void Start()
    {
        if (!SaveSystem.IntroPending) return;
        SaveSystem.IntroPending = false;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (pc == null) yield break;
        var cam = Camera.main;
        var follow = CameraFollow.Instance;

        pc.cutsceneActive = true;
        Vector3 landPos = pc.transform.position;                 // 튜토리얼 시작 바닥 = 착지 지점
        Vector3 passagePos = landPos + Vector3.up * climbHeight;  // 지상 통로(위)
        pc.transform.position = passagePos;
        pc.ZeroVelocity();
        pc.PlayAnim(airState);
        if (Letterbox.Instance != null) Letterbox.Instance.Show(letterboxTime);

        // 카메라 고정(통로 프레이밍) — 경계 클램프 피하려 팔로우 자체를 끔
        if (follow != null) follow.enabled = false;
        if (cam != null) cam.transform.position = new Vector3(passagePos.x, passagePos.y + camUp, cam.transform.position.z);

        // 철사(와이어)
        var wireGO = new GameObject("IntroWire");
        var lr = wireGO.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.textureMode = LineTextureMode.Stretch;
        lr.widthMultiplier = 0.07f;
        lr.startColor = lr.endColor = new Color(0.82f, 0.87f, 0.96f, 0.95f);
        lr.sortingOrder = 6;
        lr.positionCount = 2;
        Vector3 anchor = passagePos + Vector3.up * wireLen;

        yield return new WaitForSecondsRealtime(0.4f);

        // 철사로 끌려 올라가기
        Vector3 startP = pc.transform.position;
        Vector3 topP = startP + Vector3.up * riseAmount;
        float t = 0f;
        while (t < riseTime)
        {
            Vector3 p = Vector3.Lerp(startP, topP, Mathf.SmoothStep(0f, 1f, t / riseTime));
            pc.transform.position = p; pc.ZeroVelocity(); pc.PlayAnim(airState);
            lr.SetPosition(0, anchor); lr.SetPosition(1, p + Vector3.up * 0.3f);
            t += Time.deltaTime;
            yield return null;
        }

        // 실수 → 철사 끊김 → 낙하(중력)
        Destroy(wireGO);
        pc.ZeroVelocity();
        pc.PlayAnim(airState);

        // 화면(고정 카메라) 아래로 사라질 때까지
        float camBottom = (cam != null && cam.orthographic) ? cam.transform.position.y - cam.orthographicSize : passagePos.y - 6f;
        float t2 = 0f;
        while (t2 < fallWatch)
        {
            pc.PlayAnim(airState);
            if (pc.transform.position.y < camBottom - 1f) break;
            t2 += Time.deltaTime;
            yield return null;
        }

        // 암전
        yield return Fade(0f, 1f, fadeTime);
        yield return new WaitForSeconds(blackHold);

        // 튜토리얼 바닥에서 시작 — 플레이어 착지 지점으로, 카메라 팔로우 복귀
        pc.transform.position = landPos;
        pc.ZeroVelocity();
        pc.PlayAnim(sprawlState);
        if (follow != null) follow.enabled = true;
        if (cam != null) cam.transform.position = new Vector3(landPos.x, landPos.y + 1f, cam.transform.position.z);
        yield return null;

        // 밝아짐
        yield return Fade(1f, 0f, fadeTime);
        yield return new WaitForSeconds(0.4f);

        // 독백 (많이 다침 + 흐릿한 기억)
        yield return Caption("……크윽.", 1.3f);
        yield return Caption("온몸이... 부서질 것처럼 아프다.", 2.2f);
        yield return Caption("기억이 흐릿해. ...내가, 왜 여기 있는 거지.", 2.8f);

        // 깨어나는 모션
        pc.PlayAnim(wakeState);
        yield return new WaitForSeconds(0.8f);

        if (Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
        yield return new WaitForSeconds(0.3f);
        pc.cutsceneActive = false;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur) { fadeAlpha = Mathf.Lerp(from, to, t / dur); t += Time.deltaTime; yield return null; }
        fadeAlpha = to;
    }

    private IEnumerator Caption(string text, float hold)
    {
        caption = text;
        float t = 0f;
        while (t < 0.35f) { capAlpha = Mathf.Clamp01(t / 0.35f); t += Time.deltaTime; yield return null; }
        capAlpha = 1f;
        yield return new WaitForSeconds(hold);
        t = 0f;
        while (t < 0.35f) { capAlpha = 1f - Mathf.Clamp01(t / 0.35f); t += Time.deltaTime; yield return null; }
        capAlpha = 0f; caption = "";
    }

    void OnGUI()
    {
        // 암전
        if (fadeAlpha > 0.001f)
        {
            if (black == null) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply(); }
            GUI.depth = -1900;
            var pc0 = GUI.color; GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
            GUI.color = pc0;
        }
        // 독백 자막
        if (capAlpha > 0.01f && !string.IsNullOrEmpty(caption))
        {
            if (capStyle == null) capStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.depth = -2000;
            float w = Screen.width * 0.8f, h = 44f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.8f, w, h);
            capStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f * capAlpha);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), caption, capStyle);
            capStyle.normal.textColor = new Color(0.93f, 0.95f, 1f, capAlpha);
            GUI.Label(r, caption, capStyle);
        }
    }
}
