using UnityEngine;
using UnityEngine.Rendering.Universal; // 引入 URP 2D Light 命名空间

public class LightBreathe : MonoBehaviour
{
    private Light2D light2D;
    public float minIntensity = 10.0f;  // 最暗强度
    public float maxIntensity = 100.0f;  // 最亮强度
    public float breatheSpeed = 2.5f;  // 呼吸频率

    void Start()
    {
        light2D = GetComponent<Light2D>();
    }

    void Update()
    {
        if (light2D != null)
        {
            // 利用正弦波实现平滑的呼吸发光效果
            float t = (Mathf.Sin(Time.time * breatheSpeed) + 1f) / 2f;
            light2D.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }
    }
}