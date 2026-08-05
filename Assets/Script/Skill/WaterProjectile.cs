using UnityEngine;

public class WaterProjectile : MonoBehaviour
{
    [Header("水炮参数")]
    public float speed = 10f;          // 飞行速度
    public int damage = 20;            // 伤害值
    public float maxLifetime = 3f;     // 最大存活时间

    private Vector2 moveDirection = Vector2.right;

    public void Initialize(Vector2 direction)
    {
        moveDirection = direction.normalized;

        // 🌟 根据传入的向量朝向自动翻转贴图 scale.x
        Vector3 scale = transform.localScale;
        if (moveDirection.x < 0)
        {
            scale.x = -Mathf.Abs(scale.x);
        }
        else
        {
            scale.x = Mathf.Abs(scale.x);
        }
        transform.localScale = scale;

        Destroy(gameObject, maxLifetime);
    }

    void Update()
    {
        // 沿实际方向飞行
        transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 撞到地面/墙壁 (Ground)
        if (collision.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
        // 撞到敌人 (Enemy)
        else if (collision.CompareTag("Enemy"))
        {
            // 向碰撞物体及其父节点广播 "TakeDamage"
            collision.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            Debug.Log($"🌊 水炮击中 [{collision.name}]，造成 {damage} 点伤害！");

            Destroy(gameObject);
        }
    }
}