using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;

/// <summary>
/// 肢体动画控制器：控制抬手和抬头低头的动画
/// </summary>
public class LimbAnimationController : MonoBehaviour
{
    [Header("骨骼设置")]
    [SerializeField] private string waveBoneName = "Bone11";
    [SerializeField] private string waveBoneNameA = "Bone11A";
    [SerializeField] private string lookBoneName = "Bone015";
    
    private Transform waveBone;
    private Transform waveBoneA;
    private Transform lookBone;
    
    private Vector3 waveBoneInitialLocalEuler;
    private Vector3 waveBoneAInitialLocalEuler;
    private Vector3 lookBoneInitialLocalEuler;
    
    [Header("角度设置 - 抬手动作")]
    [SerializeField] private float currentWaveAngleHorizontal = 0f; // 当前抬手水平角度/Y轴角度（度）
    [Range(0f, 180f)]
    [SerializeField] private float currentWaveAngleVertical = 0f; // 当前抬手垂直角度/Z轴角度（0-180度）
    
    [Header("角度设置 - 抬头动作")]
    [SerializeField] private float currentLookAngleHorizontal = 0f; // 当前抬头水平角度/X轴角度（度）
    [Range(0f, 30f)]
    [SerializeField] private float currentLookAngleVertical = 0f; // 当前抬头垂直角度/Y轴角度（0-30度）
    
    [Header("头部朝向设置")]
    [SerializeField] private float headTurnMaxAngle = 180f; // 头部左右扭动最大角度（0-180度）
    [SerializeField] private float headTurnPerceptionRange = 3f; // 感知范围内才会尝试朝向目标（米）
    
    // Human位置信息（用于HeadLookAtHuman动作）
    private Vector3? currentHumanPosition = null; // 当前Human位置
    private bool hasCurrentHumanInfo = false; // 是否有有效的Human信息
    
    // 兼容性：保持向后兼容，但实际使用字段而不是属性
    
    // 动作序列执行相关
    private Coroutine actionSequenceCoroutine = null;
    private bool isExecutingActionSequence = false;
    
    private void Awake()
    {
        InitializeBones();
    }
    
    private void InitializeBones()
    {
        // 查找抬手骨骼
        waveBone = FindChildRecursive(transform, waveBoneName);
        if (waveBone != null)
        {
            waveBoneInitialLocalEuler = waveBone.localEulerAngles;
        }
        else
        {
            Debug.LogWarning($"LimbAnimationController: 未找到抬手骨骼 {waveBoneName}");
        }
        
        waveBoneA = FindChildRecursive(transform, waveBoneNameA);
        if (waveBoneA != null)
        {
            waveBoneAInitialLocalEuler = waveBoneA.localEulerAngles;
        }
        else
        {
            Debug.LogWarning($"LimbAnimationController: 未找到抬手骨骼 {waveBoneNameA}");
        }
        
        // 查找抬头骨骼
        lookBone = FindChildRecursive(transform, lookBoneName);
        if (lookBone != null)
        {
            lookBoneInitialLocalEuler = lookBone.localEulerAngles;
        }
        else
        {
            Debug.LogWarning($"LimbAnimationController: 未找到抬头骨骼 {lookBoneName}");
        }
    }
    
    /// <summary>
    /// 设置抬手角度（0-180度）
    /// </summary>
    /// <param name="angle">角度（0-180度）</param>
    /// <param name="forceChange">是否强制改变，忽略最小角度变化限制</param>
    /// <returns>是否成功设置</returns>
    public bool SetWaveAngle(float angle, bool forceChange = false)
    {
        angle = Mathf.Clamp(angle, 0f, 180f);  
        // 单参数格式：只设置垂直角度，水平角度保持当前值（或设为0）
        currentWaveAngleVertical = angle;
        // 如果水平角度未设置过，保持为0
        // currentWaveAngleHorizontal 保持不变
        
        // 容错：运行时再次查找骨骼
        if (waveBone == null)
        {
            waveBone = FindChildRecursive(transform, waveBoneName);
            if (waveBone != null) waveBoneInitialLocalEuler = waveBone.localEulerAngles;
        }
        
        if (waveBoneA == null)
        {
            waveBoneA = FindChildRecursive(transform, waveBoneNameA);
            if (waveBoneA != null) waveBoneAInitialLocalEuler = waveBoneA.localEulerAngles;
        }
        
        // 设置骨骼角度（只设置Z轴，Y轴保持当前值）
        if (waveBone != null)
        {
            Vector3 e = waveBone.localEulerAngles;
            e.z = angle % 360f;
            // Y轴保持当前值（如果之前设置过）或保持为0
            waveBone.localEulerAngles = e;
        }
        
        if (waveBoneA != null)
        {
            Vector3 e = waveBoneA.localEulerAngles;
            e.z = (360f - angle) % 360f; // 反向
            // Y轴保持当前值（如果之前设置过）或保持为0
            waveBoneA.localEulerAngles = e;
        }
        return true;
    }
    
