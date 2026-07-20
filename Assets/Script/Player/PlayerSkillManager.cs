using UnityEngine;
using System.Collections;

public class PlayerSkillManager : MonoBehaviour
{
    [Header("蓝条 (Mana) 设置")]
    public int maxMana = 100;
    public int currentMana = 0;

    [Header("闪现技能设置")]
    public int blinkManaCost = 10;        // 闪现消耗蓝量
    public float blinkDistance = 4f;       // 闪现距离
    public float blinkDelay = 0.15f;       // 消失的延迟时间（秒）
    public float blinkCooldown = 0.6f;     // 技能冷却时间
    private float nextBlinkTime = 0f;

    // 组件引用
    private PlayerController playerController;
    private Rigidbody2D rb;
    private SpriteRenderer[] playerSprites;
    private UIManager uiManager;

    [Header("安全探测设置")]
    public Transform blinkTargetMarker; // 🌟 把你场景里那个名为“闪现判定”的子物体拖到这里
    public LayerMask obstacleLayers;    // 在 Inspector 里勾选建筑、墙壁、地面等实体层（避开 Player 层）
    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        // 1. 跨场景蓝量继承
        if (PlayerController.savedMana != -1)
        {
            currentMana = PlayerController.savedMana;
        }
        else
        {
            currentMana = 0;
        }

        playerSprites = GetComponentsInChildren<SpriteRenderer>();

        // 🌟 动态生成流不需要在这里 Find 了，因为 SpawnManager 在克隆出你的时候
        // 已经主动通过 SetupSkillManager() 把 UI 塞给你了！

        // 如果你希望多加一层保险（比如测试单场景时），可以留一句兜底：
        if (uiManager == null)
        {
            uiManager = Object.FindFirstObjectByType<UIManager>();
        }

        // 确保刚出生时，UI 的数值是对齐的
        if (uiManager != null)
        {
            uiManager.UpdateSoulUI(currentMana, maxMana);
        }
    }

    void Update()
    {
        // 监听 K 键触发闪现
        if (Input.GetKeyDown(KeyCode.K) && Time.time >= nextBlinkTime)
        {
            TryCastBlink();
        }
    }

    private void TryCastBlink()
    {
        if (currentMana >= blinkManaCost)
        {
            currentMana -= blinkManaCost;
            PlayerController.savedMana = currentMana;

            if (uiManager != null) uiManager.UpdateSoulUI(currentMana, maxMana);

            // 🌟 启动改进后的空物体探测闪现
            StartCoroutine(BlinkWithDetectorRoutine());
            nextBlinkTime = Time.time + blinkCooldown;
        }
        else
        {
            Debug.Log("❌ 蓝量不足，无法闪现！");
        }
    }

    private IEnumerator BlinkWithDetectorRoutine()
    {
        // 1. 角色暂时消失
        SetPlayerVisibility(false);

        Rigidbody2D playerRb = GetComponent<Rigidbody2D>();
        Vector3 finalBlinkPosition = transform.position;

        if (playerRb != null && blinkTargetMarker != null)
        {
            // 计算闪现判定子物体相对于玩家当前位置的方向和总距离
            Vector2 direction = (blinkTargetMarker.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, blinkTargetMarker.position);

            // 🌟 核心修复：用你的 obstacleLayers 初始化一个接触过滤器
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleLayers);
            filter.useLayerMask = true;
            filter.useTriggers = false; // 顺便防御：忽略掉触发器，只撞击实体墙壁

            // 刚体投影探测
            RaycastHit2D[] hits = new RaycastHit2D[1];
            // 传入 filter 过滤器代替原先单纯的 LayerMask
            int hitCount = playerRb.Cast(direction, filter, hits, distance);

            if (hitCount > 0)
            {
                // 如果撞墙了，只移动到刚好撞墙前的那个安全点
                float safeDistance = Mathf.Max(0, hits[0].distance - 0.1f);
                finalBlinkPosition = (Vector2)transform.position + direction * safeDistance;
                Debug.Log($"🚧 [闪现防护] 刚体检测到前方有阻挡，已修正落脚点，避免卡入 {hits[0].collider.name}");
            }
            else
            {
                // 前方畅通无阻，直接去子物体“闪现判定”所在的位置
                finalBlinkPosition = blinkTargetMarker.position;
            }
        }

        // 2. 消失延迟
        yield return new WaitForSeconds(blinkDelay);

        // 3. 瞬间移动到计算出的绝对安全点
        transform.position = finalBlinkPosition;

        // 4. 恢复可见性
        SetPlayerVisibility(true);
        Debug.Log("🔮 闪现安全落位！");
    }


    // 🌟 公开方法：供 PlayerController 在 Attack() 成功砍中怪物时呼叫
    public void AddManaOnHit()
    {
        currentMana = Mathf.Clamp(currentMana + 10, 0, maxMana);
        PlayerController.savedMana = currentMana; // 同步静态变量

        if (uiManager != null) uiManager.UpdateSoulUI(currentMana, maxMana);
        Debug.Log($"✨ 击中怪物，恢复 10 点蓝量。当前蓝量: {currentMana}");
    }

    private void SetPlayerVisibility(bool isVisible)
    {
        if (playerSprites == null) return;
        foreach (SpriteRenderer sprite in playerSprites)
        {
            if (sprite != null) sprite.enabled = isVisible;
        }
    }
}