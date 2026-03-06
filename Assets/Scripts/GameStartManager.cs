using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameStartManager : MonoBehaviour {
    public static GameStartManager Instance;

    public GridDatabase gridDatabase;
    public GameObject playerPrefab;
    public Transform startGrid;
    public DiceAnimator diceAnimator;

    private List<PlayerController> players = new List<PlayerController>();

    void Awake() {
        Instance = this;
        Screen.SetResolution(750, 1334, false);
    }

    void Start() {
        UIManager.Instance.SetExtraButtonsVisible(false);
        UIManager.Instance.SetPlayerStatsVisible(false);

        InitMapBuildings();
        CreatePlayerInstances();

        if (GameDataManager.Instance != null && GameDataManager.Instance.savedPlayers.Count > 0) {
            GameDataManager.Instance.LoadGameState(players);
            ToTurnManagerFromLoad();
        } else {
            InitPlayersToStart();
            StartCoroutine(MainFlow());
        }
    }

    void InitMapBuildings() {
        if (gridDatabase == null) return;
        gridDatabase.RefreshCache();
        List<GridNode> allNodes = gridDatabase.GetAllNodes();
        foreach (GridNode node in allNodes) {
            if (node.currentBuilding != null) continue;

            GameObject prefabToSpawn = null;
            switch (node.type) {
                case GridType.Shop:     prefabToSpawn = GameDataManager.Instance.shopPrefab;     break;
                case GridType.Bank:     prefabToSpawn = GameDataManager.Instance.bankPrefab;     break;
                case GridType.Hospital: prefabToSpawn = GameDataManager.Instance.hospitalPrefab; break;
                case GridType.Prison:   prefabToSpawn = GameDataManager.Instance.prisonPrefab;   break;
            }

            if (prefabToSpawn != null && node.buildingAnchor != null) {
                GameObject b = Instantiate(prefabToSpawn, node.buildingAnchor.position, node.buildingAnchor.rotation);
                b.transform.SetParent(node.buildingAnchor);
                node.currentBuilding = b;
            }
        }
    }

    void CreatePlayerInstances() {
        for (int i = 1; i <= 6; i++) {
            GameObject pObj = Instantiate(playerPrefab);
            PlayerController pc = pObj.GetComponent<PlayerController>();
            pc.playerId = i;
            players.Add(pc);
        }
    }

    void InitPlayersToStart() {
        GridNode startNode = startGrid.GetComponent<GridNode>();
        foreach (var p in players) {
            p.currentGrid = startNode;
            Vector3 pos = startNode.GetSlotPosition(p.gameObject);
            pos.y += p.heightOffset;
            p.transform.position = pos;
        }
    }

    IEnumerator MainFlow() {
        UIManager.Instance.UpdateStatus("决定本局游戏顺序...");
        yield return new WaitForSeconds(1.0f);
        diceAnimator.ShowDice(true);
        diceAnimator.ShowAndIdle();

        UIManager.Instance.ShowActionButton("决定顺序", () => {
            UIManager.Instance.HideActionButton();
            // 告诉服务端本局玩家数量，并清除上一局残留数据
            GameNetworkManager.Instance.ResetOrderRolls(players.Count);
            StartCoroutine(HandleLocalRoll());
            StartCoroutine(SimulateOthersRoll());
        });
    }

    // 本地玩家（玩家1）：播放骰子动画后将结果发给服务端
    IEnumerator HandleLocalRoll() {
        int result = Random.Range(1, 7);
        yield return StartCoroutine(diceAnimator.PlayRollSequence(result, null));
        // [网络] 发送本地玩家骰子结果，服务端收齐后广播顺序
        GameNetworkManager.Instance.SendRollForOrderRequest(players[0].playerId, result);
    }

    // 其他玩家（模拟阶段本地代劳）：逐一延迟发送各自结果给服务端
    IEnumerator SimulateOthersRoll() {
        for (int i = 1; i < players.Count; i++) {
            yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
            int result = Random.Range(1, 7);
            // [网络] 真实联网时，这里改为等待其他客户端自己发送
            GameNetworkManager.Instance.SendRollForOrderRequest(players[i].playerId, result);
        }
    }

    /// <summary>
    /// [网络广播] 服务端收齐所有骰子后回调，下发最终出场顺序
    /// </summary>
    public void ExecuteNetFinalizeOrder(List<int> sortedPlayerIds) {
        UIManager.Instance.UpdateStatus("顺序已定，正式开始！");

        List<PlayerController> sorted = sortedPlayerIds
            .Select(id => players.Find(p => p.playerId == id))
            .Where(p => p != null)
            .ToList();

        StartCoroutine(DelayedStartGame(sorted));
    }

    private IEnumerator DelayedStartGame(List<PlayerController> sorted) {
        yield return new WaitForSeconds(1.5f);
        TurnManager.Instance.BeginGame(sorted);
        gameObject.SetActive(false);
    }

    // 从小游戏返回时直接进入回合，按小游戏名次重排顺序
    void ToTurnManagerFromLoad() {
        List<PlayerController> survivors = players.FindAll(p => p.currentGrid != null);

        // 若小游戏写入了排名，按名次顺序重排（名次靠前 = 先走）
        var ranking = GameDataManager.Instance?.minigameRanking;
        if (ranking != null && ranking.Count > 0) {
            survivors = survivors.OrderBy(p => {
                int idx = ranking.IndexOf(p.playerId);
                return idx < 0 ? int.MaxValue : idx;
            }).ToList();
            GameDataManager.Instance.minigameRanking.Clear();
        }

        TurnManager.Instance.BeginGameFromMinigame(survivors);
        gameObject.SetActive(false);
    }
}
