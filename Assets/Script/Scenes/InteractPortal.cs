using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractPortal : MonoBehaviour
{
    // 🌟 核心修改 1：保留属性访问接口，但底层数据直接映射到 ScenePortal！
    // 这样不用修改外部引用，同时确保 PlayerSpawnManager 能够统一下单与清空标记。
    public static bool isTransferring
    {
        get => ScenePortal.isTransferring;
        set => ScenePortal.isTransferring = value;
    }

    public static string sourcePortalName
    {
        get => ScenePortal.sourcePortalName;
        set => ScenePortal.sourcePortalName = value;
    }

    [Header("按键与提示设置")]
    public KeyCode interactKey = KeyCode.F; // 默认按 F 键传送
    public GameObject interactUI;          // 提示 UI 预制体/物体（如 "按 F 进入"）

    [Header("目标场景设置")]
    [HideInInspector] public string targetSceneName;

    private bool isPlayerInRange = false;
    private GameObject playerGameObject;

    private void Start()
    {
        ParsePortalName();

        // 初始时确保提示 UI 是隐藏的
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
    }

    private void Update()
    {
        // 只有当玩家在范围内、当前未处于传送过渡中、且按下了交互键时才触发
        if (isPlayerInRange && !ScenePortal.isTransferring && Input.GetKeyDown(interactKey))
        {
            TriggerSceneTransition();
        }
    }

    /// <summary>
    /// 解析传送门物体名称，自动识别目标场景 (格式: CurrentScene-TargetScene)
    /// </summary>
    private void ParsePortalName()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        string portalName = gameObject.name;

        if (portalName.Contains("-"))
        {
            string[] parts = portalName.Split('-');
            if (parts.Length == 2 && parts[0] == currentScene)
            {
                targetSceneName = parts[1];
                return;
            }
        }
        Debug.LogError($"🚨 [命名错误] 传送门 [{portalName}] 命名不规范！正确格式应为: {currentScene}-目标场景名");
    }

    /// <summary>
    /// 执行切关与数据保存逻辑
    /// </summary>
    private void TriggerSceneTransition()
    {
        // 容错 1：若 targetSceneName 为空，尝试再次自动解析
        if (string.IsNullOrEmpty(targetSceneName))
        {
            ParsePortalName();
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError($"🚨 [传送失败] 传送门 [{gameObject.name}] 无法获取目标场景名称，请检查物体命名！");
            return;
        }

        // 隐藏提示 UI
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }

        // 🌟 核心修改 2：向 ScenePortal 写入传送数据，供 PlayerSpawnManager 识别并设置坐标
        ScenePortal.sourcePortalName = gameObject.name;
        ScenePortal.isTransferring = true;

        // 容错 2：若缓存的 playerGameObject 为空，主动查找 Player
        if (playerGameObject == null)
        {
            playerGameObject = GameObject.FindWithTag("Player");
        }

        // 读取玩家组件，保存跨场景生命值与蓝量
        if (playerGameObject != null)
        {
            PlayerController playerScript = playerGameObject.GetComponent<PlayerController>();
            PlayerSkillManager skillScript = playerGameObject.GetComponent<PlayerSkillManager>();

            if (playerScript != null)
            {
                PlayerController.savedHealth = playerScript.currentHealth;
            }

            if (skillScript != null)
            {
                PlayerController.savedMana = skillScript.currentMana;
            }
        }

        // 调用淡入淡出组件切关
        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(targetSceneName);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
        }

        Debug.Log($"🚪 玩家使用传送门 [{gameObject.name}] 正在前往场景: {targetSceneName}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 🌟 核心修改 3：移除了此处的 `if (isTransferring) return;` 拦截！
        // 因为交互门必须手动按 F 才会传送，即使玩家刚好出生在传送门里面，也不会误触发传送。
        // 这样能确保玩家传送到新场景后，直接进入范围就能正常触发 UI 提示并随时按 F 传回。
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;
            playerGameObject = other.gameObject;

            // 显示按键提示 UI
            if (interactUI != null)
            {
                interactUI.SetActive(true);
            }

            Debug.Log($"💡 [提示] 靠近传送门 [{gameObject.name}]，按 [{interactKey}] 键进入 [{targetSceneName}]！");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            playerGameObject = null;

            // 离开区域时隐藏 UI
            if (interactUI != null)
            {
                interactUI.SetActive(false);
            }
        }
    }
}