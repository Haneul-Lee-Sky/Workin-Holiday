using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 손님 스폰 제어기 — StageData 기반으로 스폰 방향, 타입, 간격을 결정하고 CustomerManager에 명령
/// Update 대신 Coroutine을 사용하여 정확한 스폰 타이밍을 보장합니다.
/// </summary>
public class CustomerController : MonoBehaviour
{
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private GameManager gameManager;

    private StageData currentStage;
    private bool isActive = false;
    private Coroutine spawnCoroutine;

    private void Awake()
    {
        if (customerManager == null) customerManager = FindAnyObjectByType<CustomerManager>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
    }

    public void ApplyStageData(StageData data)
    {
        // 이전 코루틴 중지
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        currentStage = data;
        isActive = true;
        spawnCoroutine = StartCoroutine(SpawnLoop());
        Debug.Log($"[CustomerController] 스폰 루프 시작 - {data.stageName} | 간격: {data.spawnInterval}초");
    }

    public void Stop()
    {
        isActive = false;
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        Debug.Log("[CustomerController] 스폰 중지");
    }

    private IEnumerator SpawnLoop()
    {
        // 첫 손님은 1초 뒤 등장
        yield return new WaitForSeconds(1f);

        while (isActive && currentStage != null)
        {
            // 무한 모드: 게임이 활성화된 경우에만 스폰
            if (currentStage.isInfiniteMode && gameManager != null && !gameManager.IsGameActive)
            {
                yield return null;
                continue;
            }

            TrySpawn();

            // 다음 스폰 간격 계산
            float interval = CalculateNextInterval();
            yield return new WaitForSeconds(interval);
        }
    }

    private float CalculateNextInterval()
    {
        if (currentStage.isInfiniteMode && gameManager != null)
        {
            float elapsed = gameManager.ElapsedGameTime;
            float ratio = Mathf.Clamp01(elapsed / currentStage.timeLimit);
            float minInterval = currentStage.spawnInterval * 0.4f;
            return Mathf.Lerp(currentStage.spawnInterval, minInterval, ratio);
        }
        return currentStage.spawnInterval;
    }

    private void TrySpawn()
    {
        if (customerManager == null) return;

        // 허용된 방향 목록
        List<ServingManager.ServeDirection> allowed = new List<ServingManager.ServeDirection>();
        if (currentStage.allowLeft) allowed.Add(ServingManager.ServeDirection.Left);
        if (currentStage.allowRight) allowed.Add(ServingManager.ServeDirection.Right);
        if (currentStage.allowBottom) allowed.Add(ServingManager.ServeDirection.Down);

        if (allowed.Count == 0) return;

        // 빈 슬롯만 필터링
        List<ServingManager.ServeDirection> emptySlots = new List<ServingManager.ServeDirection>();
        foreach (var dir in allowed)
        {
            if (!customerManager.HasCustomerAt(dir))
                emptySlots.Add(dir);
        }

        if (emptySlots.Count == 0) return;

        // 방향 결정
        ServingManager.ServeDirection selectedDir = emptySlots[Random.Range(0, emptySlots.Count)];

        // 손님 타입 결정
        // 규칙: 특수 손님(Rich/Annoying/Kid)은 하단(Down) 슬롯에서만 등장
        CustomerType type;
        if (selectedDir != ServingManager.ServeDirection.Down)
        {
            // 좌/우 슬롯은 항상 일반 손님
            type = CustomerType.Normal;
        }
        else if (currentStage.forceSpecialOnAllSlots)
        {
            // 7~9스테이지 하단: forcedCustomerType 100% 강제
            type = currentStage.forcedCustomerType;
        }
        else if (currentStage.randomizeCustomerType)
        {
            // 무한 모드 등 랜덤 스테이지 하단: 특수 손님 중 무작위
            CustomerType[] specials = { CustomerType.Rich, CustomerType.Annoying, CustomerType.Kid };
            type = specials[Random.Range(0, specials.Length)];
        }
        else
        {
            type = currentStage.forcedCustomerType;
        }

        customerManager.SpawnCustomer(selectedDir, type);
    }
}
