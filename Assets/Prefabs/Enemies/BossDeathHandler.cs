using UnityEngine;

public class BossDeathHandler : MonoBehaviour
{
    [Header("传送门设置")]
    public GameObject portalPrefab; // 传送门预制体
    public float portalSpawnDelay = 1.5f; // Boss死亡后生成传送门的延迟

    // 在Boss的生命值脚本中调用此方法（例如当HP<=0时）
    public void OnBossDeath()
    {
        // 记录Boss死亡位置
        Vector3 spawnPosition = transform.position;

        // 延迟生成传送门
        Invoke(nameof(SpawnPortal), portalSpawnDelay);
    }

    private void SpawnPortal()
    {
        if (portalPrefab != null)
        {
            // 在Boss位置生成传送门
            Instantiate(portalPrefab, transform.position, Quaternion.identity);
            Debug.Log("在Boss位置生成传送门");
        }
        else
        {
            Debug.LogError("传送门预制体未分配给BossDeathHandler脚本！");
        }
    }
}