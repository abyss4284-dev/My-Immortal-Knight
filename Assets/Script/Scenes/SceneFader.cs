using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Header("UI 组件")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("渐变参数")]
    [Tooltip("黑幕遮蔽/揭开的时间（秒）")]
    public float fadeDuration = 0.5f;

    private void Awake()
    {
        // 单例模式 + 跨场景不销毁
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }

        // 初始化：确保游戏刚打开时遮罩是透明的，且不挡鼠标点击
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 外部调用的核心方法：带黑幕缓冲地加载场景
    /// </summary>
    public void FadeToScene(string sceneName, System.Action onMiddleAction = null)
    {
        StartCoroutine(FadeRoutine(sceneName, onMiddleAction));
    }

    private IEnumerator FadeRoutine(string sceneName, System.Action onMiddleAction)
    {
        // 1. 阻止玩家点击 UI/交互，黑幕淡出 (透明 ➔ 纯黑)
        fadeCanvasGroup.blocksRaycasts = true;
        yield return StartCoroutine(Fade(1f));

        // 2. 如果在黑幕最黑时有额外逻辑（如重置坐标等），在此执行
        onMiddleAction?.Invoke();

        // 3. 异步加载目标场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = true;

        // 等待场景真正加载完毕
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 预留一小段极短的缓冲，等新场景 Awake/Start 跑完
        yield return new WaitForSeconds(0.1f);

        // 4. 黑幕淡入 (纯黑 ➔ 透明)
        yield return StartCoroutine(Fade(0f));

        // 5. 解除点击拦截
        fadeCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 渐变控制协程
    /// </summary>
    private IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = fadeCanvasGroup.alpha;
        float speed = Mathf.Abs(targetAlpha - startAlpha) / fadeDuration;

        while (!Mathf.Approximately(fadeCanvasGroup.alpha, targetAlpha))
        {
            fadeCanvasGroup.alpha = Mathf.MoveTowards(fadeCanvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }
}