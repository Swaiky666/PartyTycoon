using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneReturnTester : MonoBehaviour {
    public Button returnButton;
    public string mainSceneName = "MainBoardScene"; // 你的主地图场景名

    void Start() {
        if (returnButton != null) {
            returnButton.onClick.AddListener(() => {
                // 直接切回主场景，GameStartManager 会自动处理恢复逻辑
                SceneManager.LoadScene(mainSceneName);
            });
        }
    }
}