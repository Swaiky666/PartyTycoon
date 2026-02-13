using UnityEngine;

public enum CardType { Barricade, Tool, Buff } 

public abstract class CardBase : ScriptableObject {
    [Header("基础信息")]
    public string cardName;
    [TextArea]
    public string description;
    public CardType type;
    public Sprite cardIcon; 

    [Header("经济属性")]
    public int price = 100; // 在 Inspector 中设置价格

    [Header("范围属性")]
    public int rangeStraight = 3;  
    public int rangeAdjacent = 3;  

    public abstract bool UseCard(PlayerController user, GridNode targetNode);
}