using UnityEngine;
using System.Collections.Generic;

// 确保枚举完整，不影响你的逻辑判断
public enum GridType { Empty, Bank, Hospital, Prison, Treasure, Shop, Park, Trap, Station }

public class GridNode : MonoBehaviour {
    [Header("唯一标识")]
    public int gridId = -1; 
    public GridType type;

    [Header("土地经济")]
    public int purchasePrice = 500; 
    public int rentPrice = 200;
    public PlayerController owner = null;

    [Header("建筑配置")]
    public Transform buildingAnchor;    
    public GameObject currentBuilding;   

    [Header("运行时状态")]
    public GameObject currentBarricade; 
    public GridNode[] connections = new GridNode[4]; 
    public Transform[] slotPoints = new Transform[6];
    private List<GameObject> playersInGrid = new List<GameObject>();

    // 修复：供 CardSystem 和 PlayerController 调用
    public bool HasBarricade() => currentBarricade != null;
    public bool HasBuilding() => currentBuilding != null;

    // 获取当前状态快照，用于保存
    public GridState GetCurrentState() {
        return new GridState {
            gridId = this.gridId,
            ownerId = this.owner != null ? this.owner.playerId : -1,
            hasBarricade = this.currentBarricade != null,
            hasHouse = (this.type == GridType.Empty && this.currentBuilding != null)
        };
    }

    // 恢复状态快照
    public void ApplyState(GridState state, List<PlayerController> allPlayers, GameObject housePrefab, GameObject barricadePrefab) {
        // 1. 恢复所有权
        if (state.ownerId != -1) {
            this.owner = allPlayers.Find(p => p.playerId == state.ownerId);
            if (this.owner != null) {
                // 视觉反馈：变色
                GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f);
            }
        }

        // 2. 恢复路障
        if (state.hasBarricade && barricadePrefab != null && currentBarricade == null) {
            currentBarricade = Instantiate(barricadePrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        // 3. 恢复房屋
        if (state.hasHouse && housePrefab != null && buildingAnchor != null) {
            if (currentBuilding == null) {
                currentBuilding = Instantiate(housePrefab, buildingAnchor.position, buildingAnchor.rotation);
                currentBuilding.transform.SetParent(buildingAnchor);
            }
        }
    }

    public void ClearBarricade() {
        if (currentBarricade != null) {
            Destroy(currentBarricade);
            currentBarricade = null;
        }
    }

    public Vector3 GetSlotPosition(GameObject player) {
        if (!playersInGrid.Contains(player)) playersInGrid.Add(player);
        int index = playersInGrid.IndexOf(player);
        if (index >= 0 && index < slotPoints.Length && slotPoints[index] != null) 
            return slotPoints[index].position;
        return transform.position;
    }

    public void RemovePlayer(GameObject player) {
        if (playersInGrid.Contains(player)) playersInGrid.Remove(player);
    }

    private void OnDrawGizmos() {
        if (buildingAnchor != null) {
            Gizmos.color = (owner == null) ? Color.cyan : Color.green;
            Gizmos.DrawWireCube(buildingAnchor.position + Vector3.up * 0.75f, new Vector3(1.5f, 1.5f, 1.5f));
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, buildingAnchor.position);
        }
    }
}