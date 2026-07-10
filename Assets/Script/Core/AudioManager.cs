using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 사운드 매니저(자동부팅·영구). ★에셋이 없어도 안전 — 클립을 못 찾으면 조용히 무시한다.
//
//  사용법(코드): AudioManager.Sfx("parry_just");  /  AudioManager.Bgm("village");
//  사용법(에셋): 오디오 파일을 아래 경로에 '이름 그대로' 넣기만 하면 끝(코드 수정 불필요).
//    Assets/Resources/Audio/SFX/<이름>.wav|ogg     예) parry_just.wav
//    Assets/Resources/Audio/BGM/<이름>.ogg          예) village.ogg
//
//  씬 BGM은 SceneBgm() 표에 따라 씬 로드 시 자동 크로스페이드. 표만 고치면 됨.
//  볼륨: MasterVolume/BgmVolume/SfxVolume (PlayerPrefs 저장) — 설정 UI 붙일 때 이 값만 조절.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private string currentBgm;
    private Coroutine bgmFade;

    private static readonly Dictionary<string, AudioClip> sfxCache = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, AudioClip> bgmCache = new Dictionary<string, AudioClip>();

    // ── 볼륨(0~1, PlayerPrefs 유지) ──
    public static float MasterVolume { get => PlayerPrefs.GetFloat("vol_master", 1f); set { PlayerPrefs.SetFloat("vol_master", Mathf.Clamp01(value)); ApplyVolumes(); } }
    public static float BgmVolume    { get => PlayerPrefs.GetFloat("vol_bgm", 0.7f);  set { PlayerPrefs.SetFloat("vol_bgm", Mathf.Clamp01(value)); ApplyVolumes(); } }
    public static float SfxVolume    { get => PlayerPrefs.GetFloat("vol_sfx", 1f);    set { PlayerPrefs.SetFloat("vol_sfx", Mathf.Clamp01(value)); ApplyVolumes(); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AudioManager");
        Instance = go.AddComponent<AudioManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true; bgmSource.playOnAwake = false;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        ApplyVolumes();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }

    private void OnScene(Scene s, LoadSceneMode m) => Bgm(SceneBgm(s.name));

    // 씬 이름 → BGM 이름. 표만 고치면 됨(빈 문자열 = 음악 없음/정지).
    private static string SceneBgm(string scene)
    {
        switch (scene)
        {
            case "StartScene":    return "title";
            case "TutorialScene": return "tutorial";
            case "StartingArea":  return "village";
            case "Stage1":
            case "Stage2":
            case "Stage3":
            case "MainMap":       return "stage";
            case "BossScene":     return "boss";
            default:              return "";
        }
    }

    // ── 정적 진입점 ──
    // 효과음. pitchJitter를 주면 재생마다 피치가 살짝 달라져 반복감이 줄어든다(타격음 추천 0.06).
    public static void Sfx(string name, float volume = 1f, float pitchJitter = 0f)
    {
        if (Instance == null || string.IsNullOrEmpty(name)) return;
        var clip = LoadSfx(name);
        if (clip == null) return;   // 에셋 없음 — 조용히 무시
        Instance.sfxSource.pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
        Instance.sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * SfxVolume * MasterVolume);
    }

    // 배경음(같은 곡이면 무시, 다르면 크로스페이드). name이 비면 페이드아웃 정지.
    public static void Bgm(string name, float fade = 1.2f)
    {
        if (Instance == null) return;
        if (Instance.currentBgm == name) return;
        Instance.currentBgm = name;
        if (Instance.bgmFade != null) Instance.StopCoroutine(Instance.bgmFade);
        Instance.bgmFade = Instance.StartCoroutine(Instance.FadeBgm(name, Mathf.Max(0.05f, fade)));
    }

    private System.Collections.IEnumerator FadeBgm(string name, float fade)
    {
        // 페이드 아웃
        float startVol = bgmSource.volume;
        for (float t = 0f; t < fade * 0.5f && bgmSource.isPlaying; t += Time.unscaledDeltaTime)
        { bgmSource.volume = Mathf.Lerp(startVol, 0f, t / (fade * 0.5f)); yield return null; }
        bgmSource.Stop();

        var clip = LoadBgm(name);
        if (clip == null) { bgmFade = null; yield break; }   // 에셋 없음 — 정지 상태 유지

        // 페이드 인
        bgmSource.clip = clip;
        bgmSource.volume = 0f;
        bgmSource.Play();
        float target = BgmVolume * MasterVolume;
        for (float t = 0f; t < fade * 0.5f; t += Time.unscaledDeltaTime)
        { bgmSource.volume = Mathf.Lerp(0f, target, t / (fade * 0.5f)); yield return null; }
        bgmSource.volume = target;
        bgmFade = null;
    }

    private static void ApplyVolumes()
    {
        if (Instance == null || Instance.bgmSource == null) return;
        Instance.bgmSource.volume = BgmVolume * MasterVolume;
    }

    private static AudioClip LoadSfx(string name)
    {
        if (sfxCache.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Audio/SFX/" + name);
        sfxCache[name] = c;   // null도 캐시(매번 디스크 조회 방지)
        return c;
    }
    private static AudioClip LoadBgm(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (bgmCache.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Audio/BGM/" + name);
        bgmCache[name] = c;
        return c;
    }
}