    /// <summary>
    /// 设置抬头角度（0-180度）
    /// </summary>
    /// <param name="angle">角度（0-180度）</param>
    /// <param name="forceChange">是否强制改变，忽略最小角度变化限制</param>
    /// <returns>是否成功设置</returns>
    public bool SetLookAngle(float angle, bool forceChange = false)
    {
        angle = Mathf.Clamp(angle, 0f, 30f); // 抬头角度范围是0-30度
        // 单参数格式：只设置垂直角度，水平角度保持当前值（或设为0）
        currentLookAngleVertical = angle;
        // 如果水平角度未设置过，保持为0
        // currentLookAngleHorizontal 保持不变
        
        // 容错：运行时再次查找骨骼
        if (lookBone == null)
        {
            lookBone = FindChildRecursive(transform, lookBoneName);
            if (lookBone != null) lookBoneInitialLocalEuler = lookBone.localEulerAngles;
        }
        
        if (lookBone == null)
        {
            Debug.LogWarning($"LimbAnimationController: 未找到抬头骨骼 {lookBoneName}");
            return false;
        }
        
        // 设置骨骼角度（应用时取负，只设置Y轴，X轴保持当前值）
        float targetY = (-angle) % 360f;
        Vector3 e = lookBone.localEulerAngles;
        e.y = targetY < 0 ? targetY + 360f : targetY;
        // X轴保持当前值（如果之前设置过）或保持为0
        lookBone.localEulerAngles = e;
        return true;
    }
    
    /// <summary>
    /// 设置抬手角度（双参数：水平/Y轴和垂直/Z轴）
    /// </summary>
    /// <param name="horizontalAngle">水平角度/Y轴角度（度）</param>
    /// <param name="verticalAngle">垂直角度/Z轴角度（0-180度）</param>
    /// <param name="forceChange">是否强制改变</param>
    /// <returns>是否成功设置</returns>
    public bool SetWaveAngleZY(float horizontalAngle, float verticalAngle, bool forceChange = false)
    {
        verticalAngle = Mathf.Clamp(verticalAngle, 0f, 180f);
        // 更新水平和垂直角度
        currentWaveAngleHorizontal = horizontalAngle;
        currentWaveAngleVertical = verticalAngle;
        
        // 容错：运行时再次查找骨骼
        if (waveBone == null)
        {
            waveBone = FindChildRecursive(transform, waveBoneName);
            if (waveBone != null) waveBoneInitialLocalEuler = waveBone.localEulerAngles;
        }
        
        if (waveBoneA == null)
        {
            waveBoneA = FindChildRecursive(transform, waveBoneNameA);
            if (waveBoneA != null) waveBoneAInitialLocalEuler = waveBoneA.localEulerAngles;
        }
        
        // 设置骨骼角度（Z轴=垂直角度，Y轴=水平角度）
        if (waveBone != null)
        {
            Vector3 e = waveBone.localEulerAngles;
            e.y = horizontalAngle % 360f;
            e.z = verticalAngle % 360f;
            waveBone.localEulerAngles = e;
        }
        
        if (waveBoneA != null)
        {
            Vector3 e = waveBoneA.localEulerAngles;
            e.y = horizontalAngle % 360f;
            e.z = (360f - verticalAngle) % 360f; // 反向
            waveBoneA.localEulerAngles = e;
        }
        
        return true;
    }
    
