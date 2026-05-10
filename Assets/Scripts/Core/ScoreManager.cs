using UnityEngine;
using System;

/// <summary>
/// 점수 매니저 — 점수, 콤보 관리
/// Kid 손님의 콤보+2를 지원하는 comboIncrement 파라미터 추가
/// </summary>
public class ScoreManager : MonoBehaviour
{
    private int currentCombo = 0;
    private int currentScore = 0;

    public event Action<int, int> OnScoreChanged; // Score, Combo
    public event Action OnComboReset;

    public int CurrentCombo => currentCombo;
    public int CurrentScore => currentScore;

    /// <summary>
    /// 서빙 성공 시 호출 (Kid 손님은 comboIncrement=2)
    /// </summary>
    public void OnServeSuccess(int comboIncrement = 1)
    {
        currentCombo += comboIncrement;
        int earnedScore = 500 + (currentCombo * 10);
        currentScore += earnedScore;
        Debug.Log($"[ScoreManager] 서빙 성공! +{earnedScore}점 (콤보: {currentCombo}, +{comboIncrement}) | 총점: {currentScore}");
        OnScoreChanged?.Invoke(currentScore, currentCombo);
    }

    public void OnServeFailure()
    {
        if (currentCombo > 0)
        {
            Debug.Log($"[ScoreManager] 콤보 초기화 (이전 콤보: {currentCombo})");
        }
        currentCombo = 0;
        OnComboReset?.Invoke();
        OnScoreChanged?.Invoke(currentScore, currentCombo);
    }

    /// <summary>
    /// 손님 만료 시 콤보만 초기화 (점수는 유지)
    /// </summary>
    public void ResetCombo()
    {
        if (currentCombo > 0)
        {
            Debug.Log($"[ScoreManager] 손님 만료로 콤보 초기화 (이전: {currentCombo})");
        }
        currentCombo = 0;
        OnComboReset?.Invoke();
        OnScoreChanged?.Invoke(currentScore, currentCombo);
    }

    /// <summary>
    /// 점수 + 콤보 완전 초기화 (스테이지 전환 시)
    /// </summary>
    public void ResetScore()
    {
        currentCombo = 0;
        currentScore = 0;
        OnScoreChanged?.Invoke(0, 0);
    }

    /// <summary>
    /// 현재 콤보에 따른 손님 요구 토핑 수 (1~3)
    /// </summary>
    public int GetRequiredToppingCount()
    {
        if (currentCombo >= 20) return 3;
        if (currentCombo >= 10) return 2;
        return 1;
    }
}
