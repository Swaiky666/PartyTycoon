using UnityEngine;
using UnityEngine.SceneManagement;

public class MinigameController : MonoBehaviour {

    // 这个方法绑定给小程序场景里的“退出/返回”按钮
    public void BackToMainGame() {
        Debug.Log("【系统】从小程序返回大富翁，准备加载保存的游戏状态...");
        
        // 1. 直接加载你的主场景名称（确保名称和 Build Settings 里一致）
        // GameDataManager 是 DontDestroyOnLoad，所以它存的数据不会丢
        SceneManager.LoadScene("MainGameScene"); 
    }
}