    /// <summary>
    /// 设置抬头角度（双参数：水平/X轴和垂直/Y轴）
    /// </summary>
    /// <param name="horizontalAngle">水平角度/X轴角度（度）</param>
    /// <param name="verticalAngle">垂直角度/Y轴角度（度，应用时取负）</param>
    /// <param name="forceChange">是否强制改变</param>
    /// <returns>是否成功设置</returns>
    public bool SetLookAngleXY(float horizontalAngle, float verticalAngle, bool forceChange = false)
    {
        verticalAngle = Mathf.Clamp(verticalAngle, 0f, 30f); // 抬头角度范围是0-30度
        // 更新水平和垂直角度
        currentLookAngleHorizontal = horizontalAngle;
        currentLookAngleVertical = verticalAngle;
        
        // 容错：运行时再次查找骨骼
        if (lookBone == null)
        {
            lookBone = FindChildRecursive(transform, lookBoneName);
            if (lookBone != null) lookBoneInitialLocalEuler = lookBone.localEulerAngles;
        }
        
        if (lookBone == null)
        {
            Debug.LogWarning($"LimbAnimationController: 未找到抬头骨骼 {lookBoneName}");
            return false;
        }
        // 设置骨骼角度（X轴=水平角度，Y轴=-垂直角度）
        float targetY = (-verticalAngle) % 360f;
        Vector3 e = lookBone.localEulerAngles;
        e.x = horizontalAngle % 360f;
        e.y = targetY < 0 ? targetY + 360f : targetY;
        lookBone.localEulerAngles = e;
        
        return true;
    }
    
    /// <summary>
    /// 设置当前Human位置（用于HeadLookAtHuman动作）
    /// </summary>
    /// <param name="humanPosition">Human位置，null表示清除</param>
    public void SetCurrentHumanPosition(Vector3? humanPosition)
    {
        currentHumanPosition = humanPosition;
        hasCurrentHumanInfo = (humanPosition != null);
    }
    
    /// <summary>
    /// 原子动作：在感知范围内将头部朝向目标（只考虑水平面），背对时转向目标所在侧并限制最大扭头角
    /// </summary>
    /// <param name="targetPosition">目标世界坐标（例如Human位置）</param>
    /// <param name="reference">参考朝向（默认使用自身transform）</param>
    /// <param name="overrideMaxAngle">可选：自定义最大扭头角（0-180度），为空时使用headTurnMaxAngle</param>
    /// <returns>是否成功执行（未在范围内或找不到骨骼则返回false）</returns>
    public bool LookHeadTowardsTarget(Vector3 targetPosition, Transform reference = null, float? overrideMaxAngle = null)
    {
        if (lookBone == null)
        {
            lookBone = FindChildRecursive(transform, lookBoneName);
            if (lookBone != null) lookBoneInitialLocalEuler = lookBone.localEulerAngles;
        }
        
        Transform refTransform = reference == null ? transform : reference;
        Vector3 origin = refTransform.position;
        
        Vector3 toTarget = targetPosition - origin;
        toTarget.y = 0f; // 只在水平面判断朝向
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return false; // 目标与自身重合
        }
        
        // 超出感知范围则不执行（保持原姿态）
        float maxRange = Mathf.Max(0f, headTurnPerceptionRange);
        if (toTarget.sqrMagnitude > maxRange * maxRange)
        {
            return false;
        }
        
        Vector3 forward = refTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();
        
        Vector3 targetDir = toTarget.normalized;
        float signedAngle = Vector3.SignedAngle(forward, targetDir, Vector3.up); // >0 目标在右侧，<0 目标在左侧
        
        float maxAngle = Mathf.Clamp(overrideMaxAngle ?? headTurnMaxAngle, 0f, 180f);
        
        // 当目标几乎在身后时，按照目标所在侧直接扭到最大允许角度
        bool isBehind = Vector3.Dot(forward, targetDir) < -0.8f;
        if (isBehind)
        {
            signedAngle = Mathf.Sign(signedAngle) * maxAngle;
        }
        
        float clampedAngle = Mathf.Clamp(signedAngle, -maxAngle, maxAngle);
        
