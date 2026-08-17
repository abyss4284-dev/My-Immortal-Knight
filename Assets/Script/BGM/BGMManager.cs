using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("音频组件")]
    public AudioSource audioSource;

    [Header("音量设置")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;          // 目标最高音量
    public float defaultFadeDuration = 1.0f; // 默认淡入淡出总时长（秒）

    private Coroutine fadeCoroutine;

    void Awake()
    {
        // 🌟 单例模式：确保全局只有一个 BGM 管理器，且跨场景切换时不被销毁
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.loop = true;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 🌟 核心方法：平滑切换背景音乐（淡出当前 -> 换曲 -> 淡入新曲）
    /// </summary>
    /// <param name="newClip">新场景的 BGM（如果传入 null，则仅淡出并停止播放）</param>
    /// <param name="fadeDuration">淡入淡出总时长（秒），传 -1 则使用默认时长</param>
    public void ChangeBGM(AudioClip newClip, float fadeDuration = -1f)
    {
        if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

        // 如果播放的是完全相同的音乐且正在播放，跳过切换
        if (audioSource.clip == newClip && audioSource.isPlaying) return;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeAndChangeBGMRoutine(newClip, fadeDuration));
    }

    /// <summary>
    /// 🌟 便捷方法：带 BGM 淡入淡出效果的场景加载
    /// </summary>
    public void LoadSceneWithBGM(string sceneName, AudioClip newBGM = null, float fadeDuration = 1.0f)
    {
        StartCoroutine(LoadSceneRoutine(sceneName, newBGM, fadeDuration));
    }

    private IEnumerator FadeAndChangeBGMRoutine(AudioClip newClip, float fadeDuration)
    {
        float halfDuration = fadeDuration * 0.5f;

        // 1. 淡出旧音乐 (Fade Out)
        if (audioSource.isPlaying && audioSource.volume > 0)
        {
            float startVolume = audioSource.volume;
            float timer = 0f;

            while (timer < halfDuration)
            {
                timer += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime，不受 Time.timeScale 影响
                audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / halfDuration);
                yield return null;
            }

            audioSource.volume = 0f;
            audioSource.Stop();
        }

        // 2. 更换音乐片段
        audioSource.clip = newClip;

        // 3. 淡入新音乐 (Fade In)
        if (newClip != null)
        {
            audioSource.Play();
            float timer = 0f;

            while (timer < halfDuration)
            {
                timer += Time.unscaledDeltaTime;
                audioSource.volume = Mathf.Lerp(0f, maxVolume, timer / halfDuration);
                yield return null;
            }

            audioSource.volume = maxVolume;
        }
    }

    private IEnumerator LoadSceneRoutine(string sceneName, AudioClip newBGM, float fadeDuration)
    {
        // 1. 开始切换/淡出 BGM
        ChangeBGM(newBGM, fadeDuration);

        // 2. 等待音乐淡出到最暗（半程时间）再进行场景加载，体验更自然
        yield return new WaitForSecondsRealtime(fadeDuration * 0.5f);

        // 3. 加载新场景
        SceneManager.LoadScene(sceneName);
    }
}