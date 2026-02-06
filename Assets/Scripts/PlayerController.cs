using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {
    public int playerId;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float heightOffset = 0.5f; 
    public GameObject arrowPrefab; 

    [Header("资产与默认卡牌")]
    public int money = 5000;
    public List<CardBase> startingCards = new List<CardBase>(); // Inspector中设置

    // 运行时实际持有的卡牌实例
    [HideInInspector] public List<CardBase> cards = new List<CardBase>(); 

    public GridNode currentGrid;
    private GridNode lastGrid; 
    private GridNode chosenNode = null;

    void Awake() {
        // 初始化默认卡牌
        foreach (var c in startingCards) {
            if (c != null) cards.Add(Instantiate(c));
        }
    }

    public void ChangeMoney(int amount) {
        money += amount;
        UIManager.Instance.UpdatePlayerStats(this);
    }

    public void SetInitialPosition(GridNode node) {
        currentGrid = node;
        lastGrid = null; 
        transform.position = GetPosWithHeight(node.GetSlotPosition(this.gameObject));
    }

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
            } else break;
            yield return new WaitForSeconds(0.1f);
        }
        onComplete?.Invoke();
    }

    private IEnumerator WaitForBranchSelection(List<GridNode> options, System.Action<GridNode> onSelected) {
        List<GameObject> activeArrows = new List<GameObject>();
        chosenNode = null;
        UIManager.Instance.UpdateStatus("请选择前进方向");
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

    public Vector3 GetPosWithHeight(Vector3 b) => new Vector3(b.x, b.y + heightOffset, b.z);
}