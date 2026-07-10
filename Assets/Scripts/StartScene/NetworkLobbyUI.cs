using UnityEngine;
using UnityEngine.UI;
using Mirror;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;

public class NetworkLobbyUI : MonoBehaviour
{
    [Header("UI组件")]
    public Button hostButton;
    public Button clientButton;
    public Button SelectButton;
    public InputField ipInputField;
    public InputField portInputField;
    public Text statusText;
    public Canvas lobbyCanvas;
    public Canvas SelectCanvas;

    [Header("网络设置")]
    public string gameSceneName = "GameScene";
    public int defaultPort = 7777;

    private MyNetworkRoomManager networkManager;

    private MyNetworkRoomManager GetManager()
    {
        if (networkManager == null)
            networkManager = NetworkManager.singleton as MyNetworkRoomManager;
        return networkManager;
    }

    private void Start()
    {


        GetManager();
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
        }

        ipInputField.text = "";
        ipInputField.placeholder.GetComponent<Text>().text = "输入Host的局域网IP";
        portInputField.text = defaultPort.ToString();
        statusText.text = $"本机IP: {GetLocalIPAddress()}";

        // 添加按钮点击事件
        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        SelectButton.onClick.AddListener(OnSelectButtonClicked);


        Debug.Log("=== 客户端预制体列表诊断 (启动时) ===");
        var nm = NetworkManager.singleton;
        if (nm != null)
        {
            Debug.Log($"客户端 SpawnPrefabs 数量: {nm.spawnPrefabs.Count}");
            for (int i = 0; i < nm.spawnPrefabs.Count; i++)
            {
                var prefab = nm.spawnPrefabs[i];
                if (prefab != null)
                {
                    var netId = prefab.GetComponent<NetworkIdentity>();
                    if (netId != null)
                    {
                        Debug.Log($"  [{i}] {prefab.name} -> AssetId: {netId.assetId}");
                        // 如果这里能打印出 AssetId=3767952548，说明列表里有，但可能版本不对
                    }
                }
            }
        }
    }

    private void Update()
    {
        GetManager();

        bool canStart = UnitSelectionData.HasValidSelection;
        if (hostButton != null) hostButton.interactable = canStart;
        if (clientButton != null) clientButton.interactable = canStart;
    }

    // 启动服务器
    public void OnHostButtonClicked()
    {
        UnitSelectionData.Save();
        string localIP = GetLocalIPAddress();
        statusText.text = $"正在启动服务器... 本机IP: {localIP}";
        Debug.Log($"[Host] 局域网IP: {localIP}, 端口: {defaultPort}");

        if (GetManager() == null)
        {
            statusText.text = "错误：未找到NetworkManager组件！";
            Debug.LogError("NetworkManager not found in scene!");
            return;
        }

        networkManager.networkAddress = "localhost";

        StartCoroutine(StartHostAfterSceneLoad());
    }

    IEnumerator StartHostAfterSceneLoad()
    {
        SceneManager.LoadScene("RoomScene");

        // 启动服务器
        networkManager.StartHost();
        Debug.LogError("host启动");
        yield return null;
    }

    // 加入客户端
    public void OnClientButtonClicked()
    {
        UnitSelectionData.Save();
        statusText.text = "正在连接服务器...";

        if (GetManager() == null)
        {
            statusText.text = "错误：未找到NetworkManager组件！";
            Debug.LogError("NetworkManager not found in scene!");
            return;
        }

        // 设置IP地址
        string ip = ipInputField.text;
        networkManager.networkAddress = ip;

        Debug.Log("=== 客户端启动序列开始 ===");

        if (NetworkManager.singleton == null)
        {
            Debug.LogError("NetworkManager.singleton 为空！");
            return;
        }

        // ==== 核心修复：对输入的地址进行严格处理 ====
        string rawInput = ipInputField?.text;
        Debug.Log($"原始输入: '{rawInput}'");

        // 1. 去除首尾空格
        string cleanedInput = string.IsNullOrEmpty(rawInput) ? "" : rawInput.Trim();

        // 2. 检查是否为空或仅包含空白字符
        if (string.IsNullOrWhiteSpace(cleanedInput))
        {
            Debug.LogWarning("输入为空，使用默认地址 'localhost'");
            cleanedInput = "localhost";
        }
        else
        {
            // 3. 额外安全措施：只允许字母、数字、点(.)、连字符(-)和冒号(:)（用于端口）
            // 这可以过滤掉绝大部分非法字符
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"[^a-zA-Z0-9\.\:\-]");
            if (regex.IsMatch(cleanedInput))
            {
                Debug.LogWarning($"输入 '{cleanedInput}' 包含非常规字符，已过滤。");
                cleanedInput = regex.Replace(cleanedInput, "");
            }
        }

        // 设置最终要连接的地址
        NetworkManager.singleton.networkAddress = cleanedInput;
        Debug.Log($"将尝试连接到: {NetworkManager.singleton.networkAddress}");

        // ==== 原有网络状态检查和启动逻辑 ====
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("网络已活跃，忽略重复启动。");
            return;
        }

        try
        {
            StartCoroutine(StartClientAfterSceneLoad());
            Debug.Log("StartClient() 调用完成。");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"启动客户端时异常: {e}");
        }

    }

    public void OnSelectButtonClicked()
    {
        if (lobbyCanvas != null && SelectCanvas != null)
        {
            lobbyCanvas.gameObject.SetActive(false);
            SelectCanvas.gameObject.SetActive(true);
        }
    }
    IEnumerator StartClientAfterSceneLoad()
    {
        SceneManager.LoadScene("RoomScene");
        NetworkManager.singleton.StartClient();
        Debug.LogError("client启动");
        yield return null;
    }



    private string GetLocalIPAddress()
    {
        // 方法1：用 UDP Socket 探测实际出口 IP（最可靠）
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 80);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                if (endPoint != null)
                    return endPoint.Address.ToString();
            }
        }
        catch (Exception) { }

        // 方法2：遍历 DNS 记录
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch (Exception) { }

        // 方法3：遍历网卡
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        return addr.Address.ToString();
                }
            }
        }
        catch (Exception) { }

        return "未知(请在cmd输入ipconfig查看)";
    }

    private System.Collections.IEnumerator CheckConnectionStatus()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // 检查客户端是否连接（使用Mirror正确的属性）
            if (NetworkClient.isConnected)
            {
                statusText.text = "连接服务器成功！";
                SceneManager.LoadScene(gameSceneName);
                yield break;
            }

            yield return null;
        }

        statusText.text = "连接超时，请检查服务器是否开启";
    }
}