using UnityEngine;
using UnityEngine.SceneManagement; // 👈 核心：引入场景管理命名空间

public class ScenePortal : MonoBehaviour
{
    [Header("要跳转的场景名字")]
    public string targetSceneName = "The Land of Sacred Waters";

    // 当有物体进入触发器时自动调用（如果是3D游戏，请把2D去掉，改为 OnTriggerEnter）
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 🌟 检查穿过空气墙的是不是玩家（防止怪物或飞刀把场景切了）
        // 确保你的玩家物体（Player）在 Inspector 顶部的 Tag 被设置为了 "Player"
        if (other.CompareTag("Player"))
        {
            Debug.Log("玩家穿过了传送门，开始切换场景！");

            // 🌟 核心代码：加载新场景
            SceneManager.LoadScene(targetSceneName);
        }
    }
}