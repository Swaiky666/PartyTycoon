using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour {
    [Header("身份设置")]
    public int playerId;
    
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float heightOffset = 0.5f; // 根据你的地板厚度调整此值

    [Header("当前状态")]
    public GridNode currentGrid;
    private bool isMoving = false;

    // 辅助方法：统一计算带高度的位置
    public Vector3 GetPosWithHeight(Vector3 basePos) {
        return new Vector3(basePos.x, basePos.y + heightOffset, basePos.z);
    }

    // 初始化位置（在 GameStartManager 实例化时调用）
    public void SetInitialPosition(GridNode node) {
        currentGrid = node;
        // 获取 Slot 位置后立即叠加高度偏移
        Vector3 slotPos = node.GetSlotPosition(this.gameObject);
        transform.position = GetPosWithHeight(slotPos);
    }

    // 由 TurnManager 调用开启移动
    public void StartMoving(int steps, System.Action onComplete) {
        StartCoroutine(MoveRoutine(steps, onComplete));
    }

    private IEnumerator MoveRoutine(int steps, System.Action onComplete) {
        int remainingSteps = steps;
        while (remainingSteps > 0) {
            isMoving = true;
            GridNode nextNode = DetermineNextNode();

            if (nextNode == null) {
                Debug.Log("前面没路了！");
                break;
            }

            yield return StartCoroutine(MoveToNode(nextNode));

            currentGrid = nextNode;
            remainingSteps--;
            yield return new WaitForSeconds(0.1f);
        }
        isMoving = false;
        onComplete?.Invoke();
    }

    private IEnumerator MoveToNode(GridNode targetNode) {
        Vector3 targetPos = GetPosWithHeight(targetNode.GetSlotPosition(this.gameObject));
        
        while (Vector3.Distance(transform.position, targetPos) > 0.01f) {
            // 位移
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            
            // 转向
            Vector3 dir = (targetPos - transform.position).normalized;
            if (dir != Vector3.zero) {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
            yield return null;
        }
        transform.position = targetPos;
    }

    private GridNode DetermineNextNode() {
        if (currentGrid == null) return null;
        // 简单逻辑：取第一个非空连接。未来这里会加入分叉路口判断
        foreach (var node in currentGrid.connections) {
            if (node != null) return node;
        }
        return null;
    }
}