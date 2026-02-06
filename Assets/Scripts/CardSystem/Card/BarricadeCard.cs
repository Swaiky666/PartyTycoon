using UnityEngine;

// 确保该类继承自你的卡牌基类 CardBase
[CreateAssetMenu(fileName = "New Barricade Card", menuName = "Cards/BarricadeCard")]
public class BarricadeCard : CardBase {
    
    [Header("路障设置")]
    public GameObject barricadePrefab; // 在 Inspector 中拖入你的路障模型预制体

    // 重写使用逻辑
    public override bool UseCard(PlayerController user, GridNode target) {
        if (target == null) return false;

        // 检查地块是否已经有路障或其他障碍物
        // 这里假设你的 GridNode 有个方法或属性记录当前地块上的物体
        Debug.Log($"{user.playerId} 在地块 {target.name} 上放置了路障！");

        // 实例化路障
        if (barricadePrefab != null) {
            GameObject barricade = Instantiate(barricadePrefab, target.transform.position, Quaternion.identity);
            // 这里可以给 target 节点设置一个状态，比如 target.hasBarricade = true;
            // 这样玩家移动经过时就能检测到并停止
        }

        // 消耗金币（可选，如果你在基类没处理的话）
        // user.ChangeMoney(-this.cost);

        // 成功使用后从玩家手牌移除
        user.cards.Remove(this);
        
        return true;
    }
}