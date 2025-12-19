using UnityEngine;

/// <summary>
/// 导航可视化器：可视化感知范围、朝向和目标位置
/// </summary>
public class NavigationVisualizer : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private EnvironmentLogger environmentLogger;
    [SerializeField] private NavigationController navigationController;
    
    [Header("可视化设置")]
    [SerializeField] private bool showInSceneView = true; // 在Scene视图中显示
    [SerializeField] private bool showInGameView = true; // 在Game视图中显示（运行时）
    [SerializeField] private float perceptionRadius = 1f; // 感知半径（米）
    
    [Header("颜色设置")]
    [SerializeField] private Color perceptionSphereColor = Color.yellow; // 感知范围球体颜色
    [SerializeField] private Color forwardDirectionColor = Color.green; // 朝向方向线颜色
    [SerializeField] private Color targetPositionColor = Color.red; // 目标位置线和点颜色
    
    [Header("线宽设置")]
    [SerializeField] private float forwardLineLength = 1f; // 朝向线长度
    [SerializeField] private float targetPointSize = 0.2f; // 目标点大小
    
    private Vector3 currentTargetPosition;
    private bool hasTarget = false;
    private LineRenderer forwardLineRenderer;
    private LineRenderer targetLineRenderer;
    
    private void Awake()
    {
        // 自动查找组件
        if (environmentLogger == null)
            environmentLogger = GetComponent<EnvironmentLogger>();
        
        if (navigationController == null)
            navigationController = GetComponent<NavigationController>();
        
        // 获取感知半径
        if (environmentLogger != null)
        {
            perceptionRadius = environmentLogger.GetScanRadius();
        }
    }
    
    private void Start()
    {
        // 创建运行时可视化线条（如果需要）
        if (showInGameView)
        {
            CreateRuntimeVisualizers();
        }
    }
    
    private void CreateRuntimeVisualizers()
    {
        // 创建朝向线
        GameObject forwardLineObj = new GameObject("ForwardDirectionLine");
        forwardLineObj.transform.SetParent(transform);
        forwardLineObj.transform.localPosition = Vector3.zero;
        forwardLineRenderer = forwardLineObj.AddComponent<LineRenderer>();
        forwardLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        forwardLineRenderer.startColor = forwardDirectionColor;
        forwardLineRenderer.endColor = forwardDirectionColor;
        forwardLineRenderer.startWidth = 0.05f;
        forwardLineRenderer.endWidth = 0.05f;
        forwardLineRenderer.positionCount = 2;
        forwardLineRenderer.useWorldSpace = true;
        
        // 创建目标线
        GameObject targetLineObj = new GameObject("TargetPositionLine");
        targetLineObj.transform.SetParent(transform);
        targetLineObj.transform.localPosition = Vector3.zero;
        targetLineRenderer = targetLineObj.AddComponent<LineRenderer>();
        targetLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        targetLineRenderer.startColor = targetPositionColor;
        targetLineRenderer.endColor = targetPositionColor;
        targetLineRenderer.startWidth = 0.03f;
        targetLineRenderer.endWidth = 0.03f;
        targetLineRenderer.positionCount = 2;
        targetLineRenderer.useWorldSpace = true;
        targetLineRenderer.enabled = false;
    }
    
    private void Update()
    {
        if (showInGameView)
        {
            UpdateRuntimeVisualizers();
        }
        
        // 同步感知半径（如果EnvironmentLogger的半径改变）
        if (environmentLogger != null)
        {
            float newRadius = environmentLogger.GetScanRadius();
            if (Mathf.Abs(perceptionRadius - newRadius) > 0.01f)
            {
                perceptionRadius = newRadius;
            }
        }
    }
    
    /// <summary>
    /// 设置目标位置（由NavigationController调用）
    /// </summary>
    public void SetTargetPosition(Vector3 targetPos)
    {
        currentTargetPosition = targetPos;
        hasTarget = true;
    }
    
    /// <summary>
    /// 清除目标位置
    /// </summary>
    public void ClearTargetPosition()
    {
        hasTarget = false;
        if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = false;
        }
    }
    
    private void UpdateRuntimeVisualizers()
    {
        if (forwardLineRenderer != null)
        {
            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * forwardLineLength;
            forwardLineRenderer.SetPosition(0, start);
            forwardLineRenderer.SetPosition(1, end);
            forwardLineRenderer.startColor = forwardDirectionColor;
            forwardLineRenderer.endColor = forwardDirectionColor;
        }
        
        if (targetLineRenderer != null && hasTarget)
        {
            targetLineRenderer.enabled = true;
            targetLineRenderer.SetPosition(0, transform.position);
            targetLineRenderer.SetPosition(1, currentTargetPosition);
            targetLineRenderer.startColor = targetPositionColor;
            targetLineRenderer.endColor = targetPositionColor;
        }
        else if (targetLineRenderer != null)
        {
            targetLineRenderer.enabled = false;
        }
    }
    
    /// <summary>
    /// Scene视图绘制（Gizmos）
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showInSceneView) return;
        
        // 绘制感知范围球体（黄色）
        Gizmos.color = perceptionSphereColor;
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);
        
        // 绘制朝向方向线（绿色）
        Gizmos.color = forwardDirectionColor;
        Vector3 forwardEnd = transform.position + transform.forward * forwardLineLength;
        Gizmos.DrawLine(transform.position, forwardEnd);
        
        // 绘制目标位置线和点（红色）
        if (hasTarget)
        {
            Gizmos.color = targetPositionColor;
            // 从当前位置到目标位置的线
            Gizmos.DrawLine(transform.position, currentTargetPosition);
            // 目标位置点
            Gizmos.DrawSphere(currentTargetPosition, targetPointSize);
        }
    }
    
    /// <summary>
    /// 设置感知半径
    /// </summary>
    public void SetPerceptionRadius(float radius)
    {
        perceptionRadius = Mathf.Max(0.1f, radius);
    }
    
    private void OnValidate()
    {
        perceptionRadius = Mathf.Max(0.1f, perceptionRadius);
        forwardLineLength = Mathf.Max(0.1f, forwardLineLength);
        targetPointSize = Mathf.Max(0.05f, targetPointSize);
    }
}


