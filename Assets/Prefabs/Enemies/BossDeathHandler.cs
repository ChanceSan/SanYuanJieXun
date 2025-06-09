using UnityEngine;

public class BossDeathHandler : MonoBehaviour
{
    [Header("����������")]
    public GameObject portalPrefab; // ������Ԥ����
    public float portalSpawnDelay = 1.5f; // Boss���������ɴ����ŵ��ӳ�

    // ��Boss������ֵ�ű��е��ô˷��������統HP<=0ʱ��
    public void OnBossDeath()
    {
        // ��¼Boss����λ��
        Vector3 spawnPosition = transform.position;

        // �ӳ����ɴ�����
        Invoke(nameof(SpawnPortal), portalSpawnDelay);
    }

    private void SpawnPortal()
    {
        if (portalPrefab != null)
        {
            // ��Bossλ�����ɴ�����
            Instantiate(portalPrefab, transform.position, Quaternion.identity);
            Debug.Log("��Bossλ�����ɴ�����");
        }
        else
        {
            Debug.LogError("������Ԥ����δ�����BossDeathHandler�ű���");
        }
    }
}