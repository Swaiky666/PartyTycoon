using UnityEngine;
using System.Collections;

public class NetworkManager : MonoBehaviour {
    public static NetworkManager Instance;
    
    [Header("模拟延迟")]
    public float simulatedLatency = 0.1f; 

    void Awake() {
        if (Instance == null) { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        } else { 
            Destroy(gameObject); 
        }
    }

    // [请求] 客户端发起掷骰子
    public void SendRollDiceRequest(int playerId) {
        Debug.Log($"<color=cyan>[网络请求] 玩家 {playerId} 请求掷骰子</color>");
        StartCoroutine(SimulateServerProcess(playerId));
    }

    private IEnumerator SimulateServerProcess(int playerId) {
        // 模拟网络传输耗时
        yield return new WaitForSeconds(simulatedLatency);
        
        // 模拟服务器计算结果
        int result = Random.Range(1, 7);
        
        // [广播] 告诉全场：某人掷出了几点
        if (TurnManager.Instance != null) {
            TurnManager.Instance.ExecuteNetRollDice(playerId, result);
        }
    }
}