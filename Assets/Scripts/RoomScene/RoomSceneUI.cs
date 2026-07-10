using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Linq;

/// <summary>
/// RoomScene 自定义 UI：Ready 按钮、移除第二玩家按钮、玩家列表显示。
/// 实现 RoomManager 基本功能，替代默认 OnGUI。
/// </summary>
public class RoomSceneUI : MonoBehaviour
{
    [Header("UI 组件")]
    public Button readyButton;
    public Text readyButtonText;
    public Text playerListText;
    public Button removeSecondPlayerButton;

    private MyNetworkRoomManager _roomManager;
    private bool? _pendingReady; // 乐观更新：点击后立即显示，等 SyncVar 确认后清除

    void Start()
    {
        _roomManager = NetworkManager.singleton as MyNetworkRoomManager;
        if (_roomManager == null)
        {
            Debug.LogError("[RoomSceneUI] 未找到 MyNetworkRoomManager");
            return;
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
            if (readyButtonText == null) readyButtonText = readyButton.GetComponentInChildren<Text>();
        }

        if (removeSecondPlayerButton == null)
            removeSecondPlayerButton = CreateRemoveButton();
        if (removeSecondPlayerButton != null)
        {
            removeSecondPlayerButton.onClick.AddListener(OnRemoveSecondPlayerClicked);
            removeSecondPlayerButton.gameObject.SetActive(false);
        }
    }

    Button CreateRemoveButton()
    {
        if (readyButton == null) return null;
        var go = Instantiate(readyButton.gameObject, readyButton.transform.parent);
        go.name = "RemoveSecondPlayerButton";
        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y - 200);
        }
        var text = go.GetComponentInChildren<Text>();
        if (text != null) text.text = "移除玩家2";
        text.fontSize = 47;
        var btn = go.GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveAllListeners();
        return btn;
    }

    void Update()
    {
        if (_roomManager == null) return;
        if (!Utils.IsSceneActive(_roomManager.RoomScene)) return;

        RefreshPlayerList();
        RefreshReadyButton();
        RefreshRemoveButton();
    }

    void RefreshPlayerList()
    {
        if (playerListText == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("玩家列表");

        foreach (var rp in _roomManager.roomSlots.OrderBy(r => r.index))
        {
            if (rp == null) continue;
            bool ready = rp.isLocalPlayer && _pendingReady.HasValue ? _pendingReady.Value : rp.readyToBegin;
            string status = ready ? "已准备" : "未准备";
            string hostTag = (rp.index == 0 && NetworkServer.active) ? " [主机]" : "";
            sb.AppendLine($"  {rp.index + 1}. {status}{hostTag}");
        }

        if (_roomManager.roomSlots.Count == 0)
            sb.AppendLine("  (等待玩家加入)");

        playerListText.text = sb.ToString();
    }

    void RefreshReadyButton()
    {
        if (readyButton == null || readyButtonText == null) return;

        var localRoomPlayer = GetLocalRoomPlayer();
        readyButton.interactable = localRoomPlayer != null;

        if (localRoomPlayer != null)
        {
            if (_pendingReady.HasValue && localRoomPlayer.readyToBegin == _pendingReady.Value)
                _pendingReady = null; // SyncVar 已同步，清除乐观状态
            bool ready = _pendingReady ?? localRoomPlayer.readyToBegin;
            readyButtonText.text = ready ? "取消准备" : "准备";
        }
    }

    void RefreshRemoveButton()
    {
        if (removeSecondPlayerButton == null) return;

        bool showRemove = NetworkServer.active && _roomManager.roomSlots.Count >= 2;
        removeSecondPlayerButton.gameObject.SetActive(showRemove);
    }

    void OnReadyClicked()
    {
        var localRoomPlayer = GetLocalRoomPlayer();
        if (localRoomPlayer == null) return;

        bool newReady = !localRoomPlayer.readyToBegin;
        _pendingReady = newReady; // 乐观更新，立即显示
        localRoomPlayer.CmdChangeReadyState(newReady);
    }

    void OnRemoveSecondPlayerClicked()
    {
        if (!NetworkServer.active) return;

        var secondPlayer = _roomManager.roomSlots.FirstOrDefault(rp => rp != null && rp.index == 1);
        if (secondPlayer == null) return;

        var identity = secondPlayer.GetComponent<NetworkIdentity>();
        if (identity != null && identity.connectionToClient != null)
        {
            identity.connectionToClient.Disconnect();
            Debug.Log("[RoomSceneUI] 已移除第二玩家");
        }
    }

    NetworkRoomPlayer GetLocalRoomPlayer()
    {
        if (_roomManager == null) return null;
        return _roomManager.roomSlots.FirstOrDefault(rp => rp != null && rp.isLocalPlayer);
    }
}
