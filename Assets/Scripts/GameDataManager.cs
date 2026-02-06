using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class PlayerState {
    public int playerId;
    public int currentGridId;
    public int money;
    public List<string> cardNames = new List<string>();
}

[System.Serializable]
public class GridState {
    public int gridId;
    public int ownerId;
    public bool hasBarricade; // 新增：路障数据位
}

public class GameDataManager : MonoBehaviour {
    public static GameDataManager Instance;

    [Header("数据快照")]
    public List<PlayerState> savedPlayers = new List<PlayerState>();
    public List<GridState> savedGrids = new List<GridState>();

    [Header("资源库")]
    public List<CardBase> allPossibleCards = new List<CardBase>();
    public GameObject barricadePrefab; // 还原路障所需的预制体

    void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public void SaveCurrentGameState(List<PlayerController> players, List<GridNode> allNodes) {
        savedPlayers.Clear();
        foreach (var p in players) {
            savedPlayers.Add(new PlayerState {
                playerId = p.playerId,
                currentGridId = p.currentGrid != null ? p.currentGrid.gridId : -1,
                money = p.money,
                cardNames = p.cards.Select(c => c.cardName).ToList()
            });
        }

        savedGrids.Clear();
        foreach (var node in allNodes) {
            savedGrids.Add(new GridState {
                gridId = node.gridId,
                ownerId = (node.owner != null) ? node.owner.playerId : -1,
                hasBarricade = node.HasBarricade() // 保存路障状态
            });
        }
    }

    public void LoadGameState(List<PlayerController> players, GridDatabase database) {
        if (savedPlayers.Count == 0) return;

        // 还原玩家 (代码同原版本)
        foreach (var state in savedPlayers) {
            PlayerController p = players.Find(x => x.playerId == state.playerId);
            if (p != null) {
                p.money = state.money;
                GridNode node = database.GetGridById(state.currentGridId);
                if (node != null) p.SetInitialPosition(node);
                p.cards.Clear();
                foreach (string cName in state.cardNames) {
                    CardBase refCard = allPossibleCards.Find(c => c.cardName == cName);
                    if (refCard != null) p.cards.Add(Instantiate(refCard));
                }
            }
        }

        // 还原地块与路障
        foreach (var gState in savedGrids) {
            GridNode node = database.GetGridById(gState.gridId);
            if (node != null) {
                // 还原所有权
                if (gState.ownerId != -1) {
                    node.owner = players.Find(x => x.playerId == gState.ownerId);
                    node.GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f);
                } else {
                    node.owner = null;
                }

                // --- 新增：跨场景还原路障物体 ---
                if (gState.hasBarricade && node.currentBarricade == null) {
                    if (barricadePrefab != null) {
                        node.currentBarricade = Instantiate(barricadePrefab, node.transform.position, Quaternion.identity);
                    }
                }
            }
        }
    }
}