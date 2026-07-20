using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    public static bool isTransferring = false;
    public static string sourcePortalName = "";
    public static float relativeHitRatio = 0.5f;

    [HideInInspector] public string targetSceneName;
    public Vector3 localExitDirection;

    // 🌟 核心改进：逻辑锁。每个空气墙自己记录：当前有没有玩家“正在从我肚子里出生”
    [HideInInspector] public bool isPlayerSpawningInside = false;

    private void Start()
    {
        ParsePortalName();
        CalculateExitDirection();
    }

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
        Debug.LogError($"🚨 [命名错误] 空气墙 [{portalName}] 命名不规范！");
    }

    private void CalculateExitDirection()
    {
        Vector3 toCenter = (Vector3.zero - transform.position).normalized;

        if (Mathf.Abs(toCenter.x) > Mathf.Abs(toCenter.y))
        {
            localExitDirection = new Vector3(Mathf.Sign(toCenter.x), 0, 0);
        }
        else
        {
            localExitDirection = new Vector3(0, Mathf.Sign(toCenter.y), 0);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransferring) return;

        if (other.CompareTag("Player") && !string.IsNullOrEmpty(targetSceneName))
        {
            // 🌟 核心防线：如果这个玩家是刚在这里出生的，且还没走出去，直接拦截，绝不二次传送！
            if (isPlayerSpawningInside)
            {
                return;
            }

            sourcePortalName = gameObject.name;

            Bounds portalBounds = GetComponent<Collider2D>().bounds;
            Vector3 hitPoint = other.transform.position;

            if (portalBounds.size.y > portalBounds.size.x)
            {
                relativeHitRatio = (hitPoint.y - portalBounds.min.y) / portalBounds.size.y;
            }
            else
            {
                relativeHitRatio = (hitPoint.x - portalBounds.min.x) / portalBounds.size.x;
            }
            relativeHitRatio = Mathf.Clamp01(relativeHitRatio);

            PlayerController playerScript = other.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                PlayerController.savedHealth = playerScript.currentHealth;
                PlayerController.savedMana = playerScript.currentMana;
            }

            isTransferring = true;
            SceneManager.LoadScene(targetSceneName);
        }
    }

    // 🌟 当玩家肉体完全离开这个 Trigger 时，解开逻辑锁！
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (isPlayerSpawningInside)
            {
                isPlayerSpawningInside = false;
                Debug.Log($"✅ [绝对安全解除] 玩家已完全离开 [{gameObject.name}] 区域，折返响应已重新充能！");
            }
        }
    }
}