using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {
    public int playerId;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float heightOffset = 0.5f; 
    public GameObject arrowPrefab; 

    public GridNode currentGrid;
    private GridNode lastGrid; 
    private GridNode chosenNode = null;

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
                // 进入分叉路口选择
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

        // 新增：在 UI 上显示选择提醒
        if (UIManager.Instance != null) {
            UIManager.Instance.UpdateStatus("请点击箭头选择前进方向");
        }

        foreach (var node in options) {
            Vector3 diff = node.transform.position - currentGrid.transform.position;
            Quaternion fixedRotation = Quaternion.identity;
            Vector3 offset = Vector3.zero;
            float dist = 1.3f;

            // 固定 X/Z 轴朝向逻辑
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z)) {
                if (diff.x > 0) { fixedRotation = Quaternion.Euler(0, 90, 0); offset = new Vector3(dist, 0, 0); }
                else { fixedRotation = Quaternion.Euler(0, -90, 0); offset = new Vector3(-dist, 0, 0); }
            } else {
                if (diff.z > 0) { fixedRotation = Quaternion.Euler(0, 0, 0); offset = new Vector3(0, 0, dist); }
                else { fixedRotation = Quaternion.Euler(0, 180, 0); offset = new Vector3(0, 0, -dist); }
            }

            Vector3 spawnPos = currentGrid.transform.position + Vector3.up * 0.7f + offset;
            GameObject arrow = Instantiate(arrowPrefab, spawnPos, fixedRotation);
            arrow.transform.Rotate(90, 0, 0, Space.Self); 

            ArrowClicker clicker = arrow.GetComponent<ArrowClicker>() ?? arrow.AddComponent<ArrowClicker>();
            clicker.Setup(node, (target) => chosenNode = target);
            activeArrows.Add(arrow);
        }

        while (chosenNode == null) yield return null;
        
        foreach (var arrow in activeArrows) Destroy(arrow);
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

    public Vector3 GetPosWithHeight(Vector3 basePos) { return new Vector3(basePos.x, basePos.y + heightOffset, basePos.z); }
}