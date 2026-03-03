using UnityEngine;

[CreateAssetMenu(fileName = "New Barricade Card", menuName = "Cards/BarricadeCard")]
public class BarricadeCard : CardBase {
    
    [Header("路障设置")]
    public GameObject barricadePrefab;

    public override bool UseCard(PlayerController user, GridNode target) {
        if (target == null || target.HasBarricade()) return false;

        Debug.Log($"{user.playerId} 在地块 {target.name} 上放置了路障！");

        if (barricadePrefab != null) {
            // 加 0.3f 高度偏移防止路障埋入地面
            GameObject barricade = Instantiate(barricadePrefab, target.transform.position + Vector3.up * 0.3f, Quaternion.identity);
            target.currentBarricade = barricade;
        }

        // 注意：卡牌移除由 CardRangeFinder.ConfirmUse() 统一处理，此处不重复 Remove
        return true;
    }
}