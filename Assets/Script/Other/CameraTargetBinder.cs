using UnityEngine;
// 🌟 关键：Cinemachine 3.0+ 使用这个新的命名空间
using Unity.Cinemachine;

public class CameraTargetBinder : MonoBehaviour
{
    // 🌟 类型更新为 CinemachineCamera
    private CinemachineCamera virtualCamera;
    private Transform currentTarget;

    private void Awake()
    {
        // 🌟 组件获取也更新为 CinemachineCamera
        virtualCamera = GetComponent<CinemachineCamera>();
    }

    private void Start()
    {
        TryBindPlayer();
    }

    private void Update()
    {
        // 逻辑保持不变，依然是检测目标是否丢失
        if (virtualCamera != null && (virtualCamera.Follow == null || currentTarget == null))
        {
            TryBindPlayer();
        }
    }

    private void TryBindPlayer()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineCamera>();
        }

        if (virtualCamera == null)
        {
            Debug.LogError($"🚨 [配置错误] {gameObject.name} 上找不到 CinemachineCamera 组件！");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            currentTarget = player.transform;

            // 🌟 CinemachineCamera 的 API 依然支持 Follow 和 LookAt
            virtualCamera.Follow = currentTarget;
            virtualCamera.LookAt = currentTarget;

            Debug.Log($"📸 [相机自动绑定] 成功捕捉到刚出生的玩家 [{player.name}]");
        }
    }
}