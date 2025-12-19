using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 多摄像头显示控制器
/// 可以同时显示多个Camera组件的画面到UI界面
/// </summary>
public class MultiCameraDisplayController : MonoBehaviour
{
    [System.Serializable]
    public class CameraDisplayItem
    {
        [Tooltip("要显示的Camera组件")]
        public Camera targetCamera;
        
        [Tooltip("用于显示该Camera画面的RawImage组件")]
        public RawImage displayImage;
        
        [Tooltip("RenderTexture分辨率宽度")]
        public int textureWidth = 1920;
        
        [Tooltip("RenderTexture分辨率高度")]
        public int textureHeight = 1080;
        
        [Tooltip("是否启用此摄像头显示")]
        public bool isEnabled = true;
        
        // 内部使用的RenderTexture
        [HideInInspector]
        public RenderTexture renderTexture;
    }

    [Header("摄像头显示列表")]
    [Tooltip("摄像头显示配置列表，可以添加多个Camera")]
    public List<CameraDisplayItem> cameraDisplays = new List<CameraDisplayItem>();

    [Header("全局设置")]
    [Tooltip("默认RenderTexture分辨率宽度（新建项时使用）")]
    public int defaultTextureWidth = 1920;
    
    [Tooltip("默认RenderTexture分辨率高度（新建项时使用）")]
    public int defaultTextureHeight = 1080;

    [Header("调试信息")]
    [Tooltip("是否在控制台输出调试信息")]
    public bool showDebugInfo = true;

    private bool isInitialized = false;

    void Start()
    {
        InitializeAllCameras();
    }

    /// <summary>
    /// 初始化所有摄像头
    /// </summary>
    private void InitializeAllCameras()
    {
        if (cameraDisplays == null || cameraDisplays.Count == 0)
        {
            Debug.LogWarning("MultiCameraDisplayController: 摄像头显示列表为空！请在Inspector中添加摄像头显示项。");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < cameraDisplays.Count; i++)
        {
            var display = cameraDisplays[i];
            if (display == null)
            {
                Debug.LogWarning($"MultiCameraDisplayController: 列表项 [{i}] 为 null，跳过");
                failCount++;
                continue;
            }

            if (!display.isEnabled)
            {
                if (showDebugInfo)
                    Debug.Log($"MultiCameraDisplayController: 列表项 [{i}] 已禁用，跳过");
                continue;
            }

            if (InitializeCameraDisplay(display, i))
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log($"MultiCameraDisplayController: 初始化完成 - 成功: {successCount}, 失败: {failCount}, 总计: {cameraDisplays.Count}");
        }
    }

    /// <summary>
    /// 初始化单个摄像头显示
    /// </summary>
    /// <param name="display">摄像头显示项</param>
    /// <param name="index">列表索引（用于调试）</param>
    /// <returns>初始化是否成功</returns>
    private bool InitializeCameraDisplay(CameraDisplayItem display, int index = -1)
    {
        string indexInfo = index >= 0 ? $"[{index}] " : "";

        if (display.targetCamera == null)
        {
            Debug.LogError($"MultiCameraDisplayController: {indexInfo}targetCamera 未设置！请在Inspector中为列表项 [{index}] 的 'Target Camera' 字段拖入一个Camera组件。");
            return false;
        }

        if (display.displayImage == null)
        {
            Debug.LogError($"MultiCameraDisplayController: {indexInfo}displayImage 未设置（Camera: {display.targetCamera.name}）！\n" +
                          $"请在Inspector中为列表项 [{index}] 的 'Display Image' 字段拖入一个RawImage组件。\n" +
                          $"创建步骤：1. 在Canvas下创建RawImage 2. 将RawImage拖到脚本的Display Image字段");
            return false;
        }

        // 检查Camera是否启用
        if (!display.targetCamera.enabled)
        {
            Debug.LogWarning($"MultiCameraDisplayController: {indexInfo}Camera '{display.targetCamera.name}' 未启用，正在启用...");
            display.targetCamera.enabled = true;
        }

        // 如果RenderTexture已存在且尺寸相同，则复用
        if (display.renderTexture != null && 
            display.renderTexture.width == display.textureWidth && 
            display.renderTexture.height == display.textureHeight)
        {
            // RenderTexture已存在，只需重新分配
            display.targetCamera.targetTexture = display.renderTexture;
            display.displayImage.texture = display.renderTexture;
            
            if (showDebugInfo)
            {
                Debug.Log($"MultiCameraDisplayController: {indexInfo}复用现有RenderTexture '{display.targetCamera.name}'");
            }
            return true;
        }

        // 清理旧的RenderTexture
        if (display.renderTexture != null)
        {
            if (display.targetCamera != null)
                display.targetCamera.targetTexture = null;
            Destroy(display.renderTexture);
        }

        // 创建新的RenderTexture
        display.renderTexture = new RenderTexture(
            display.textureWidth, 
            display.textureHeight, 
            24,
            RenderTextureFormat.ARGB32
        );
        display.renderTexture.name = $"CameraDisplay_{display.targetCamera.name}_RT";

        // 将Camera的输出目标设置为RenderTexture
        display.targetCamera.targetTexture = display.renderTexture;

        // 设置到RawImage
        display.displayImage.texture = display.renderTexture;

        // 确保RawImage可见
        if (!display.displayImage.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"MultiCameraDisplayController: {indexInfo}RawImage '{display.displayImage.name}' 的游戏对象未激活，正在激活...");
            display.displayImage.gameObject.SetActive(true);
        }

