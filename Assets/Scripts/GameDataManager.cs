using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 1. 定义纯数据类，用于序列化保存
[System.Serializable]
public class PlayerState {
    public int playerId;
    public int currentGridId;
    public int money;
    public List<string> cardNames = new List<string>(); // 保存卡牌资源的名称
}

[System.Serializable]
public class GridState {
    public int gridId;
    public int ownerId; // -1 表示无人占领
}

public class GameDataManager : MonoBehaviour {
    public static GameDataManager Instance;

    [Header("数据快照")]
    public List<PlayerState> savedPlayers = new List<PlayerState>();
    public List<GridState> savedGrids = new List<GridState>();

    [Header("卡牌资源库")]
    // 在 Inspector 中把所有可能的 CardBase 资源拖进去，用于加载时按名字匹配
    public List<CardBase> allPossibleCards = new List<CardBase>();

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 保存当前游戏状态（切小游戏或存档前调用）
    /// </summary>
    public void SaveCurrentGameState(List<PlayerController> players, List<GridNode> allNodes) {
        savedPlayers.Clear();
        foreach (var p in players) {
            PlayerState state = new PlayerState {
                playerId = p.playerId,
                currentGridId = p.currentGrid != null ? p.currentGrid.gridId : -1,
                money = p.money,
                // 将 PlayerController 中的 List<CardBase> 转换为名字列表
                cardNames = p.cards.Select(c => c.cardName).ToList()
            };
            savedPlayers.Add(state);
        }

        savedGrids.Clear();
        foreach (var node in allNodes) {
            savedGrids.Add(new GridState {
                gridId = node.gridId,
                ownerId = (node.owner != null) ? node.owner.playerId : -1
            });
        }
        Debug.Log("游戏数据已成功快照。");
    }

    /// <summary>
    /// 还原游戏状态（从小游戏返回或读档时调用）
    /// </summary>
    public void LoadGameState(List<PlayerController> players, GridDatabase database) {
        if (savedPlayers.Count == 0) return;

        foreach (var state in savedPlayers) {
            PlayerController p = players.Find(x => x.playerId == state.playerId);
            if (p != null) {
                p.money = state.money;
                
                // 还原地块位置
                GridNode node = database.GetGridById(state.currentGridId);
                if (node != null) p.SetInitialPosition(node);

                // 还原卡牌列表
                p.cards.Clear();
                foreach (string cName in state.cardNames) {
                    CardBase refCard = allPossibleCards.Find(c => c.cardName == cName);
                    if (refCard != null) {
                        // 注意：这里建议使用 Instantiate 复制一份，防止多个玩家共用同一个 SO 实例导致数据污染
                        p.cards.Add(Instantiate(refCard));
                    }
                }
            }
        }

        // 还原土地归属权
        foreach (var gState in savedGrids) {
            GridNode node = database.GetGridById(gState.gridId);
            if (node != null) {
                if (gState.ownerId != -1) {
                    node.owner = players.Find(x => x.playerId == gState.ownerId);
                    // 顺便刷一下颜色（示例逻辑）
                    node.GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f);
                } else {
                    node.owner = null;
                }
            }
        }
        Debug.Log("游戏数据已成功加载还原。");
    }
}