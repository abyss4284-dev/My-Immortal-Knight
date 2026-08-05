using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractPortal : MonoBehaviour
{
    // 跨场景传递的传送门数据
    public static bool isTransferring = false;
    public static string sourcePortalName = "";

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
        // 只有当玩家在范围内、未在传送中、且按下了 F 键时才触发
        if (isPlayerInRange && !isTransferring && Input.GetKeyDown(interactKey))
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
        if (string.IsNullOrEmpty(targetSceneName) || playerGameObject == null) return;

        // 隐藏提示 UI
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }

        // 记录来源传送门名称（供新场景的初始化脚本寻找生成点）
        sourcePortalName = gameObject.name;

        // 读取玩家组件，保存跨场景生命值与蓝量
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

        isTransferring = true;

        // 调用淡入淡出组件切关
        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(targetSceneName);
        }
        else
        {
            // 备用：若没有 SceneFader 则直接加载场景
            SceneManager.LoadScene(targetSceneName);
        }

        Debug.Log($"🚪 玩家使用传送门 [{gameObject.name}] 正在前往场景: {targetSceneName}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransferring) return;

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