        if (showDebugInfo)
        {
            Debug.Log($"MultiCameraDisplayController: {indexInfo}成功初始化摄像头 '{display.targetCamera.name}' -> RawImage '{display.displayImage.name}' ({display.textureWidth}x{display.textureHeight})");
        }

        return true;
    }

    /// <summary>
    /// 启用指定索引的摄像头显示
    /// </summary>
    public void EnableCameraDisplay(int index)
    {
        if (index < 0 || index >= cameraDisplays.Count)
        {
            Debug.LogError($"MultiCameraDisplayController: 索引 {index} 超出范围");
            return;
        }

        var display = cameraDisplays[index];
        if (display != null && !display.isEnabled)
        {
            display.isEnabled = true;
            InitializeCameraDisplay(display, index);
        }
    }

    /// <summary>
    /// 禁用指定索引的摄像头显示
    /// </summary>
    public void DisableCameraDisplay(int index)
    {
        if (index < 0 || index >= cameraDisplays.Count)
        {
            Debug.LogError($"MultiCameraDisplayController: 索引 {index} 超出范围");
            return;
        }

        var display = cameraDisplays[index];
        if (display != null && display.isEnabled)
        {
            display.isEnabled = false;
            CleanupCameraDisplay(display);
        }
    }

    /// <summary>
    /// 更新指定摄像头的分辨率
    /// </summary>
    /// <param name="index">摄像头索引</param>
    /// <param name="width">新宽度</param>
    /// <param name="height">新高度</param>
    public void UpdateCameraResolution(int index, int width, int height)
    {
        if (index < 0 || index >= cameraDisplays.Count)
        {
            Debug.LogError($"MultiCameraDisplayController: 索引 {index} 超出范围");
            return;
        }

        var display = cameraDisplays[index];
        if (display != null)
        {
            display.textureWidth = width;
            display.textureHeight = height;
            
            if (display.isEnabled)
            {
                InitializeCameraDisplay(display, index);
            }

            if (showDebugInfo)
            {
                Debug.Log($"MultiCameraDisplayController: 摄像头 [{index}] 分辨率已更新为 {width}x{height}");
            }
        }
    }

    /// <summary>
    /// 批量设置所有摄像头为相同分辨率
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    public void SetAllCamerasResolution(int width, int height)
    {
        if (cameraDisplays == null || cameraDisplays.Count == 0)
        {
            Debug.LogWarning("MultiCameraDisplayController: 摄像头显示列表为空");
            return;
        }

        for (int i = 0; i < cameraDisplays.Count; i++)
        {
            UpdateCameraResolution(i, width, height);
        }

        if (showDebugInfo)
        {
            Debug.Log($"MultiCameraDisplayController: 所有摄像头分辨率已设置为 {width}x{height}");
        }
    }

    /// <summary>
    /// 清理单个摄像头显示
    /// </summary>
    private void CleanupCameraDisplay(CameraDisplayItem display)
    {
        if (display.renderTexture != null)
        {
            if (display.targetCamera != null)
                display.targetCamera.targetTexture = null;
            
            Destroy(display.renderTexture);
            display.renderTexture = null;
        }

        if (display.displayImage != null)
        {
            display.displayImage.texture = null;
        }
    }

    /// <summary>
    /// 清理所有资源
    /// </summary>
    private void CleanupAll()
    {
        if (cameraDisplays != null)
        {
            foreach (var display in cameraDisplays)
            {
                if (display != null)
                {
                    CleanupCameraDisplay(display);
                }
            }
        }

        isInitialized = false;
    }

    /// <summary>
    /// 获取当前启用的摄像头数量
    /// </summary>
    public int GetActiveCameraCount()
    {
        int count = 0;
        if (cameraDisplays != null)
        {
            foreach (var display in cameraDisplays)
            {
                if (display != null && display.isEnabled)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 重新初始化所有摄像头（用于运行时修改配置后刷新）
    /// </summary>
    [ContextMenu("重新初始化所有摄像头")]
    public void ReinitializeAllCameras()
    {
        CleanupAll();
        InitializeAllCameras();
    }

    /// <summary>
    /// 检查配置并输出详细信息（用于调试）
    /// </summary>
    [ContextMenu("检查配置状态")]
    public void CheckConfiguration()
    {
        Debug.Log("=== MultiCameraDisplayController 配置检查 ===");
        
        if (cameraDisplays == null || cameraDisplays.Count == 0)
        {
            Debug.LogWarning("摄像头显示列表为空！");
            return;
        }

        Debug.Log($"总共有 {cameraDisplays.Count} 个配置项：\n");

        for (int i = 0; i < cameraDisplays.Count; i++)
        {
            var display = cameraDisplays[i];
            Debug.Log($"--- 配置项 [{i}] ---");
            
            if (display == null)
            {
                Debug.LogError($"  配置项为 null！");
                continue;
            }

            Debug.Log($"  启用状态: {(display.isEnabled ? "✓ 已启用" : "✗ 已禁用")}");
            Debug.Log($"  Target Camera: {(display.targetCamera != null ? $"✓ {display.targetCamera.name}" : "✗ 未设置")}");
            Debug.Log($"  Display Image: {(display.displayImage != null ? $"✓ {display.displayImage.name}" : "✗ 未设置")}");
            Debug.Log($"  分辨率: {display.textureWidth}x{display.textureHeight}");
            Debug.Log($"  RenderTexture: {(display.renderTexture != null ? $"✓ 已创建 ({display.renderTexture.width}x{display.renderTexture.height})" : "✗ 未创建")}");
            
            if (display.targetCamera != null)
            {
                Debug.Log($"  Camera启用: {(display.targetCamera.enabled ? "✓" : "✗")}");
                Debug.Log($"  Camera TargetTexture: {(display.targetCamera.targetTexture != null ? $"✓ {display.targetCamera.targetTexture.name}" : "✗ 未设置")}");
            }
            
            if (display.displayImage != null)
            {
                Debug.Log($"  RawImage激活: {(display.displayImage.gameObject.activeInHierarchy ? "✓" : "✗")}");
                Debug.Log($"  RawImage Texture: {(display.displayImage.texture != null ? $"✓ {display.displayImage.texture.name}" : "✗ 未设置")}");
            }
            
            Debug.Log("");
        }
        
        Debug.Log("=== 检查完成 ===");
    }

    void OnDestroy()
    {
        CleanupAll();
    }

    void OnDisable()
    {
        // 当组件被禁用时，可以暂停所有Camera的渲染以节省资源
        if (cameraDisplays != null)
        {
            foreach (var display in cameraDisplays)
            {
                if (display != null && display.targetCamera != null && display.isEnabled)
                {
                    display.targetCamera.enabled = false;
                }
            }
        }
    }

    void OnEnable()
    {
        // 当组件被启用时，恢复所有Camera的渲染
        if (cameraDisplays != null)
        {
            foreach (var display in cameraDisplays)
            {
                if (display != null && display.targetCamera != null && display.isEnabled)
                {
                    display.targetCamera.enabled = true;
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 在编辑器中验证配置
    /// </summary>
    void OnValidate()
    {
        // 确保默认值合理
        defaultTextureWidth = Mathf.Max(64, defaultTextureWidth);
        defaultTextureHeight = Mathf.Max(64, defaultTextureHeight);

        // 验证每个显示项的分辨率
        if (cameraDisplays != null)
        {
            foreach (var display in cameraDisplays)
            {
                if (display != null)
                {
                    display.textureWidth = Mathf.Max(64, display.textureWidth);
                    display.textureHeight = Mathf.Max(64, display.textureHeight);
                }
            }
        }
    }
#endif
}

