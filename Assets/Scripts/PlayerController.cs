using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {
    public int playerId;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    
    [Tooltip("角色相对于地块插槽的高度偏移，支持负数以允许角色下沉。")]
    public float heightOffset = 0.5f; 
    
    public GameObject arrowPrefab; 
    public int money = 5000;
    public List<CardBase> startingCards = new List<CardBase>();
    [HideInInspector] public List<CardBase> cards = new List<CardBase>(); 
    
    public GridNode currentGrid;
    private GridNode lastGrid; // 内部记录从哪来的，用于禁止回退
    private GridNode chosenNode = null;

    [Header("异常状态")]
    public int remainingFreezeTurns = 0; 
    private GameObject currentIceVisual;

    void Awake() {
        // 初始化卡牌
        foreach (var c in startingCards) {
            if (c != null) cards.Add(Instantiate(c));
        }
    }

    // --- 存档系统专用接口 ---
    public GridNode GetLastGrid() => lastGrid;
    public void SetLastGrid(GridNode node) => lastGrid = node;

    // --- 冰冻逻辑 (保留并补全遗漏的 Unfreeze) ---
    public void ApplyFreeze(int turns, GameObject prefab) {
        remainingFreezeTurns = turns;
        if (currentIceVisual == null && prefab != null) {
            currentIceVisual = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            currentIceVisual.transform.SetParent(this.transform);
        }
        SetModelColor(Color.cyan);
    }

    public bool CheckFreezeStatus() {
        if (remainingFreezeTurns > 0) {
            remainingFreezeTurns--;
            UIManager.Instance.UpdateStatus($"玩家 {playerId} 被冰冻，剩余 {remainingFreezeTurns} 回合");
            if (remainingFreezeTurns <= 0) {
                Unfreeze();
            }
            return true; 
        }
        return false;
    }

    public void Unfreeze() {
        remainingFreezeTurns = 0;
        if (currentIceVisual != null) Destroy(currentIceVisual);
        SetModelColor(Color.white);
    }

    private void SetModelColor(Color color) {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        foreach (var r in rs) if (r != null) r.material.color = color;
    }

    // --- 定位逻辑 (恢复了 Rigidbody 物理保护逻辑) ---
    public void SetInitialPosition(GridNode node) {
        if (node == null) return;
        currentGrid = node;
        StopAllCoroutines();

        // 支持负 Offset 的位置计算
        Vector3 slotPos = node.GetSlotPosition(this.gameObject);
        Vector3 finalPos = new Vector3(slotPos.x, slotPos.y + heightOffset, slotPos.z);
        
        transform.position = finalPos;
        
        // 物理状态重置：防止切场景或传送时产生物理冲力
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
            StartCoroutine(RestorePhysics(rb));
        }

        Physics.SyncTransforms();
        Debug.Log($"【系统】玩家 {playerId} 已定位至地块 {node.gridId} (Offset: {heightOffset})");
    }

    private IEnumerator RestorePhysics(Rigidbody rb) {
        yield return new WaitForFixedUpdate();
        if (rb != null) rb.isKinematic = false;
    }

    // --- 移动逻辑 (整合了防回退、路障、及分叉路选择) ---
    public void StartMoving(int steps, System.Action onComplete) {
        StartCoroutine(MoveRoutine(steps, onComplete));
    }

    private IEnumerator MoveRoutine(int steps, System.Action onComplete) {
        if (currentGrid == null) {
            onComplete?.Invoke();
            yield break;
        }

        int remainingSteps = steps;
        while (remainingSteps > 0) {
            List<GridNode> validOptions = new List<GridNode>();
            
            // 过滤掉刚才走过来的格子，实现“禁止往回走”
            foreach (var conn in currentGrid.connections) {
                if (conn != null && conn != lastGrid) {
                    validOptions.Add(conn);
                }
            }

            // 如果走到了死胡同（除了来路没别的连接了），才允许回头
            if (validOptions.Count == 0 && lastGrid != null) {
                validOptions.Add(lastGrid);
            }

            GridNode nextNode = null;
            if (validOptions.Count > 1) {
                // 有多条路，显示箭头供玩家选择
                yield return StartCoroutine(WaitForBranchSelection(validOptions, (selected) => nextNode = selected));
            } else if (validOptions.Count == 1) {
                // 只有一条路，直接走
                nextNode = validOptions[0];
            }

            if (nextNode != null) {
                lastGrid = currentGrid; 
                yield return StartCoroutine(MoveToNode(nextNode));
                currentGrid = nextNode;
                remainingSteps--;

                // 路障检测
                if (currentGrid.HasBarricade()) {
                    currentGrid.ClearBarricade();
                    UIManager.Instance.UpdateStatus("撞到路障！停止移动。");
                    break;
                }
            } else {
                break;
            }
            yield return new WaitForSeconds(0.05f);
        }
        onComplete?.Invoke();
    }

    private IEnumerator WaitForBranchSelection(List<GridNode> options, System.Action<GridNode> onSelected) {
        List<GameObject> activeArrows = new List<GameObject>();
        chosenNode = null;
        
        foreach (var node in options) {
            Vector3 diff = node.transform.position - currentGrid.transform.position;
            Quaternion rot = Quaternion.identity;
            Vector3 offset = Vector3.zero;
            
            // 根据地块相对位置计算箭头的朝向和偏移
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z)) {
                if (diff.x > 0) { rot = Quaternion.Euler(0, 90, 0); offset = new Vector3(1.3f, 0, 0); }
                else { rot = Quaternion.Euler(0, -90, 0); offset = new Vector3(-1.3f, 0, 0); }
            } else {
                if (diff.z > 0) { rot = Quaternion.Euler(0, 0, 0); offset = new Vector3(0, 0, 1.3f); }
                else { rot = Quaternion.Euler(0, 180, 0); offset = new Vector3(0, 0, -1.3f); }
            }

            GameObject arrow = Instantiate(arrowPrefab, currentGrid.transform.position + Vector3.up * 0.7f + offset, rot);
            arrow.transform.Rotate(90, 0, 0); 
            arrow.GetComponent<ArrowClicker>().Setup(node, (t) => chosenNode = t);
            activeArrows.Add(arrow);
        }

        while (chosenNode == null) yield return null;

        foreach (var a in activeArrows) Destroy(a);
        onSelected?.Invoke(chosenNode);
    }

    private IEnumerator MoveToNode(GridNode targetNode) {
        Vector3 targetPos = targetNode.GetSlotPosition(this.gameObject);
        targetPos.y += heightOffset;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f) {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            
            // 平滑转向目标地块
            Vector3 dir = (targetPos - transform.position).normalized;
            if (dir != Vector3.zero) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            }
            yield return null;
        }
        transform.position = targetPos;
    }

    public void ChangeMoney(int amount) { 
        money += amount; 
        UIManager.Instance.UpdatePlayerStats(this); 
    }
}