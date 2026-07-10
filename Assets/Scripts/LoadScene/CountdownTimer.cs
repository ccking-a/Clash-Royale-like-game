using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Mirror;

public class CountdownTimer : NetworkBehaviour
{
    [SerializeField] private Text countdownText;
    [SerializeField] private GameObject gameUI;
    [SerializeField] private GameObject countdownUI;
    private CostControler costControler;
    private UIManager uimanager;
    private bool gameStartedLocally = false;

    public override void OnStartClient()
    {
        if (!isLocalPlayer) return;
        BindUI();
        if (gameUI != null) gameUI.SetActive(false);
        if (countdownUI != null) countdownUI.SetActive(true);
    }

    private void BindUI()
    {
        gameUI = GameObject.FindWithTag("Canva");
        countdownUI = GameObject.FindWithTag("Canvacold");
        countdownText = GameObject.Find("Textcoldtime")?.GetComponent<Text>();
        costControler = GetComponent<CostControler>();
        uimanager = GetComponent<UIManager>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (gameStartedLocally) return;

        var gs = NetworkGameState.Instance;
        if (gs == null) return;

        if (gs.isGameStarted)
        {
            gameStartedLocally = true;
            StartGame();
            return;
        }

        if (gs.gameCountdown >= 0f)
        {
            if (countdownText != null)
            {
                if (gs.gameCountdown > 1f)
                    countdownText.text = Mathf.CeilToInt(gs.gameCountdown).ToString();
                else if (gs.gameCountdown > 0f)
                    countdownText.text = "GO!";
            }
        }
        else
        {
            if (countdownText != null)
                countdownText.text = "等待对手...";
        }
    }

    void StartGame()
    {
        if (countdownText != null) countdownText.text = "";
        if (countdownUI != null) countdownUI.SetActive(false);
        if (gameUI != null) gameUI.SetActive(true);
        if (costControler != null)
        {
            costControler.PlaySecondMusic();
            costControler.BindUIComponents();
        }
        if (uimanager != null) uimanager.GameStart();
        Debug.Log("游戏开始！");
    }
}