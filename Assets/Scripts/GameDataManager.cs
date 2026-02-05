using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlayerState {
    public int playerId;
    public int currentGridId;
    public int money;
    public List<string> cardIds = new List<string>();
}

[System.Serializable]
public class GridState {
    public int gridId;
    public int ownerId; 
}

public class GameDataManager : MonoBehaviour {
    public static GameDataManager Instance;
    public List<PlayerState> savedPlayers = new List<PlayerState>();
    public List<GridState> savedGrids = new List<GridState>();

    void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public void SaveGameState(List<PlayerController> players, GridNode[] allNodes) {
        savedPlayers.Clear();
        foreach (var p in players) {
            savedPlayers.Add(new PlayerState {
                playerId = p.playerId,
                currentGridId = p.currentGrid.gridId,
                money = p.money,
                cardIds = new List<string>(p.inventoryCards)
            });
        }
        savedGrids.Clear();
        foreach (var node in allNodes) {
            savedGrids.Add(new GridState {
                gridId = node.gridId,
                ownerId = (node.owner != null) ? node.owner.playerId : -1
            });
        }
    }
}