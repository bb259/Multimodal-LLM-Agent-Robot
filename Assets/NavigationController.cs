using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;

/// <summary>
/// 导航路径类型枚举
/// </summary>
public enum NavigationPathType
{
    Straight,   // 直线路径
    SCurve      // S形曲线路径
}

/// <summary>
/// 导航控制器：解析LLM输出的目标位置，控制游戏对象旋转和移动
/// </summary>
public class NavigationController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float totalNavigationDuration = 5f; // 导航总时间（旋转+移动，秒）
    [SerializeField] private float rotationTimeRatio = 0.3f; // 旋转时间占比（0-1），剩余时间用于移动
    [SerializeField] private float speedScale = 1f; // 导航速度系数：1为默认速度，>1更快，<1更慢
    [SerializeField] private float movementOrientationSpeed = 360f; // 移动过程中的朝向跟随速度（度/秒）
    [SerializeField] private bool snapOrientationToPath = true; // 是否每帧强制朝向路径切线
    [SerializeField] private float curveHeight = 0.5f; // 曲线路径的弧度高度（米）
    
    [Header("组件引用")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private NavigationVisualizer visualizer; // 可视化组件
    
    private bool isRotating = false;
    private bool isMoving = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private float rotationTimer = 0f;
    private float movementTimer = 0f;
    private float postMoveWaitDuration = 0f;
	private bool isWaiting = false;
	private Vector3? lookAtTarget = null; // 到达后要朝向的目标位置（可选）
	private float lookAtRotationDuration = 0.5f; // 朝向目标旋转的时间（秒）
	private NavigationPathType currentPathType = NavigationPathType.Straight; // 当前路径类型
	
    // 动作序列相关
    private ActionSequence currentActionSequence = null;
    private bool currentActionSequenceHasWait = false; // 当前动作序列是否包含停留动作
    private bool isNavigationPaused = false; // 导航是否被暂停（由停留动作触发）
    
    // 正则表达式：匹配 CSV 行的前三个位置坐标，支持格式：x,y,z,action_sequence,path_type
    // action_sequence 可能包含逗号，所以需要匹配到倒数第二个逗号之前的内容
    private static readonly Regex PositionRegex = new Regex(
        @"^\s*([-+]?\d+\.?\d*)\s*,\s*([-+]?\d+\.?\d*)\s*,\s*([-+]?\d+\.?\d*)\s*,",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
    
    /// <summary>
    /// 设置导航速度系数
    /// </summary>
    /// <param name="scale">速度系数（建议范围 0.2 - 5.0），1 为默认速度，>1 更快，<1 更慢</param>
    public void SetSpeedScale(float scale)
    {
        // 做一下安全限制，避免极端值
        speedScale = Mathf.Clamp(scale, 0.2f, 5f);
    }
    
    /// <summary>
    /// 检查文本中是否包含目标位置信息
    /// 新格式：x,y,z,action_sequence,path_type
    /// </summary>
    public bool TryParseTargetPosition(string text, out Vector3 position)
    {
        position = Vector3.zero;
        
        if (string.IsNullOrEmpty(text))
            return false;
        
        // 新格式：x,y,z,action_sequence,path_type
        // action_sequence可能包含逗号，所以需要先找到前三个逗号分隔的值
        string trimmed = text.Trim();
        string[] parts = trimmed.Split(',');
        
        if (parts.Length >= 3)
        {
            // 前三个部分应该是 x, y, z
            if (float.TryParse(parts[0].Trim(), out float x) &&
                float.TryParse(parts[1].Trim(), out float y) &&
                float.TryParse(parts[2].Trim(), out float z))
            {
                // 强制锁定 Y 为当前物体的 Y，忽略解析得到的 y
                position = new Vector3(x, transform.position.y, z);
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 移动到目标位置（先旋转，再移动）
    /// </summary>
    /// <param name="targetPos">目标位置</param>
    /// <param name="dwellSeconds">到达后停留时间（秒）（已废弃，使用actionSequence代替）</param>
    /// <param name="lookAtPosition">到达后要朝向的目标位置（可选，如果提供则在停留前先朝向此位置）</param>
    /// <param name="pathType">路径类型（直线或曲线）</param>
    /// <param name="actionSequence">动作序列（5秒的动作序列，与导航并行执行）</param>
    public void NavigateToPosition(Vector3 targetPos, float dwellSeconds = 0f, Vector3? lookAtPosition = null, NavigationPathType pathType = NavigationPathType.Straight, ActionSequence actionSequence = null)
    {
        // 无论是否正在导航，都先完全停止当前导航（包括动作序列）
        // 这确保新导航能够立即开始，不会受到旧导航的影响
        if (isRotating || isMoving || isWaiting)
        {
            Debug.Log($"[NavigateToPosition] 检测到导航正在进行，立即停止 (时间: {Time.time:F2}秒)");
            StopNavigation(); // 使用StopNavigation确保完全清理所有状态
        }
        else
        {
            // 即使状态标志为false，也确保动作序列被停止
            LimbAnimationController limbController = GetComponent<LimbAnimationController>();
            if (limbController != null && limbController.IsExecutingActionSequence())
            {
                Debug.Log($"[NavigateToPosition] 检测到动作序列正在执行，立即停止 (时间: {Time.time:F2}秒)");
                limbController.StopActionSequence();
            }
        }
        
        // 清除旧的目标位置可视化（新目标会立即设置）
        if (visualizer != null)
        {
            visualizer.ClearTargetPosition();
        }
        targetPosition = targetPos;
        postMoveWaitDuration = Mathf.Max(0f, dwellSeconds);
        lookAtTarget = lookAtPosition;
        currentPathType = pathType;
        currentActionSequence = actionSequence;
        currentActionSequenceHasWait = (currentActionSequence != null && currentActionSequence.actions.Exists(a => a.type == ActionType.Wait));
        isNavigationPaused = false;
        
        // 计算目标方向
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0f; // 只在水平面旋转
        
        float distance = directionToTarget.magnitude;
        
        Debug.Log($"[NavigationController] 开始导航到目标位置: ({targetPos.x:F2}, {targetPos.y:F2}, {targetPos.z:F2}), 距离: {distance:F2}m");
        
        if (distance < 0.1f)
        {
            Debug.LogWarning("[NavigationController] 目标位置与当前位置太近，无需移动");
            return;
        }
        
        // 计算目标旋转
        targetRotation = Quaternion.LookRotation(directionToTarget.normalized);
        initialRotation = transform.rotation;
        
        float angleDiff = Quaternion.Angle(initialRotation, targetRotation);
        
        // 这里不再直接使用 totalNavigationDuration，而是在导航协程中根据 speedScale 计算
        // 通知可视化器更新目标位置
        if (visualizer != null)
        {
            visualizer.SetTargetPosition(targetPosition);
        }

        StartCoroutine(NavigationSequence());
    }
    
    /// <summary>
    /// 导航序列：旋转和移动总时间共3秒
    /// </summary>
    private IEnumerator NavigationSequence()
    {
        // 根据速度系数计算本次导航的实际总时长
        float effectiveTotalDuration = totalNavigationDuration / Mathf.Max(speedScale, 0.01f);
        // 计算时间分配
        float rotationDuration = effectiveTotalDuration * rotationTimeRatio;
        float movementDuration = effectiveTotalDuration * (1f - rotationTimeRatio);
        
        float totalTimer = 0f;
        
        // 如果有动作序列，启动动作序列执行（与导航并行）
        if (currentActionSequence != null && currentActionSequence.actions.Count > 0)
        {
            LimbAnimationController limbController = GetComponent<LimbAnimationController>();
            if (limbController != null)
            {
                limbController.ExecuteActionSequence(currentActionSequence,
                    onActionStart: (actionType, shouldPauseNavigation) =>
                    {
                        if (shouldPauseNavigation)
                        {
                            isNavigationPaused = true;
                            Debug.Log("[导航暂停] 停留动作开始，暂停导航移动");
                        }
                    },
                    onActionEnd: (actionType) =>
                    {
                        if (actionType == ActionType.Wait)
                        {
                            isNavigationPaused = false;
                            Debug.Log("[导航恢复] 停留动作结束，恢复导航移动");
                        }
                    });
            }
        }
        
        // 第一步：旋转到目标方向
        isRotating = true;
        rotationTimer = 0f;
       
        while (rotationTimer < rotationDuration && totalTimer < totalNavigationDuration)
        {
            // 如果导航被暂停，不更新旋转计时器
            if (isNavigationPaused)
            {
                yield return null;
                continue;
            }
            
            rotationTimer += Time.deltaTime;
            totalTimer += Time.deltaTime;
            float t = Mathf.Clamp01(rotationTimer / rotationDuration);
            
            // 使用球面插值平滑旋转
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, t);
            
            yield return null;
        }
        
        // 确保最终旋转准确
        transform.rotation = targetRotation;
        isRotating = false;
       
		// 第二步：移动到目标位置（使用剩余时间）
        isMoving = true;
        movementTimer = 0f;
        initialPosition = transform.position;
        Vector3 directionToTarget = targetPosition - initialPosition;
        float totalDistance = directionToTarget.magnitude;
        
        // 使用实际剩余时间进行移动
        float actualMovementDuration = Mathf.Max(0.1f, effectiveTotalDuration - totalTimer);
        
        //Debug.Log($"开始移动到目标位置 ({targetPosition.x:F2}, {targetPosition.y:F2}, {targetPosition.z:F2})，距离: {totalDistance:F2}m，预计时间: {actualMovementDuration:F2}秒，路径类型: {currentPathType}");
        
        // 初始化移动方向为从初始位置到目标位置的方向，确保即使路径很短也有有效的初始方向
        Vector3 initialDirection = targetPosition - initialPosition;
        initialDirection.y = 0f;
        Vector3 lastMoveDirection = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : transform.forward;
        Vector3 previousTargetPos = initialPosition;
        
        while (movementTimer < actualMovementDuration && totalTimer < totalNavigationDuration)
        {
            // 如果导航被暂停（停留动作），不更新计时器和位置
            if (isNavigationPaused)
            {
                yield return null;
                continue;
            }
            
            movementTimer += Time.deltaTime;
            totalTimer += Time.deltaTime;
            float t = Mathf.Clamp01(movementTimer / actualMovementDuration);
            
            // 根据路径类型计算当前应该到达的位置
            Vector3 currentTargetPos;
            if (currentPathType == NavigationPathType.SCurve)
            {
                // S形曲线路径：使用三次贝塞尔曲线
                currentTargetPos = CalculateSCurvePosition(initialPosition, targetPosition, t);
            }
            else
            {
                // 直线路径：线性插值
                currentTargetPos = Vector3.Lerp(initialPosition, targetPosition, t);
            }         
            
            // 计算路径方向：使用路径上相邻两点的方向作为切线方向
            // 对于短路径，这能更准确地反映实际移动方向
            Vector3 pathDirection = currentTargetPos - previousTargetPos;
            Vector3 planarPathDirection = new Vector3(pathDirection.x, 0f, pathDirection.z);
            
            // 如果相邻两点的方向太小（路径很短），使用当前位置到目标位置的方向作为备选
            if (planarPathDirection.sqrMagnitude > 0.0000000000000001f)
            {
                lastMoveDirection = planarPathDirection.normalized;
                Quaternion desiredRotation = Quaternion.LookRotation(lastMoveDirection, Vector3.up);
                if (snapOrientationToPath)
                {
                    transform.rotation = desiredRotation;
                }
                else
                {
                    float maxStep = Mathf.Max(0f, movementOrientationSpeed) * Time.deltaTime;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, maxStep);
                }
            }
            else
            {
                // 对于极短的路径，使用从当前位置到目标位置的总体方向
                Vector3 toTarget = targetPosition - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    lastMoveDirection = toTarget.normalized;
                }
                // 如果还是太小，保持上一帧的方向不变
            }

            transform.position = new Vector3(currentTargetPos.x, transform.position.y, currentTargetPos.z);

            previousTargetPos = currentTargetPos;
            
            yield return null;
        }
        
        // 确保最终位置准确
        Vector3 finalPos = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
        if (characterController != null)
        {
            Vector3 finalMove = finalPos - transform.position;
            characterController.Move(finalMove);
        }
        else
        {
            transform.position = finalPos;
        }

        isMoving = false;
        if (lookAtTarget.HasValue)
        {
            // 到达后瞬时对齐朝向，避免额外等待
            Vector3 directionToLookAt = lookAtTarget.Value - transform.position;
            directionToLookAt.y = 0f;
            if (directionToLookAt.sqrMagnitude > 0.1f)
            {
                Quaternion lookAtRotation = Quaternion.LookRotation(directionToLookAt.normalized);
                Quaternion startLookRotation = transform.rotation;
                float lookAtAngleDiff = Quaternion.Angle(startLookRotation, lookAtRotation);
                
                if (lookAtAngleDiff > 1f) // 如果角度差大于1度才旋转
                {
                    isRotating = true;
                    float lookAtTimer = 0f;
                    
                    while (lookAtTimer < lookAtRotationDuration)
                    {
                        lookAtTimer += Time.deltaTime;
                        float t = Mathf.Clamp01(lookAtTimer / lookAtRotationDuration);
                        transform.rotation = Quaternion.Slerp(startLookRotation, lookAtRotation, t);
                        yield return null;
                    }
                    transform.rotation=lookAtRotation;
                    isRotating = false;
                }
            }
        }
        // 清除朝向目标和可视化器目标位置
        lookAtTarget = null;
        currentActionSequence = null;
        currentActionSequenceHasWait = false;
        isNavigationPaused = false;
        if (visualizer != null)
        {
            visualizer.ClearTargetPosition();
        }
    }
    
    /// <summary>
    /// 检查是否正在进行导航
    /// </summary>
	public bool IsNavigating()
    {
		return isRotating || isMoving || isWaiting;
    }

	/// <summary>
	/// 是否处于到达后的等待阶段
	/// </summary>
	public bool IsWaiting()
	{
		return isWaiting;
	}
	
	/// <summary>
	/// 获取一次导航（旋转+移动）的总时长估计
	/// </summary>
	public float GetTotalNavigationDuration()
	{
		return totalNavigationDuration;
	}
    
    /// <summary>
    /// 停止当前导航
    /// </summary>
    public void StopNavigation()
    {
        // 停止动作序列（如果存在）
        LimbAnimationController limbController = GetComponent<LimbAnimationController>();
        if (limbController != null && limbController.IsExecutingActionSequence())
        {
            Debug.Log("[停止导航] 先停止动作序列");
            limbController.StopActionSequence();
        }
        // 无条件停止导航协程和重置状态，确保完全打断
        Debug.Log("[停止导航] 停止所有导航协程并重置状态");
        StopAllCoroutines();
        isRotating = false;
        isMoving = false;
        isWaiting = false;
        postMoveWaitDuration = 0f;
        lookAtTarget = null;
        isNavigationPaused = false;
        currentActionSequence = null;
    }
    
    /// <summary>
    /// 计算到目标位置的距离和角度
    /// </summary>
    public void CalculateNavigationInfo(Vector3 targetPos, out float distance, out float angle)
    {
        Vector3 direction = targetPos - transform.position;
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
        
        distance = flatDirection.magnitude;
        
        // 计算角度（相对于当前朝向）
        if (flatDirection.magnitude > 0.01f)
        {
            Vector3 forward = transform.forward;
            Vector3 forwardFlat = new Vector3(forward.x, 0f, forward.z).normalized;
            Vector3 targetFlat = flatDirection.normalized;
            
            angle = Vector3.SignedAngle(forwardFlat, targetFlat, Vector3.up);
        }
        else
        {
            angle = 0f;
        }
    }
    private void Awake()
    { 
        if (visualizer == null)
        {
            visualizer = GetComponent<NavigationVisualizer>();
        }
        // 自动查找CharacterController组件（如果Inspector中未设置）
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }
    /// <summary>
    /// 计算S形曲线路径上的位置（使用三次贝塞尔曲线）
    /// </summary>
    /// <param name="start">起始位置</param>
    /// <param name="end">结束位置</param>
    /// <param name="t">插值参数（0-1）</param>
    /// <returns>S形曲线上的位置</returns>
    private Vector3 CalculateSCurvePosition(Vector3 start, Vector3 end, float t)
    {
        // 计算方向向量
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
        
        // 如果垂直向量为零（方向与up平行），使用右向量
        if (perpendicular.sqrMagnitude < 0.001f)
        {
            perpendicular = Vector3.Cross(direction, Vector3.right).normalized;
            if (perpendicular.sqrMagnitude < 0.001f)
            {
                perpendicular = Vector3.right;
            }
        }
        
        float distance = Vector3.Distance(start, end);
        float curveAmplitude = Mathf.Min(curveHeight, distance * 0.25f); // S形幅度不超过距离的25%
        
        // 计算两个控制点，形成S形
        // 第一个控制点：在起点附近，向一侧偏移
        Vector3 controlPoint1 = start + direction * (distance * 0.33f) + perpendicular * curveAmplitude;
        
        // 第二个控制点：在终点附近，向另一侧偏移（形成S形）
        Vector3 controlPoint2 = start + direction * (distance * 0.67f) - perpendicular * curveAmplitude;
        
        // 三次贝塞尔曲线公式：B(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
        float oneMinusT = 1f - t;
        float oneMinusT2 = oneMinusT * oneMinusT;
        float oneMinusT3 = oneMinusT2 * oneMinusT;
        float t2 = t * t;
        float t3 = t2 * t;
        
        Vector3 position = oneMinusT3 * start + 
                          3f * oneMinusT2 * t * controlPoint1 + 
                          3f * oneMinusT * t2 * controlPoint2 + 
                          t3 * end;
        
        // 保持Y坐标在水平面上
        position = new Vector3(position.x, transform.position.y, position.z);
        
        return position;
    }
    
    private void OnValidate()
    {
        totalNavigationDuration = Mathf.Max(0.1f, totalNavigationDuration);
        rotationTimeRatio = Mathf.Clamp01(rotationTimeRatio);
        curveHeight = Mathf.Max(0f, curveHeight);
        movementOrientationSpeed = Mathf.Max(0f, movementOrientationSpeed);
    }
}


