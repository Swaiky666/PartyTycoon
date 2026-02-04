using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour {
    public static TurnManager Instance;
    public DiceAnimator diceAnimator;

    private List<PlayerController> turnOrder;
    private int currentIndex = 0;

    void Awake() { Instance = this; }

    public void BeginGame(List<PlayerController> sortedPlayers) {
        turnOrder = sortedPlayers;
        StartTurn();
    }

    void StartTurn() {
        PlayerController p = turnOrder[currentIndex];
        UIManager.Instance.UpdateStatus($"轮到玩家 {p.playerId}");
        diceAnimator.ShowAndIdle();

        UIManager.Instance.ShowActionButton("投掷燃料", () => {
            UIManager.Instance.HideActionButton();
            StartCoroutine(ProcessTurn());
        });
    }

    IEnumerator ProcessTurn() {
        int fuel = Random.Range(1, 7);
        yield return StartCoroutine(diceAnimator.PlayRollSequence(fuel, null));

        bool done = false;
        turnOrder[currentIndex].StartMoving(fuel, () => done = true);
        
        while (!done) yield return null;

        currentIndex = (currentIndex + 1) % turnOrder.Count;
        yield return new WaitForSeconds(1f);
        StartTurn();
    }
}