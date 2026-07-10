using UnityEngine;
using UnityEngine.SceneManagement; // 引入场景管理命名空间
using UnityEngine.UI; // 如果按钮引用需要，则引入UI

public class SceneController : MonoBehaviour
{
    // 通过场景在Build Settings中的索引跳转
    // 例如，开始菜单是0，主游戏是1
    public void LoadGameSceneByIndex()
    {
        SceneManager.LoadScene(1);
    }
}