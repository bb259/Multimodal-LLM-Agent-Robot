using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Human视场可视化器：可视化Human的视场范围（蓝色扇形）
/// </summary>
public class HumanFovVisualizer : MonoBehaviour
{
    [Header("视场设置")]
    [SerializeField] public float FovAngle = 90f; // Human视场角度（度），以human的forward方向为中心，左右各一半
    [SerializeField] public float FovMaxDistance = 2f; // Human视场最大距离（米）
    
    [Header("可视化设置")]
    [SerializeField] private string humanTag = "Human"; // Human标签
    [SerializeField] private int fovSegments = 32; // 扇形分段数（越多越平滑）
    
    [Header("颜色设置")]
    [SerializeField] private Color fovEdgeColor = new Color(0f, 0.3f, 1f, 0.8f); // 蓝色边缘，更不透明
    
    [Header("显示设置")]
    [SerializeField] private float lineHeight = 0.1f; // 视场显示高度（Y轴偏移）
    
    private void Awake()
    {
        // 自动查找LLMAgent组件
    }
    
    private void Start()
    { 
    }
    private void Update()
    {
    }
    /// <summary>
    /// Scene视图绘制（Gizmos）
    /// </summary>
    private void OnDrawGizmos()
    {
        // 查找Human对象
        GameObject human = GameObject.FindGameObjectWithTag(humanTag);
        if (human == null) return;
        
        // 获取视场参数
        FovAngle = 90f;
        FovMaxDistance = 2f;
        
        // 绘制视场扇形（Gizmos）
        Vector3 center = human.transform.position + Vector3.up * lineHeight;
        Vector3 forward = human.transform.forward;
        float halfAngle = FovAngle * 0.5f;
        
        // 绘制扇形边缘
        Gizmos.color = fovEdgeColor;
        
        // 左边界
        Quaternion leftRot = Quaternion.AngleAxis(-halfAngle, Vector3.up);
        Vector3 leftDir = leftRot * forward;
        Gizmos.DrawLine(center, center + leftDir * FovMaxDistance);
        
        // 右边界
        Quaternion rightRot = Quaternion.AngleAxis(halfAngle, Vector3.up);
        Vector3 rightDir = rightRot * forward;
        Gizmos.DrawLine(center, center + rightDir * FovMaxDistance);
        
        // 绘制弧线
        Vector3 prevPoint = center + leftDir * FovMaxDistance;
        int segments = Mathf.Max(8, fovSegments);
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Quaternion rot = Quaternion.AngleAxis(currentAngle, Vector3.up);
            Vector3 dir = rot * forward;
            Vector3 currentPoint = center + dir * FovMaxDistance;
            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
        
        // 绘制中心线（Human朝向）
        Gizmos.color = new Color(fovEdgeColor.r, fovEdgeColor.g, fovEdgeColor.b, 0.5f);
        Gizmos.DrawLine(center, center + forward * FovMaxDistance);
    }
    
    private void OnValidate()
    {
        fovSegments = Mathf.Max(8, fovSegments);
        lineHeight = Mathf.Max(0f, lineHeight);
    }
}

