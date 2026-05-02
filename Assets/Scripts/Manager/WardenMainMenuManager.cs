using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 主選單：僅負責開始遊戲（載入場景）與離開遊戲。
/// </summary>
public class WardenMainMenuManager : MonoBehaviour
{
    [Header("場景")]
    [Tooltip("非空則優先依場景名稱載入（須出現在 Build Settings），例如 SampleScene。")]
    [SerializeField]
    private string gameSceneNameOverride = "";

    [Tooltip("當 gameSceneNameOverride 為空時使用：Build Settings／Build Profile 中的建置索引（0 為清單第一個場景）。")]
    [SerializeField]
    private int gameSceneIndex = 1;

    /// <summary>
    /// 載入遊戲場景：同幀呼叫 <see cref="SceneManager.LoadSceneAsync"/> 並啟用 <c>allowSceneActivation</c>；
    /// 優先使用 <see cref="gameSceneNameOverride"/>，否則使用 <see cref="gameSceneIndex"/>。
    /// </summary>
    public void StartGame()
    {
        Debug.Log("[Menu] Loading game scene…");

        AsyncOperation op = null;
        if (!string.IsNullOrWhiteSpace(gameSceneNameOverride))
        {
            string sceneName = gameSceneNameOverride.Trim();
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[Menu] 場景 \"" + sceneName + "\" 不在 Build Settings 或名稱不符，無法載入。請檢查 File → Build Profiles / Build Settings。");
                return;
            }

            var active = SceneManager.GetActiveScene();
            if (active.name == sceneName)
            {
                Debug.LogError("[Menu] 已在場景 \"" + sceneName + "\"，請勿重複載入；或清空 gameSceneNameOverride 並確認索引。");
                return;
            }

            op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        else
        {
            if (gameSceneIndex < 0 || gameSceneIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError("[Menu] gameSceneIndex 超出建置清單範圍: " + gameSceneIndex + "（共 " + SceneManager.sceneCountInBuildSettings + " 個場景）。");
                return;
            }

            int activeIdx = SceneManager.GetActiveScene().buildIndex;
            if (gameSceneIndex == activeIdx)
            {
                Debug.LogError("[Menu] gameSceneIndex (" + gameSceneIndex + ") 與目前作用中場景相同，不會切換。請改為遊戲場景索引或填寫 gameSceneNameOverride（例如 SampleScene）。");
                return;
            }

            op = SceneManager.LoadSceneAsync(gameSceneIndex, LoadSceneMode.Single);
        }

        if (op != null)
            op.allowSceneActivation = true;
    }

    /// <summary>
    /// 離開遊戲：正式版呼叫 <see cref="Application.Quit"/>；在 Editor 則停止 Play 模式。
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gameSceneIndex = Mathf.Max(0, gameSceneIndex);
    }
#endif
}
