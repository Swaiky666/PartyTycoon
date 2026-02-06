using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {
    public int playerId;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float heightOffset = 0.5f; 
    public GameObject arrowPrefab; 
    public int money = 5000;
    public List<CardBase> startingCards = new List<CardBase>();
    [HideInInspector] public List<CardBase> cards = new List<CardBase>(); 
    public GridNode currentGrid;
    private GridNode lastGrid; 
    private GridNode chosenNode = null;

    [Header("异常状态")]
    public int remainingFreezeTurns = 0; 
    private GameObject currentIceVisual;

    void Awake() {
        foreach (var c in startingCards) if (c != null) cards.Add(Instantiate(c));
    }

    // --- 冰冻逻辑 ---
    public void ApplyFreeze(int turns, GameObject prefab) {
        remainingFreezeTurns = turns;
        if (currentIceVisual == null && prefab != null) {
            currentIceVisual = Instantiate(prefab, transform.position, Quaternion.identity, transform);
        }
        // 视觉提示：变蓝
        SetModelColor(Color.cyan);
    }

    public bool CheckFreezeStatus() {
        if (remainingFreezeTurns > 0) {
            remainingFreezeTurns--;
            UIManager.Instance.UpdateStatus($"玩家 {playerId} 被冰冻，剩余 {remainingFreezeTurns} 回合");
            if (remainingFreezeTurns <= 0) Unfreeze();
            return true; // 冰冻中，跳过回合
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
        foreach (var r in rs) r.material.color = color;
    }

    // --- 移动逻辑 ---
    public void StartMoving(int steps, System.Action onComplete) {
        StartCoroutine(MoveRoutine(steps, onComplete));
    }

    private IEnumerator MoveRoutine(int steps, System.Action onComplete) {
        int remainingSteps = steps;
        while (remainingSteps > 0) {
            List<GridNode> validOptions = new List<GridNode>();
            foreach (var conn in currentGrid.connections) {
                if (conn != null && conn != lastGrid) validOptions.Add(conn);
            }
            if (validOptions.Count == 0 && lastGrid != null) validOptions.Add(lastGrid);

            GridNode nextNode = null;
            if (validOptions.Count > 1) {
                yield return StartCoroutine(WaitForBranchSelection(validOptions, (selected) => nextNode = selected));
            } else if (validOptions.Count == 1) {
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
                    remainingSteps = 0;
                    break;
                }
            } else break;
            yield return new WaitForSeconds(0.1f);
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
        Vector3 targetPos = GetPosWithHeight(targetNode.GetSlotPosition(this.gameObject));
        while (Vector3.Distance(transform.position, targetPos) > 0.01f) {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            Vector3 dir = (targetPos - transform.position).normalized;
            if (dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
    }

    public void ChangeMoney(int amount) { money += amount; UIManager.Instance.UpdatePlayerStats(this); }
    public void SetInitialPosition(GridNode node) { currentGrid = node; transform.position = GetPosWithHeight(node.GetSlotPosition(this.gameObject)); }
    public Vector3 GetPosWithHeight(Vector3 b) => new Vector3(b.x, b.y + heightOffset, b.z);
}