using UnityEngine;

[CreateAssetMenu(fileName = "New Freeze Card", menuName = "Cards/FreezeCard")]
public class FreezeCard : CardBase {
    [Header("冰冻设置")]
    public int freezeTurns = 2;
    public GameObject iceEffectPrefab;

    public override bool UseCard(PlayerController user, GridNode target) {
        if (target == null) return false;

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        bool hit = false;

        foreach (var p in allPlayers) {
            if (p.currentGrid == target && p != user) {
                p.ApplyFreeze(freezeTurns, iceEffectPrefab);
                hit = true;
            }
        }

        if (!hit) {
            UIManager.Instance.UpdateStatus("该格没有可冻结的玩家！");
        }
        // 注意：卡牌移除由 CardRangeFinder.ConfirmUse() 统一处理，此处不重复 Remove
        return hit;
    }
}