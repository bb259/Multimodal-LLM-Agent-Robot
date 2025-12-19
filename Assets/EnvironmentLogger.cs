using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class EnvironmentLogger : MonoBehaviour
{
    [Header("扫描设置")]
    [SerializeField] private float scanInterval = 2f; // 扫描间隔（秒）
    [SerializeField] private float scanRadius = 1f; // 扫描半径
    [SerializeField] private LayerMask scanLayerMask = ~0; // 扫描层
    [SerializeField] private string[] scanTags; // 扫描标签过滤
    
    [Header("日志设置")]
    [SerializeField] private bool enableLogging = true; // 是否启用日志
    [SerializeField] private string logFileName = "environment_log.txt"; // 日志文件名
    [SerializeField] private bool useCustomPath = false; // 是否使用自定义路径
    [SerializeField] private string customLogPath = ""; // 自定义日志路径
    [SerializeField] private bool appendToFile = true; // 是否追加到文件
    [SerializeField] private bool includeTimestamp = true; // 是否包含时间戳
    
    [Header("调试")]
    [SerializeField] private bool showDebugInfo = true; // 是否显示调试信息
    
    private Transform scanOrigin;
    private string logFilePath;
    private Coroutine scanCoroutine;
    
    // 存储最新的扫描结果，供其他组件使用
    private string latestEnvironmentInfo = "";
    private Vector3 latestScanPosition;
    
    /// <summary>
    /// 获取最新的环境扫描结果
    /// </summary>
    public string GetLatestEnvironmentInfo()
    {
        return latestEnvironmentInfo;
    }
    
    /// <summary>
    /// 获取最近扫描时的位置
    /// </summary>
    public Vector3 GetLatestScanPosition()
    {
        return latestScanPosition;
    }
    
    /// <summary>
    /// 感知半径范围内带标签物体的位置信息
    /// </summary>
    /// <param name="radius">感知半径（米），默认使用组件设置的扫描半径</param>
    /// <param name="tagsFilter">标签过滤器，如果为null则使用组件设置的标签过滤，如果为空数组则检测所有带标签的物体</param>
    /// <returns>范围内带标签物体的位置信息列表</returns>
    public System.Collections.Generic.List<EnvironmentScanner.PerceivedObject> PerceiveTaggedObjects(float? radius = null, string[] tagsFilter = null)
    {
        Transform scanTransform = scanOrigin != null ? scanOrigin : transform;
        float useRadius = radius ?? scanRadius;
        string[] useTags = tagsFilter ?? scanTags;
        
        return EnvironmentScanner.PerceiveTaggedObjects(scanTransform, useRadius, scanLayerMask, useTags);
    }
    
    private void Awake()
    {
        //Debug.Log("=== EnvironmentLogger Awake() 方法被调用 ===");
        //Debug.Log($"enableLogging = {enableLogging}");
        //Debug.Log($"scanInterval = {scanInterval}");
        //Debug.Log($"scanRadius = {scanRadius}");
        //Debug.Log($"logFileName = {logFileName}");
    }
    
    private void Start()
    {
        //Debug.Log("=== EnvironmentLogger Start() 方法被调用 ===");
        
        // 设置扫描原点
        scanOrigin = transform;
        //Debug.Log($"扫描原点设置为: {scanOrigin.name} at {scanOrigin.position}");
        
        // 使用指定的默认路径
        string defaultLogDirectory = @"D:\file\shixi\chanping\AI\log";
        logFilePath = Path.Combine(defaultLogDirectory, logFileName);
        //Debug.Log($"使用默认路径: {logFilePath}");
        
        // 初始化日志文件
        InitializeLogFile();
        
        // 开始定期扫描
        if (enableLogging)
        {
            //Debug.Log($"环境感知日志系统已启动");
            //Debug.Log($"扫描间隔: {scanInterval}秒（每{scanInterval}秒记录一次）");
            //Debug.Log($"扫描半径: {scanRadius}米");
            //Debug.Log($"日志文件路径: {logFilePath}");
            StartPeriodicScanning();
        }
        else
        {
            //Debug.LogWarning("日志功能已禁用！请启用 enableLogging 以开始记录");
        }
    }
    
    private void InitializeLogFile()
    {
        //Debug.Log($"初始化日志文件: {logFilePath}");
        
        // 确保目录存在
        string directory = Path.GetDirectoryName(logFilePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            //Debug.Log($"创建日志目录: {directory}");
        }
        
        if (!appendToFile && File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
            //Debug.Log("删除现有日志文件");
        }
        
        // 写入文件头
        if (!File.Exists(logFilePath))
        {
            WriteToLog("=== 环境扫描日志开始 ===");
            //Debug.Log("创建新的日志文件");
        }
        else
        {
            //Debug.Log("使用现有日志文件");
        }
    }
    
    private void StartPeriodicScanning()
    {
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
        }
        
        scanCoroutine = StartCoroutine(PeriodicScanCoroutine());
    }
    
    private IEnumerator PeriodicScanCoroutine()
    {
        // 启动时立即执行一次扫描
        if (enableLogging)
        {
            if (showDebugInfo)
            {
                //Debug.Log($"开始首次环境感知记录...");
            }
            ScanAndLogEnvironment();
        }
        
        // 然后每间隔时间执行一次
        while (enableLogging)
        {
            yield return new WaitForSeconds(scanInterval);
            
            if (enableLogging)
            {
                if (showDebugInfo)
                {
                    //Debug.Log($"执行定期环境感知记录（间隔{scanInterval}秒）...");
                }
                ScanAndLogEnvironment();
            }
        }
    }
    
    private void ScanAndLogEnvironment()
    {
        // 使用当前游戏对象的位置作为扫描原点（确保使用实时位置）
        Transform currentTransform = scanOrigin != null ? scanOrigin : transform;
        if (currentTransform == null) return;
        
        // 使用新的感知方法获取带标签物体的位置信息
        var perceivedObjects = EnvironmentScanner.PerceiveTaggedObjects(
            currentTransform,
            scanRadius,
            scanLayerMask,
            scanTags
        );
        
        // 同时保留旧的摘要格式用于兼容
        string environmentInfo = EnvironmentScanner.BuildEnvironmentSummary(
            currentTransform,
            scanRadius,
            scanLayerMask,
            scanTags,
            20 // 最大项目数
        );
        
        // 存储最新的扫描结果
        latestEnvironmentInfo = environmentInfo;
        latestScanPosition = currentTransform.position;
        
        // 构建日志条目（使用简洁格式）
        StringBuilder logEntry = new StringBuilder();
        
        // 时间戳和当前对象位置
        if (includeTimestamp)
        {
            logEntry.Append($"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
        }
        logEntry.Append($"当前对象位置: ({currentTransform.position.x:F2}, {currentTransform.position.y:F2}, {currentTransform.position.z:F2}) | ");
        
        // 计算标签统计
        Dictionary<string, int> tagCounts = new Dictionary<string, int>();
        foreach (var obj in perceivedObjects)
        {
            string tag = string.IsNullOrEmpty(obj.tag) ? "Untagged" : obj.tag;
            if (tagCounts.ContainsKey(tag))
                tagCounts[tag]++;
            else
                tagCounts[tag] = 1;
        }
        
        // 环境感知摘要： [环境感知|半径=Xm|数量=X] tag1:count1, tag2:count2
        logEntry.AppendFormat("[环境感知|半径={0:0}m|数量={1}]", scanRadius, perceivedObjects.Count);
        if (perceivedObjects.Count > 0)
        {
            logEntry.Append(" ");
            int printed = 0;
            foreach (var kv in tagCounts)
            {
                if (printed > 0) logEntry.Append(", ");
                logEntry.Append(kv.Key).Append(":").Append(kv.Value);
                printed++;
            }
        }
        logEntry.AppendLine();
        
        // 每个物体的详细信息
        for (int i = 0; i < perceivedObjects.Count; i++)
        {
            var obj = perceivedObjects[i];
            string tag = string.IsNullOrEmpty(obj.tag) ? "Untagged" : obj.tag;
            float bearingRounded = Mathf.Round(obj.bearingDeg);
            // 格式：序号) 名称(标签) at (x,y,z) 距离Xm 方向X°
            var lineBuilder = new StringBuilder();
            lineBuilder.AppendFormat("{0}) {1}({2}) at ({3:F1},{4:F1},{5:F1}) 距离{6:0.0}m 方向{7:+0;-0;0}°",
                i + 1,
                obj.name,
                tag,
                obj.position.x, obj.position.y, obj.position.z,
                obj.distance,
                bearingRounded);

            if (tag == "Human")
            {
                string humanStateLabel = "未知";
                Vector3 forward = Vector3.forward;

                if (obj.transform != null)
                {
                    forward = obj.transform.forward;
                    if (obj.transform.TryGetComponent(out HumanInteraction interaction))
                    {
                        humanStateLabel = interaction.IsInteractable ? "Interactable" : "NonInteractable";
                    }
                }

                lineBuilder.AppendFormat(" 状态={0} 朝向=({1:F2},{2:F2},{3:F2})",
                    humanStateLabel,
                    forward.x,
                    forward.y,
                    forward.z);
                lineBuilder.AppendFormat("边界=({0:F2},{1:F2},{2:F3},{3:F4})",
                    obj.boundsmin.x,
                    obj.boundsmax.x,
                    obj.boundsmin.z,
                    obj.boundsmax.z);
            }

            logEntry.Append(lineBuilder);

            if (i < perceivedObjects.Count - 1)
            {
                logEntry.AppendLine();
            }
        }
        
        // 写入日志
        WriteToLog(logEntry.ToString());
    }
    
    private void WriteToLog(string content)
    {
        try
        {
            // 确保目录存在
            string directory = Path.GetDirectoryName(logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                //Debug.Log($"创建日志目录: {directory}");
            }
            
            // 使用StreamWriter并立即刷新缓冲区
            using (StreamWriter writer = new StreamWriter(logFilePath, appendToFile, Encoding.UTF8))
            {
                writer.WriteLine(content);
                writer.Flush(); // 立即刷新缓冲区到磁盘
            }
            
            //Debug.Log($"成功写入日志: {content}");
            
            // 验证文件是否真的存在
            if (File.Exists(logFilePath))
            {
                FileInfo fileInfo = new FileInfo(logFilePath);
                //Debug.Log($"日志文件验证成功: {logFilePath}, 大小: {fileInfo.Length} 字节");
            }
            else
            {
                //Debug.LogError($"文件写入后验证失败: {logFilePath}");
            }
        }
        catch 
        {
            //Debug.LogError($"写入日志文件失败: {e.Message}");
           // Debug.LogError($"日志文件路径: {logFilePath}");
            //Debug.LogError($"详细错误: {e}");
        }
    }
    
    /// <summary>
    /// 手动触发一次环境扫描
    /// </summary>
    public void ManualScan()
    {
        ScanAndLogEnvironment();
    }
    
    /// <summary>
    /// 开始/停止定期扫描
    /// </summary>
    public void ToggleScanning()
    {
        enableLogging = !enableLogging;
        
        if (enableLogging)
        {
            StartPeriodicScanning();
            //Debug.Log("环境扫描已启动");
        }
        else
        {
            if (scanCoroutine != null)
            {
                StopCoroutine(scanCoroutine);
                scanCoroutine = null;
            }
            //Debug.Log("环境扫描已停止");
        }
    }
    
    /// <summary>
    /// 设置扫描间隔
    /// </summary>
    public void SetScanInterval(float interval)
    {
        scanInterval = Mathf.Max(0.1f, interval);
        
        if (enableLogging)
        {
            StartPeriodicScanning(); // 重启扫描以应用新间隔
        }
    }
    
    /// <summary>
    /// 设置扫描半径
    /// </summary>
    public void SetScanRadius(float radius)
    {
        scanRadius = Mathf.Max(0.1f, radius);
    }
    
    /// <summary>
    /// 获取扫描半径
    /// </summary>
    public float GetScanRadius()
    {
        return scanRadius;
    }
    
    /// <summary>
    /// 设置自定义日志路径
    /// </summary>
    /// <param name="path">日志文件路径（可以是绝对路径或相对路径）</param>
    /// <param name="fileName">文件名（可选，如果为空则使用当前文件名）</param>
    public void SetCustomLogPath(string path, string fileName = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            //Debug.LogWarning("路径不能为空，将使用默认路径");
            useCustomPath = false;
            return;
        }
        
        customLogPath = path;
        useCustomPath = true;
        
        if (!string.IsNullOrEmpty(fileName))
        {
            logFileName = fileName;
        }
        
        // 重新计算文件路径
        if (Path.IsPathRooted(customLogPath))
        {
            logFilePath = Path.Combine(customLogPath, logFileName);
        }
        else
        {
            logFilePath = Path.Combine(Application.dataPath, "..", customLogPath, logFileName);
        }
        
        //Debug.Log($"自定义路径已设置: {logFilePath}");
        
        // 重新初始化日志文件
        InitializeLogFile();
    }
    
    /// <summary>
    /// 使用默认路径
    /// </summary>
    public void UseDefaultPath()
    {
        useCustomPath = false;
        string defaultLogDirectory = @"D:\file\shixi\chanping\AI\log";
        logFilePath = Path.Combine(defaultLogDirectory, logFileName);
        //Debug.Log($"已切换到默认路径: {logFilePath}");
        
        // 重新初始化日志文件
        InitializeLogFile();
    }
    
    /// <summary>
    /// 获取日志文件路径
    /// </summary>
    public string GetLogFilePath()
    {
        return logFilePath;
    }
    
    /// <summary>
    /// 设置日志文件名
    /// </summary>
    /// <param name="fileName">新的文件名</param>
    public void SetLogFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            //Debug.LogWarning("文件名不能为空");
            return;
        }
        
        logFileName = fileName;
        
        // 重新计算文件路径
        if (useCustomPath && !string.IsNullOrEmpty(customLogPath))
        {
            if (Path.IsPathRooted(customLogPath))
            {
                logFilePath = Path.Combine(customLogPath, logFileName);
            }
            else
            {
                logFilePath = Path.Combine(Application.dataPath, "..", customLogPath, logFileName);
            }
        }
        else
        {
            string defaultLogDirectory = @"D:\file\shixi\chanping\AI\log";
            logFilePath = Path.Combine(defaultLogDirectory, logFileName);
        }
        
        //Debug.Log($"日志文件名已设置为: {logFileName}");
        //Debug.Log($"完整路径: {logFilePath}");
        
        // 重新初始化日志文件
        InitializeLogFile();
    }
    
    /// <summary>
    /// 在控制台显示日志文件路径
    /// </summary>
    public void ShowLogFilePath()
    {
        //Debug.Log($"日志文件路径: {logFilePath}");
        //Debug.Log($"文件是否存在: {File.Exists(logFilePath)}");
        if (File.Exists(logFilePath))
        {
            //Debug.Log($"文件大小: {new FileInfo(logFilePath).Length} 字节");
        }
    }
    
    /// <summary>
    /// 清空日志文件
    /// </summary>
    public void ClearLogFile()
    {
        try
        {
            File.WriteAllText(logFilePath, "=== 环境扫描日志开始 ===\n", Encoding.UTF8);
            //Debug.Log("日志文件已清空");
        }
        catch
        {
           //Debug.LogError($"清空日志文件失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 手动刷新文件系统缓冲区
    /// </summary>
    public void ManualFlush()
    {
        ForceFlushFileSystem();
    }
    
    // ========== 预设路径方法 ==========
    
    /// <summary>
    /// 设置日志到当前项目文件夹
    /// </summary>
    [ContextMenu("设置到当前项目文件夹")]
    public void SetToCurrentProjectFolder()
    {
        // 获取当前项目根目录
        string projectPath = Application.dataPath; // Assets文件夹的父目录
        string projectRoot = Path.GetDirectoryName(projectPath); // 项目根目录
        
        //Debug.Log($"项目根目录: {projectRoot}");
        SetCustomLogPath(projectRoot, "environment_log.txt");
    }
    
    /// <summary>
    /// 设置日志到项目根目录下的Logs文件夹
    /// </summary>
    [ContextMenu("设置到项目Logs文件夹")]
    public void SetToProjectLogsFolder()
    {
        // 获取项目根目录
        string projectPath = Application.dataPath;
        string projectRoot = Path.GetDirectoryName(projectPath);
        string logsPath = Path.Combine(projectRoot, "Logs");
        
        //Debug.Log($"项目Logs文件夹: {logsPath}");
        SetCustomLogPath(logsPath, "environment_log.txt");
    }
    
    /// <summary>
    /// 设置日志到桌面
    /// </summary>
    [ContextMenu("设置到桌面")]
    public void SetToDesktop()
    {
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string fullPath = Path.Combine(desktopPath, "GameLogs");
        SetCustomLogPath(fullPath, "environment_log.txt");
    }
    
    /// <summary>
    /// 使用默认路径
    /// </summary>
    [ContextMenu("使用默认路径")]
    public void UseDefaultPathMenu()
    {
        UseDefaultPath();
    }
    
    /// <summary>
    /// 检查日志文件状态
    /// </summary>
    public void CheckLogFileStatus()
    {
        //Debug.Log($"=== 日志文件状态检查 ===");
        //Debug.Log($"文件路径: {logFilePath}");
       // Debug.Log($"文件是否存在: {File.Exists(logFilePath)}");
        
        if (File.Exists(logFilePath))
        {
            FileInfo fileInfo = new FileInfo(logFilePath);
            //Debug.Log($"文件大小: {fileInfo.Length} 字节");
            //Debug.Log($"最后修改时间: {fileInfo.LastWriteTime}");
            
            // 读取文件内容的前几行
            try
            {
                string[] lines = File.ReadAllLines(logFilePath);
                //Debug.Log($"文件行数: {lines.Length}");
                if (lines.Length > 0)
                {
                    //Debug.Log($"第一行: {lines[0]}");
                    if (lines.Length > 1)
                    {
                        //Debug.Log($"最后一行: {lines[lines.Length - 1]}");
                    }
                }
            }
            catch
            {
                //Debug.LogError($"读取文件内容失败: {e.Message}");
            }
        }
        //Debug.Log($"=== 状态检查完成 ===");
    }
    
    private void OnDestroy()
    {
        //Debug.Log("=== EnvironmentLogger OnDestroy() ===");
        
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
            //Debug.Log("停止扫描协程");
        }
        
        if (enableLogging)
        {
            WriteToLog("=== 环境扫描日志结束 ===");
            //Debug.Log("写入结束标记");
        }
        
        // 强制刷新文件系统缓冲区
        ForceFlushFileSystem();
    }
    
    /// <summary>
    /// 强制刷新文件系统缓冲区
    /// </summary>
    private void ForceFlushFileSystem()
    {
        try
        {
            // 强制刷新文件系统缓冲区
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            //Debug.Log("文件系统缓冲区已刷新");
            
            // 再次验证文件
            if (File.Exists(logFilePath))
            {
                FileInfo fileInfo = new FileInfo(logFilePath);
                //Debug.Log($"最终文件验证: {logFilePath}, 大小: {fileInfo.Length} 字节");
            }
        }
        catch 
        {
            //Debug.LogError($"刷新文件系统失败: {e.Message}");
        }
    }
    
    private void OnValidate()
    {
        // 在编辑器中验证参数
        scanInterval = Mathf.Max(0.1f, scanInterval);
        scanRadius = Mathf.Max(0.1f, scanRadius);
    }
}


