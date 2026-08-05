using UnityEngine;
using UnityEngine.Rendering.Universal; // 引入 URP 2D Light 命名空间

public class SproutVineController : MonoBehaviour
{
    [Header("组件引用")]
    private Animator anim;
    private BoxCollider2D col;

    [Header("灯光设置")]
    [Tooltip("需要关闭的 2D 灯光子物体/组件（若不拖入，脚本会自动在子物体中寻找）")]
    public GameObject lightChild; // 也可以直接挂载含有 Light2D 的 GameObject

    [Header("状态标记")]
    private bool isGrown = false;

    void Awake()
    {
        anim = GetComponent<Animator>();
        col = GetComponent<BoxCollider2D>();

        // 如果 Inspector 没有手动拖入灯光物体，自动获取子物体中的 Light2D 所在 GameObject
        if (lightChild == null)
        {
            Light2D light2D = GetComponentInChildren<Light2D>();
            if (light2D != null)
            {
                lightChild = light2D.gameObject;
            }
        }
    }

    /// <summary>
    /// 被水炮击中时调用的入口方法
    /// </summary>
    public void GrowIntoVine()
    {
        if (isGrown) return; // 已经长成藤蔓后不再重复触发

        isGrown = true;

        // 1. 播放生长动画
        if (anim != null)
        {
            anim.SetTrigger("GrowTrigger");
        }

        // 2. 🌟 关闭灯光子物体
        if (lightChild != null)
        {
            lightChild.SetActive(false);
            Debug.Log($"💡 [{gameObject.name}] 的灯光子物体已关闭。");
        }

        Debug.Log($"🌱 [{gameObject.name}] 受到水流滋养，开始生长为藤蔓！");
    }

    // 🌟 供 WaterProjectile 或 SendMessage 统一调用的受击接口
    public void TakeDamage(int damage)
    {
        // 收到“水炮攻击”时触发成长
        GrowIntoVine();
    }
}