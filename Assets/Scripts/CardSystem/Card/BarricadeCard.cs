using UnityEngine;

[CreateAssetMenu(fileName = "New Barricade Card", menuName = "Cards/BarricadeCard")]
public class BarricadeCard : CardBase {
    
    [Header("路障设置")]
    public GameObject barricadePrefab;

    public override bool UseCard(PlayerController user, GridNode target) {
        if (target == null || target.HasBarricade()) return false;

        Debug.Log($"{user.playerId} 在地块 {target.name} 上放置了路障！");

        if (barricadePrefab != null) {
            GameObject barricade = Instantiate(barricadePrefab, target.transform.position, Quaternion.identity);
            // 关键：建立地块与路障的关联
            target.currentBarricade = barricade;
        }

        user.cards.Remove(this);
        return true;
    }
}