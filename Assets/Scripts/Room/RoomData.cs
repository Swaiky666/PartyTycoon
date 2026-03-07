public enum SlotState { Empty, Human, AI }

[System.Serializable]
public class SlotData {
    public int    slotIndex;
    public SlotState state    = SlotState.Empty;
    public int    playerId    = -1;
    public string playerName  = "";
    public int    colorIndex  = 0;
    public bool   isReady     = false;
    public bool   isHost      = false;

    public SlotData(int index) {
        slotIndex  = index;
        playerName = "玩家" + (index + 1);
        colorIndex = index;
    }

    public SlotData Clone() => new SlotData(slotIndex) {
        state      = state,
        playerId   = playerId,
        playerName = playerName,
        colorIndex = colorIndex,
        isReady    = isReady,
        isHost     = isHost,
    };
}
