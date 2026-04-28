using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    [Header("설정")]
    public GameObject customerPrefab; // 우리가 만든 손님 프리팹
    public Transform[] spawnPoints;    // 손님이 나타날 위치들
    public float spawnInterval = 3f;   // 손님 나오는 간격 (3초)

    private void Awake()
    {
        // 실제 게임 손님은 CustomerManager + CustomerController 만 사용합니다.
        // 여기서 Instantiate 하면 Customer/타이머 없는 NPC만 쌓여 파란 UI 손님과 겹치고 만료도 안 됩니다.
        enabled = false;
    }

    private void Start()
    {
        if (!enabled) return;

        // 게임 시작하면 손님 소환 루틴 시작!
        if (spawnPoints.Length > 0 && customerPrefab != null)
        {
            StartCoroutine(SpawnCustomerRoutine());
        }
        else
        {
            Debug.LogError("SpawnManager: 스폰 포인트나 프리팹이 비어있어요!");
        }
    }

    IEnumerator SpawnCustomerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // 랜덤한 위치 하나 골라서 소환
            int randomIndex = Random.Range(0, spawnPoints.Length);
            Instantiate(customerPrefab, spawnPoints[randomIndex].position, Quaternion.identity);
        }
    }
}