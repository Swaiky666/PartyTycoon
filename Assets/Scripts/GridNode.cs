using UnityEngine;
using System.Collections.Generic;

public enum GridType { Empty, Bank, Hospital, Prison, Treasure, Shop, Park, Trap, Station }

public class GridNode : MonoBehaviour {
    [Header("唯一标识")]
    public int gridId = -1; 
    public GridType type;

    [Header("土地经济")]
    public int purchasePrice = 500; 
    public int rentPrice = 200;
    public PlayerController owner = null;

    [Header("四向连接 (0:北, 1:东, 2:南, 3:西)")]
    public GridNode[] connections = new GridNode[4]; 

    [Header("6人站位锚点")]
    public Transform[] slotPoints = new Transform[6];
    private List<GameObject> playersInGrid = new List<GameObject>();

    // --- 新增：路障运行时引用 ---
    [Header("运行时状态")]
    public GameObject currentBarricade; 

    public bool HasBarricade() => currentBarricade != null;

    public void ClearBarricade() {
        if (currentBarricade != null) {
            Destroy(currentBarricade);
            currentBarricade = null;
        }
    }

    public static int GetOppositeDirection(int dir) { return (dir + 2) % 4; }

    public Vector3 GetSlotPosition(GameObject player) {
        if (!playersInGrid.Contains(player)) playersInGrid.Add(player);
        int index = playersInGrid.IndexOf(player);
        if (index >= 0 && index < slotPoints.Length && slotPoints[index] != null) 
            return slotPoints[index].position;
        return transform.position;
    }

    public void RemovePlayer(GameObject player) { playersInGrid.Remove(player); }

    private void OnDrawGizmos() {
        Color[] dirColors = { Color.blue, Color.red, Color.yellow, Color.green };
        for (int i = 0; i < 4; i++) {
            if (connections[i] != null) {
                Gizmos.color = dirColors[i];
                Gizmos.DrawLine(transform.position, connections[i].transform.position);
            }
        }
    }
}