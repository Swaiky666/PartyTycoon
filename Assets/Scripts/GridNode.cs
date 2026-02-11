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

    [Header("建筑配置")]
    public Transform buildingAnchor;    
    public GameObject currentBuilding;   

    [Header("运行时状态")]
    public GameObject currentBarricade; 
    public GridNode[] connections = new GridNode[4]; 
    public Transform[] slotPoints = new Transform[6];
    private List<GameObject> playersInGrid = new List<GameObject>();

    // 修复：添加被 CardSystem 和 PlayerController 引用的方法
    public bool HasBarricade() => currentBarricade != null;
    public bool HasBuilding() => currentBuilding != null;

    public GridState GetCurrentState() {
        return new GridState {
            gridId = this.gridId,
            ownerId = this.owner != null ? this.owner.playerId : -1,
            hasBarricade = this.currentBarricade != null,
            hasHouse = (this.type == GridType.Empty && this.currentBuilding != null)
        };
    }

    public void ApplyState(GridState state, List<PlayerController> allPlayers, GameObject housePrefab, GameObject barricadePrefab) {
        if (state.ownerId != -1) {
            this.owner = allPlayers.Find(p => p.playerId == state.ownerId);
            if (this.owner != null) GetComponent<Renderer>().material.color = new Color(0.6f, 1f, 0.6f);
        } else {
            this.owner = null;
            GetComponent<Renderer>().material.color = Color.white;
        }

        if (state.hasBarricade) {
            if (currentBarricade == null && barricadePrefab != null) {
                currentBarricade = Instantiate(barricadePrefab, transform.position, Quaternion.identity);
            }
        } else if (currentBarricade != null) {
            Destroy(currentBarricade);
            currentBarricade = null;
        }

        if (state.hasHouse) {
            if (currentBuilding == null && housePrefab != null) {
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
        Gizmos.color = Color.blue;
        for (int i = 0; i < 4; i++) {
            if (connections[i] != null) Gizmos.DrawLine(transform.position, connections[i].transform.position);
        }
    }
}