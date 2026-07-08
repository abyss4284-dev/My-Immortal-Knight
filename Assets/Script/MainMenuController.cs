using UnityEngine;
using UnityEngine.SceneManagement; // 👈 🌟 必须引入场景管理命名空间

public class MainMenuController : MonoBehaviour
{
    [Header("要加载的游戏关卡名称")]
    public string gameplaySceneName = "InitialScene"; // 填入你游戏核心关卡的场景名

    // 1. 点击“开始游戏”时调用
    public void PlayGame()
    {
        Debug.Log("正在加载游戏...");

        // 🌟 核心：加载核心玩法场景
        SceneManager.LoadScene(gameplaySceneName);
    }

    // 2. 点击“退出游戏”时调用
    public void QuitGame()
    {
        Debug.Log("玩家退出了游戏！");

        // 🌟 核心：关闭游戏程序（这句代码在编辑器里无效，只有打包打包成.exe或手机Apk后才生效）
        Application.Quit();
    }
}