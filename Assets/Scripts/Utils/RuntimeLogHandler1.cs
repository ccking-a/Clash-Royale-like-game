using UnityEngine;
using System.IO;
using System.Text;
using System;

public class RuntimeLogHandler1 : MonoBehaviour
{
    // 日志文件路径
    private string logFilePath;
    // 日志文件夹路径
    private string logFolderPath;
    // 日志文件流
    private StreamWriter logWriter;
    // 日志缓存
    private StringBuilder logBuffer;
    // 上次写入时间
    private float lastWriteTime;
    // 写入间隔（秒）
    private const float WRITE_INTERVAL = 1.0f;

    private void Awake()
    {
        // 设置为单例，确保整个游戏只有一个日志处理器
        if (FindObjectsOfType<RuntimeLogHandler1>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        // 保持对象不被销毁
        DontDestroyOnLoad(gameObject);

        // 初始化日志系统
        InitializeLogging();

        // 注册日志回调
        Application.logMessageReceived += HandleLog;
    }

    private void InitializeLogging()
    {
        // 获取游戏根目录路径
        string gameRootPath = Application.dataPath;
        // 如果是Windows平台的构建版本，需要向上一级目录
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            gameRootPath = Path.GetDirectoryName(gameRootPath);
        }

        // 创建Logs文件夹路径
        logFolderPath = Path.Combine(gameRootPath, "Logs");
        // 创建Logs文件夹（如果不存在）
        if (!Directory.Exists(logFolderPath))
        {
            Directory.CreateDirectory(logFolderPath);
        }

        // 创建日志文件路径，使用当前日期和时间作为文件名
        string logFileName = "Log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        logFilePath = Path.Combine(logFolderPath, logFileName);

        // 初始化日志缓存
        logBuffer = new StringBuilder();

        // 尝试创建日志文件
        try
        {
            // 创建日志文件流，追加模式
            logWriter = new StreamWriter(logFilePath, true);
            // 写入日志头信息
            logWriter.WriteLine("==========================================");
            logWriter.WriteLine("游戏日志开始 - " + DateTime.Now.ToString());
            logWriter.WriteLine("游戏版本: " + Application.version);
            logWriter.WriteLine("平台: " + Application.platform.ToString());
            logWriter.WriteLine("==========================================");
            logWriter.Flush();

            Debug.Log("日志系统初始化成功，日志文件路径: " + logFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError("日志系统初始化失败: " + e.Message);
            logWriter = null;
        }

        // 初始化上次写入时间
        lastWriteTime = Time.time;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // 构建日志消息
        string logMessage = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " +
                           "[" + type.ToString() + "] " +
                           logString;

        // 如果是错误或异常，添加堆栈跟踪
        if (type == LogType.Error || type == LogType.Exception)
        {
            logMessage += "\n" + stackTrace;
        }

        // 添加到日志缓存
        logBuffer.AppendLine(logMessage);

        // 输出到控制台（仅在编辑器中）
//#if UNITY_EDITOR
//        switch (type)
//        {
//            case LogType.Log:
//                Debug.Log(logString);
//                break;
//            case LogType.Warning:
//                Debug.LogWarning(logString);
//                break;
//            case LogType.Error:
//            case LogType.Exception:
//                Debug.LogError(logString + "\n" + stackTrace);
//                break;
//            case LogType.Assert:
//                Debug.LogError(logString);
//                break;
//        }
//#endif
    }

    private void Update()
    {
        // 定期写入日志缓存
        if (Time.time - lastWriteTime >= WRITE_INTERVAL)
        {
            WriteLogBuffer();
            lastWriteTime = Time.time;
        }
    }

    private void WriteLogBuffer()
    {
        if (logWriter != null && logBuffer.Length > 0)
        {
            try
            {
                logWriter.Write(logBuffer.ToString());
                logWriter.Flush();
                logBuffer.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError("写入日志失败: " + e.Message);
            }
        }
    }

    private void OnApplicationQuit()
    {
        // 写入剩余的日志缓存
        WriteLogBuffer();

        // 写入日志结束信息
        if (logWriter != null)
        {
            try
            {
                logWriter.WriteLine("==========================================");
                logWriter.WriteLine("游戏日志结束 - " + DateTime.Now.ToString());
                logWriter.WriteLine("==========================================");
                logWriter.Flush();
                logWriter.Close();
            }
            catch (Exception e)
            {
                Debug.LogError("关闭日志文件失败: " + e.Message);
            }
        }

        // 注销日志回调
        Application.logMessageReceived -= HandleLog;
    }

    // 手动触发日志写入
    public void ForceWriteLog()
    {
        WriteLogBuffer();
    }

    // 获取日志文件夹路径
    public string GetLogFolderPath()
    {
        return logFolderPath;
    }

    // 获取当前日志文件路径
    public string GetCurrentLogFilePath()
    {
        return logFilePath;
    }
}