        // 记录当前水平扭头角度（正右负左），并将其映射到骨骼的X轴
        currentLookAngleHorizontal = clampedAngle;
        Vector3 e = lookBone.localEulerAngles;
        e.x = NormalizeAngle(clampedAngle);
        lookBone.localEulerAngles = e;
        return true;
    }
    
    /// <summary>
    /// 同时设置抬手和抬头角度
    /// </summary>
    /// <param name="waveAngle">抬手角度（0-180度）</param>
    /// <param name="lookAngle">抬头角度（0-180度）</param>
    /// <param name="forceChange">是否强制改变，忽略最小角度变化限制</param>
    /// <returns>是否成功设置</returns>
    public bool SetLimbAngles(float waveAngle, float lookAngle, bool forceChange = false)
    {
        bool waveSuccess = SetWaveAngle(waveAngle, forceChange);
        bool lookSuccess = SetLookAngle(lookAngle, forceChange);
        
        return waveSuccess || lookSuccess; // 只要有一个成功就返回true
    }
    
    /// <summary>
    /// 获取当前抬手角度（兼容旧代码，返回垂直角度）
    /// </summary>
    public float GetWaveAngle()
    {
        return currentWaveAngleVertical;
    }
    
    /// <summary>
    /// 获取当前抬头角度（兼容旧代码，返回垂直角度）
    /// </summary>
    public float GetLookAngle()
    {
        return currentLookAngleVertical;
    }
    
    /// <summary>
    /// 获取当前抬手水平角度（Y轴）
    /// </summary>
    public float GetWaveAngleHorizontal()
    {
        return currentWaveAngleHorizontal;
    }
    
    /// <summary>
    /// 获取当前抬手垂直角度（Z轴）
    /// </summary>
    public float GetWaveAngleVertical()
    {
        return currentWaveAngleVertical;
    }
    
    /// <summary>
    /// 获取当前抬头水平角度（X轴）
    /// </summary>
    public float GetLookAngleHorizontal()
    {
        return currentLookAngleHorizontal;
    }
    
    /// <summary>
    /// 获取当前抬头垂直角度（Y轴）
    /// </summary>
    public float GetLookAngleVertical()
    {
        return currentLookAngleVertical;
    }
    
    /// <summary>
    /// 恢复初始姿态
    /// </summary>
    public void ResetToInitialPose()
    {
        if (waveBone != null)
        {
            waveBone.localEulerAngles = waveBoneInitialLocalEuler;
        }
        if (waveBoneA != null)
        {
            waveBoneA.localEulerAngles = waveBoneAInitialLocalEuler;
        }
        if (lookBone != null)
        {
            lookBone.localEulerAngles = lookBoneInitialLocalEuler;
        }
        // 重置所有角度
        currentWaveAngleHorizontal = 0f;
        currentWaveAngleVertical = 0f;
        currentLookAngleHorizontal = 0f;
        currentLookAngleVertical = 0f;
    }
    
    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }
    
    /// <summary>
    /// 执行连续的挥手动作（垂直角度从0度到100度，4个来回，时长由duration控制）
    /// </summary>
    /// <param name="duration">动作持续时间（秒），越小速度越快，越大速度越慢</param>
    /// <param name="onComplete">完成回调（可选）</param>
    /// <returns>协程</returns>
    public Coroutine ExecuteContinuousWave(float duration = 2f, System.Action onComplete = null)
    {
        // 停止之前的动作序列
        StopActionSequence();
        
        // 启动连续挥手协程
        actionSequenceCoroutine = StartCoroutine(ContinuousWaveCoroutine(duration, onComplete));
        return actionSequenceCoroutine;
    }
    
    /// <summary>
    /// 连续挥手动作协程
    /// </summary>
    /// <param name="duration">动作持续时间（秒）</param>
    /// <param name="onComplete">完成回调</param>
    private IEnumerator ContinuousWaveCoroutine(float duration, System.Action onComplete)
    {
        isExecutingActionSequence = true;
        
        const int cycles = 4; // 4个来回
        float totalDuration = Mathf.Max(0.1f, duration); // 总时长由参数控制，避免除0
        float cycleDuration = totalDuration / cycles; // 每个来回的时长
        float halfCycleDuration = cycleDuration / 2f; // 每个方向的时长
        const float minAngle = 0f; // 最小角度0度
        const float maxAngle = 100f; // 最大角度100度
        
        // 保存当前水平角度，只改变垂直角度
        float savedHorizontalAngle = currentWaveAngleHorizontal;
        
        // 容错：运行时再次查找骨骼
        if (waveBone == null)
        {
            waveBone = FindChildRecursive(transform, waveBoneName);
            if (waveBone != null) waveBoneInitialLocalEuler = waveBone.localEulerAngles;
        }
        
        if (waveBoneA == null)
        {
            waveBoneA = FindChildRecursive(transform, waveBoneNameA);
            if (waveBoneA != null) waveBoneAInitialLocalEuler = waveBoneA.localEulerAngles;
        }
        
        if (waveBone == null || waveBoneA == null)
        {
            Debug.LogWarning("LimbAnimationController: 未找到挥手骨骼，无法执行连续挥手动作");
            isExecutingActionSequence = false;
            onComplete?.Invoke();
            yield break;
        }
        
        // 从当前垂直角度开始
        float startAngle = currentWaveAngleVertical;
        
        // 执行4个来回
        for (int i = 0; i < cycles; i++)
        {
            // 第一个半周期：从当前角度到最大角度（或从最小角度到最大角度）
            float fromAngle = (i == 0) ? startAngle : minAngle;
            float toAngle = maxAngle;
            
            float elapsed = 0f;
            while (elapsed < halfCycleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfCycleDuration);
                // 使用平滑插值（ease in-out）
                t = t * t * (3f - 2f * t); // Smoothstep
                float currentAngle = Mathf.Lerp(fromAngle, toAngle, t);
                
                // 更新角度（保持水平角度不变）
                SetWaveAngleZY(savedHorizontalAngle, currentAngle, true);
                
                yield return null;
            }
            
            // 确保到达最大角度
            SetWaveAngleZY(savedHorizontalAngle, maxAngle, true);
            
            // 第二个半周期：从最大角度回到最小角度
            elapsed = 0f;
            while (elapsed < halfCycleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfCycleDuration);
                // 使用平滑插值
                t = t * t * (3f - 2f * t); // Smoothstep
                float currentAngle = Mathf.Lerp(maxAngle, minAngle, t);
                
                // 更新角度（保持水平角度不变）
                SetWaveAngleZY(savedHorizontalAngle, currentAngle, true);
                
                yield return null;
            }
            
            // 确保回到最小角度
            SetWaveAngleZY(savedHorizontalAngle, minAngle, true);
        }
        
        // 动作完成
        isExecutingActionSequence = false;
        actionSequenceCoroutine = null;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// 执行动作序列（支持时间插值）
    /// </summary>
    /// <param name="sequence">动作序列</param>
    /// <param name="onActionStart">动作开始回调（参数：动作类型，是否需要暂停导航）</param>
    /// <param name="onActionEnd">动作结束回调（参数：动作类型）</param>
    /// <returns>协程</returns>
    public Coroutine ExecuteActionSequence(ActionSequence sequence, System.Action<ActionType, bool> onActionStart = null, System.Action<ActionType> onActionEnd = null)
    {
        // 停止之前的动作序列
        StopActionSequence();
        
        if (sequence == null || sequence.actions.Count == 0)
            return null;
        
        actionSequenceCoroutine = StartCoroutine(ExecuteActionSequenceCoroutine(sequence, onActionStart, onActionEnd));
        return actionSequenceCoroutine;
    }
    
    /// <summary>
    /// 停止当前动作序列
    /// </summary>
    public void StopActionSequence()
    {
        if (actionSequenceCoroutine != null)
        {
            StopCoroutine(actionSequenceCoroutine);
            actionSequenceCoroutine = null;
        }
        isExecutingActionSequence = false;
    }
    
    /// <summary>
    /// 检查是否正在执行动作序列
    /// </summary>
    public bool IsExecutingActionSequence()
    {
        return isExecutingActionSequence;
    }
    
    /// <summary>
    /// 动作序列执行协程
    /// </summary>
    private IEnumerator ExecuteActionSequenceCoroutine(ActionSequence sequence, System.Action<ActionType, bool> onActionStart, System.Action<ActionType> onActionEnd)
    {
        isExecutingActionSequence = true;
        
        foreach (var action in sequence.actions)
        {
            // 通知动作开始（停留动作需要暂停导航）
            bool shouldPauseNavigation = (action.type == ActionType.Wait);
            onActionStart?.Invoke(action.type, shouldPauseNavigation);
            
            float startTime = Time.time;
            float startWaveAngle = currentWaveAngleVertical;
            float startLookAngle = currentLookAngleVertical;
            
            switch (action.type)
            {
                case ActionType.Wait:
                    // 停留：保持当前角度不变
                    yield return new WaitForSeconds(action.duration);
                    break;
                    
                case ActionType.Wave:
                    // 抬手：支持单参数和双参数
                    if (action.targetValue2 != 0f)
                    {
                        // 双参数：水平/Y轴和垂直/Z轴
                        // 使用当前存储的角度作为起始值
                        float startHorizontal = currentWaveAngleHorizontal;
                        float startVertical = currentWaveAngleVertical;
                        float targetHorizontal = action.targetValue;
                        float targetVertical = action.targetValue2;
                        
                        while (Time.time - startTime < action.duration)
                        {
                            float t = (Time.time - startTime) / action.duration;
                            t = Mathf.Clamp01(t);
                            float currentHorizontal = Mathf.LerpAngle(startHorizontal, targetHorizontal, t);
                            float currentVertical = Mathf.Lerp(startVertical, targetVertical, t);
                            SetWaveAngleZY(currentHorizontal, currentVertical, true);
                            yield return null;
                        }
                        // 确保最终角度准确
                        SetWaveAngleZY(targetHorizontal, targetVertical, true);
                    }
                    else
                    {
                        // 单参数：只设置垂直/Z轴角度（兼容旧格式）
                        while (Time.time - startTime < action.duration)
                        {
                            float t = (Time.time - startTime) / action.duration;
                            t = Mathf.Clamp01(t);
                            float currentAngle = Mathf.Lerp(startWaveAngle, action.targetValue, t);
                            SetWaveAngle(currentAngle, true);
                            yield return null;
                        }
                        // 确保最终角度准确
                        SetWaveAngle(action.targetValue, true);
                    }
                    break;
                    
                case ActionType.Look:
                    // 抬头：支持单参数和双参数
                    if (action.targetValue2 != 0f)
                    {
                        // 双参数：水平/X轴和垂直/Y轴
                        // 使用当前存储的角度作为起始值
                        float startHorizontal = currentLookAngleHorizontal;
                        float startVertical = currentLookAngleVertical;
                        float targetHorizontal = action.targetValue;
                        float targetVertical = action.targetValue2;
                        
                        while (Time.time - startTime < action.duration)
                        {
                            float t = (Time.time - startTime) / action.duration;
                            t = Mathf.Clamp01(t);
                            float currentHorizontal = Mathf.LerpAngle(startHorizontal, targetHorizontal, t);
                            float currentVertical = Mathf.Lerp(startVertical, targetVertical, t);
                            SetLookAngleXY(currentHorizontal, currentVertical, true);
                            yield return null;
                        }
                        // 确保最终角度准确
                        SetLookAngleXY(targetHorizontal, targetVertical, true);
                    }
                    else
                    {
                        // 单参数：只设置垂直/Y轴角度（兼容旧格式）
                        while (Time.time - startTime < action.duration)
                        {
                            float t = (Time.time - startTime) / action.duration;
                            t = Mathf.Clamp01(t);
                            float currentAngle = Mathf.Lerp(startLookAngle, action.targetValue, t);
                            SetLookAngle(currentAngle, true);
                            yield return null;
                        }
                        // 确保最终角度准确
                        SetLookAngle(action.targetValue, true);
                    }
                    break;
                    
                case ActionType.ContinuousWave:
                    // 连续挥手：垂直角度从0度到100度，4个来回
                    // 根据duration调整来回次数和速度
                    yield return StartCoroutine(ContinuousWaveInSequence(action.duration));
                    break;
                    
                case ActionType.HeadLookAtHuman:
                    // 头部朝向Human：在感知范围内自动朝向Human位置，背对时转向靠近人的一侧
                    // 在duration时间内持续朝向Human位置（每帧更新）
                    if (hasCurrentHumanInfo && currentHumanPosition != null)
                    {
                        float elapsed = 0f;
                        while (elapsed < action.duration)
                        {
                            LookHeadTowardsTarget(currentHumanPosition.Value, transform, headTurnMaxAngle);
                            elapsed += Time.deltaTime;
                            yield return null;
                        }
                        // 最后一次确保朝向准确
                        LookHeadTowardsTarget(currentHumanPosition.Value, transform, headTurnMaxAngle);
                    }
                    else
                    {
                        // 如果没有Human信息，直接等待duration时间
                        Debug.LogWarning("LimbAnimationController: HeadLookAtHuman动作执行时没有Human位置信息");
                        yield return new WaitForSeconds(action.duration);
                    }
                    break;
            }
            
            // 通知动作结束
            onActionEnd?.Invoke(action.type);
        }
        
        isExecutingActionSequence = false;
        actionSequenceCoroutine = null;
    }
    
    /// <summary>
    /// 在动作序列中执行连续挥手（根据duration调整）
    /// </summary>
    private IEnumerator ContinuousWaveInSequence(float duration)
    {
        const int cycles = 4; // 4个来回
        const float minAngle = 0f; // 最小角度0度
        const float maxAngle = 100f; // 最大角度100度
        float cycleDuration = duration / cycles; // 每个来回的时长
        float halfCycleDuration = cycleDuration / 2f; // 每个方向的时长
        
        // 保存当前水平角度，只改变垂直角度
        float savedHorizontalAngle = currentWaveAngleHorizontal;
        
        // 容错：运行时再次查找骨骼
        if (waveBone == null)
        {
            waveBone = FindChildRecursive(transform, waveBoneName);
            if (waveBone != null) waveBoneInitialLocalEuler = waveBone.localEulerAngles;
        }
        
        if (waveBoneA == null)
        {
            waveBoneA = FindChildRecursive(transform, waveBoneNameA);
            if (waveBoneA != null) waveBoneAInitialLocalEuler = waveBoneA.localEulerAngles;
        }
        
        if (waveBone == null || waveBoneA == null)
        {
            Debug.LogWarning("LimbAnimationController: 未找到挥手骨骼，无法执行连续挥手动作");
            yield break;
        }
        
        // 从当前垂直角度开始
        float startAngle = currentWaveAngleVertical;
        
        // 执行4个来回
        for (int i = 0; i < cycles; i++)
        {
            // 第一个半周期：从当前角度到最大角度（或从最小角度到最大角度）
            float fromAngle = (i == 0) ? startAngle : minAngle;
            float toAngle = maxAngle;
            
            float elapsed = 0f;
            while (elapsed < halfCycleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfCycleDuration);
                // 使用平滑插值（ease in-out）
                t = t * t * (3f - 2f * t); // Smoothstep
                float currentAngle = Mathf.Lerp(fromAngle, toAngle, t);
                
                // 更新角度（保持水平角度不变）
                SetWaveAngleZY(savedHorizontalAngle, currentAngle, true);
                
                yield return null;
            }
            
            // 确保到达最大角度
            SetWaveAngleZY(savedHorizontalAngle, maxAngle, true);
            
            // 第二个半周期：从最大角度回到最小角度
            elapsed = 0f;
            while (elapsed < halfCycleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfCycleDuration);
                // 使用平滑插值
                t = t * t * (3f - 2f * t); // Smoothstep
                float currentAngle = Mathf.Lerp(maxAngle, minAngle, t);
                
                // 更新角度（保持水平角度不变）
                SetWaveAngleZY(savedHorizontalAngle, currentAngle, true);
                
                yield return null;
            }
            
            // 确保回到最小角度
            SetWaveAngleZY(savedHorizontalAngle, minAngle, true);
        }
    }
    
    /// <summary>
    /// 递归查找子物体
    /// </summary>
    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

