using UnityEngine;
using UnityEngine.UI;          // 👈 控制 Slider 需要
using UnityEngine.Audio;       // 👈 控制 AudioMixer 需要
using UnityEngine.SceneManagement; // 🌟 必须引入：用于场景切换

public class MainMenuController : MonoBehaviour
{
    [Header("UI 面板引用")]
    public GameObject optionsPanel;     // 拖入 Options_Panel
    public GameObject mainButtonGoup;   // 拖入你原本的主按钮组（可选，用于隐藏主菜单按钮）

    [Header("音频控制")]
    public AudioMixer audioMixer;       // 拖入你的 MainMixer
    public Slider volumeSlider;         // 拖入 Volume_Slider

    void Start()
    {
        // 游戏启动时，让滑动条的初始值等于当前音量（防止滑块错位）
        if (audioMixer != null && volumeSlider != null)
        {
            float currentVolume;
            // 获取当前 Mixer 的音量（Git底层值）并反算回滑动条的 0-1 之间
            audioMixer.GetFloat("MyVolume", out currentVolume);
            // 简单线性折算：AudioMixer的0dB对应Slider的1，-40dB对应0
            volumeSlider.value = Mathf.InverseLerp(-40f, 0f, currentVolume);

            // 🌟 监听滑动条的数值变化
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    // 🌟 1. 新增：点击“开始游戏”按钮时调用，加载初始场景
    public void PlayGame()
    {
        Debug.Log("🎮 正在加载初始场景：InitialScene...");
        SceneManager.LoadScene("InitialScene");
    }

    // 🌟 2. 新增：点击“退出游戏”按钮时调用
    public void QuitGame()
    {
        Debug.Log("🚪 玩家点击退出游戏");

#if UNITY_EDITOR
        // 如果在 Unity 编辑器中运行，停止 Play 模式
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 如果是在打包后的 exe 中运行，退出程序
        Application.Quit();
#endif
    }

    // 3. 点击“设置”按钮时调用
    public void OpenOptions()
    {
        optionsPanel.SetActive(true);       // 显示设置面板
        if (mainButtonGoup != null) mainButtonGoup.SetActive(false); // 隐藏主菜单按钮
    }

    // 4. 点击设置面板里的“返回”按钮时调用
    public void CloseOptions()
    {
        optionsPanel.SetActive(false);      // 隐藏设置面板
        if (mainButtonGoup != null) mainButtonGoup.SetActive(true);  // 重新显示主菜单
    }

    // 5. 当滑动条被拖动时自动调用的方法
    public void SetVolume(float sliderValue)
    {
        // 因为人类耳朵对声音强度的感知是指数型的，而 Slider 是线性的
        // 将 Slider 的 0~1 映射到 Mixer 的 -40dB 到 0dB（低于-40分贝基本就听不到了）
        float mixerVolume = Mathf.Lerp(-40f, 0f, sliderValue);

        // 如果滑块拉到最左边（0），直接强制静音（-80dB）
        if (sliderValue <= 0.01f) mixerVolume = -80f;

        // 🌟 核心：改变 Mixer 的音量
        audioMixer.SetFloat("MyVolume", mixerVolume);
    }
}