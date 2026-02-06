using UnityEngine;

// 卡牌类型枚举
public enum CardType { Barricade, Tool, Buff } 

public abstract class CardBase : ScriptableObject {
    [Header("基础信息")]
    public string cardName;
    [TextArea]
    public string description;
    public CardType type;
    public Sprite cardIcon; 

    [Header("范围属性")]
    public int rangeStraight = 3;  
    public int rangeAdjacent = 3;  

    // 执行卡牌效果：返回true表示使用成功
    public abstract bool UseCard(PlayerController user, GridNode targetNode);
}