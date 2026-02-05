using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;

    [Header("引用")]
    public DiceAnimator diceAnimator; 

    private List<PlayerController> turnOrder;
    private int currentIndex = 0;
    private bool isGameActive = false;

    void Awake() {
        if (Instance == null) Instance = this;
    }

    public void BeginGame(List<PlayerController> sortedPlayers) {
        turnOrder = sortedPlayers;
        currentIndex = 0;
        isGameActive = true;
        
        Debug.Log("正式游戏开始！");
        StartTurn();
    }

    void StartTurn() {
        if (!isGameActive) return;

        PlayerController p = turnOrder[currentIndex];

        if (CameraController.Instance != null) {
            CameraController.Instance.SetTarget(p.transform);
        }

        // 更新状态：当前轮到谁
        UIManager.Instance.UpdateStatus($"当前回合：玩家 {p.playerId}");

        if (diceAnimator != null) {
            diceAnimator.ShowAndIdle();
        }

        // 修改术语：投骰子
        UIManager.Instance.ShowActionButton("投骰子", () => {
            UIManager.Instance.HideActionButton();
            StartCoroutine(ProcessTurn());
        });
    }

    IEnumerator ProcessTurn() {
        PlayerController p = turnOrder[currentIndex];
        
        // 1. 投骰子结果
        int diceResult = Random.Range(1, 7);
        UIManager.Instance.UpdateStatus($"玩家 {p.playerId} 投出了 {diceResult} 点");

        // 2. 播放骰子动画 (保留你原本的 PlayRollSequence 逻辑)
        if (diceAnimator != null) {
            yield return StartCoroutine(diceAnimator.PlayRollSequence(diceResult, null));
        }

        // 3. 执行玩家移动
        bool moveFinished = false;
        p.StartMoving(diceResult, () => {
            moveFinished = true;
        });

        // 4. 等待移动完成（包含分叉选择时间）
        while (!moveFinished) {
            yield return null;
        }

        yield return new WaitForSeconds(1.0f);
        EndTurn();
    }

    void EndTurn() {
        if (turnOrder == null || turnOrder.Count == 0) return;
        currentIndex = (currentIndex + 1) % turnOrder.Count;
        StartTurn();
    }
}