using UnityEngine;

public class AbilityFruit : MonoBehaviour
{
    [Header("技能信息设置")]
    public string abilityName = "下砸技能"; // 解锁的技能名称

    [Header("按键与提示设置")]
    public KeyCode interactKey = KeyCode.F; // 交互按键，默认 F 键
    public GameObject interactUI;          // 提示 UI

    [Header("音效与特效（可选）")]
    public GameObject collectVFXPrefab;    // 拾取时的粒子特效预制体
    public AudioClip collectSFX;           // 拾取音效

    private bool isPlayerInRange = false;  // 玩家是否处于检测范围内
    private GameObject playerGameObject;   // 缓存玩家对象引用
    private bool isCollected = false;

    void Start()
    {
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
    }

    void Update()
    {
        if (isPlayerInRange && !isCollected && Input.GetKeyDown(interactKey))
        {
            isCollected = true;

            if (interactUI != null)
            {
                interactUI.SetActive(false);
            }

            OnCollect(playerGameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isCollected && collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            playerGameObject = collision.gameObject;

            if (interactUI != null)
            {
                interactUI.SetActive(true);
            }

            Debug.Log($"💡 [提示] 靠近了 [{gameObject.name}]，按 [{interactKey}] 键拾取并解锁技能！");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = false;
            playerGameObject = null;

            if (interactUI != null)
            {
                interactUI.SetActive(false);
            }
        }
    }

    private void OnCollect(GameObject player)
    {
        if (collectVFXPrefab != null)
        {
            GameObject vfx = Instantiate(collectVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 1.5f);
        }

        if (collectSFX != null)
        {
            AudioSource.PlayClipAtPoint(collectSFX, transform.position);
        }

        // 🌟 解锁玩家的下砸技能
        ActivateNewAbility(player);

        gameObject.SetActive(false);
    }

    private void ActivateNewAbility(GameObject player)
    {
        PlayerSkillManager skillManager = player.GetComponent<PlayerSkillManager>();
        if (skillManager != null)
        {
            skillManager.UnlockGroundSlam();
            Debug.Log($"🎉【能力解锁】玩家成功拾取了 [{gameObject.name}]！下砸技能已解锁！");
        }
        else
        {
            Debug.LogWarning("⚠️ 玩家身上未找到 PlayerSkillManager 组件！");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}