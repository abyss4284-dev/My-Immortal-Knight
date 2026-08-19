using UnityEngine;
using UnityEngine.UI;

public class PlayerPauseMenu : MonoBehaviour
{
    [Header("UI 容器与按钮绑定")]
    [Tooltip("菜单面板根物体（即包含背景和按钮的子物体）")]
    public GameObject menuPanel;
    [Tooltip("回到存档点按钮")]
    public Button respawnButton;
    [Tooltip("退出游戏按钮")]
    public Button quitButton;

    private bool isMenuOpen = false;
    private PlayerController playerController;

    void Awake()
    {
        // 自动获取主角 Controller（因为本脚本挂载在玩家的子物体上）
        playerController = GetComponentInParent<PlayerController>();
    }

    void Start()
    {
        // 默认隐藏菜单
        if (menuPanel != null) menuPanel.SetActive(false);

        // 绑定按钮点击事件
        if (respawnButton != null) respawnButton.onClick.AddListener(OnRespawnButtonClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
    }

    void Update()
    {
        // 按下 ESC 键切换菜单开关状态
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    /// <summary>
    /// 切换 Pause 菜单的显示状态
    /// </summary>
    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (menuPanel != null)
        {
            menuPanel.SetActive(isMenuOpen);
        }

        if (isMenuOpen)
        {
            // 暂停游戏时间
            Time.timeScale = 0f;
        }
        else
        {
            // 恢复游戏时间
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// 按钮事件 1：回到存档点
    /// </summary>
    private void OnRespawnButtonClicked()
    {
        Debug.Log("🔄 玩家点击菜单：回到存档点");

        // 1. 先恢复时间流逝，否则加载场景/协程会卡住
        Time.timeScale = 1f;
        isMenuOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);

        // 2. 调用场景中的 PlayerSpawnManager 进行复活/传送
        PlayerSpawnManager spawnManager = Object.FindFirstObjectByType<PlayerSpawnManager>();
        if (spawnManager != null)
        {
            // 如果玩家当时是死亡状态，重置血量与死亡标志
            if (playerController != null)
            {
                playerController.isDead = false;
                playerController.currentHealth = playerController.maxHealth;
            }

            spawnManager.RespawnPlayer();
        }
        else
        {
            Debug.LogError("🚨 场景中找不到 PlayerSpawnManager，无法返回存档点！");
        }
    }

    /// <summary>
    /// 按钮事件 2：退出游戏
    /// </summary>
    private void OnQuitButtonClicked()
    {
        Debug.Log("🚪 玩家点击菜单：退出游戏");

        // 恢复时间Scale，避免影响编辑器模式
        Time.timeScale = 1f;
        Application.Quit();

#if UNITY_EDITOR
        // 编辑器模式下停止运行
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 打包出的游戏打包体退出程序
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // 移除按钮监听，防止内存泄漏
        if (respawnButton != null) respawnButton.onClick.RemoveListener(OnRespawnButtonClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitButtonClicked);
    }
}