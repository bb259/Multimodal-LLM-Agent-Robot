using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// LLM智能代理：整合环境感知、LLM决策、导航系统和肢体动作控制
/// 使用合并的prompt，在一次LLM调用中同时处理导航和肢体动作决策
/// </summary>
public class LLMAgent : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private DSConnect llmConnect;
    [SerializeField] private EnvironmentLogger environmentLogger;
    [SerializeField] private NavigationController navigationController;
    [SerializeField] private LimbAnimationController limbController; // 肢体动画控制器
    [SerializeField] private HumanFovVisualizer humanFovVisualizer;
    
    [Header("LLM设置")]
    [SerializeField] private bool autoRequestDecision = false; // 是否自动请求决策
    [SerializeField] private float llmResponseTime = 0.01f; // LLM响应时间（秒），用于等待响应完成
    [SerializeField] private float minMoveSeparation = 0.3f; // 最小移动距离阈值（米），避免目标过近不移动
    [SerializeField] private bool enableFovConstraint = true; // 是否启用视场约束
    [Header("提示词设置")]
    [TextArea(12, 30)]
    [SerializeField] private string combinedSystemPrompt = @"你是一只温顺黏人的小狗机器人，喜欢贴近主人但也会判断安全距离。你会根据环境感知信息同时决定移动目标位置(x,y,z)与两种原子动作：抬手(狗狗挥手)与抬头(仰头/低头)。  
=== 角色/性格设定 ===
- 你把所有 Human 当作主人，默认愿意靠近、互动。
- 当主人状态为 Interactable 时，你更主动地靠近、热情摇摆(抬手、抬头角度变化大),可以选择视场内或者视场外方案。
- 当主人状态为 NonInteractable 时，先在脑内构思至少3种策略（如：留在原地观察、在附近绕圈等），再随机抽一条作为最终行动；需保持安全与友好。
=== 原子动作定义 ===
- 抬手动作（wave）：优先使用双参数格式以获得更丰富的动作表现
  * 双参数格式（推荐）：wave:horizontal:vertical:duration，horizontal为水平角度（Y轴，度），vertical为垂直角度（Z轴，0-180度）
  * 单参数格式（简化）：wave:angle:duration，angle为垂直角度（Z轴，0-180度），水平角度默认为0
- 抬头动作（look）：优先使用双参数格式以获得更丰富的动作表现
  * 双参数格式（推荐）：look:horizontal:vertical:duration，horizontal为水平角度（X轴，度），vertical为垂直角度（Y轴，0-30度，应用时取负）
  * 单参数格式（简化）：look:angle:duration，angle为垂直角度（Y轴，0-30度，应用时取负），水平角度默认为0
- 连续挥手动作（continuous_wave）：热情友好的连续挥手动作
  * 格式：continuous_wave:duration，执行4个来回，垂直角度从0度到100度，duration为动作持续时间（秒），duration越小，挥手越快；duration越大，挥手越慢
  * 重要：duration必须根据情境和情绪变化！不要总是使用相同的值（如总是3.0秒）！
  * 当你想表现非常兴奋、急促地向主人打招呼时，使用较短的duration（1.0-2.0秒，快速挥手）
  * 正常友好互动时，使用中等duration（2.5-3.5秒，中等速度）
  * 想要稍微慢一点、悠闲地挥手时，使用较长的duration（3.5-5.0秒，慢速挥手）
  * 根据主人的状态（Interactable/NonInteractable）、距离远近、当前情绪等因素，灵活选择不同的duration值
  * 这是一个非常适合向主人表达热情和友好的动作，建议在需要表现热情时使用
- 头部朝向Human动作（head_look_at_human）：当感知范围内有人物标签时，自动让头部朝向对方
  * 格式：head_look_at_human:duration，duration为动作持续时间（秒）
  * 当人物在身后时，头部会转向靠近人的一侧（扭到最大允许角度，范围0-180度）
  * 这是一个非常适合表达关注和互动的动作，建议在需要朝向主人时使用
- 重要：为了表现更生动自然的宠物行为，建议优先使用双参数格式和连续挥手动作，这样可以同时控制水平和垂直方向的角度变化，让动作更加丰富多样。
- 角度使用要求：
  * 抬手角度（vertical）：建议使用较大的角度（60-150度），表现热情友好的动作。小角度（0-30度）显得不够活跃。
  * 抬头角度（vertical）：建议使用较大的角度（15-30度），表现积极关注。小角度（0-10度）显得不够明显。
  * 水平角度：可以根据需要设置，建议在-45到+45度之间，增加动作的丰富性。
- 输出的角度必须要严格符合范围要求！同时保持宠物友好风格。
=== 导航与决策 ===
- 输出前务必先在脑内生成 2-3 个不同方案（靠近、停留、绕行等），然后随机挑选其中一个作为最终输出。
- Interactable 场景下，必须遵守视场约束（详见用户消息中的详细说明和数学公式）。
- NonInteractable：根据随机抽到的方案灵活行动，可留在附近、缓慢远离或转向其他兴趣点。
- 无 Human：自由探索，动作温和。
- 你必须根据环境决定移动速度（speed）：当环境中检测到 Human 时，整体速度应偏慢、更谨慎；当环境中没有 Human 时，可以适当加快探索速度但保持安全。
- 下面的速度策略是**强制规则**，你在每一次输出前都必须先检查自己打算输出的 speed 是否落在指定区间内，若不在必须立刻调整到最近的合法数值后再输出：
  * 有可交互 Human（Interactable）：slow 到 normal（例如 speed = 0.8~1.2），体现贴近主人、动作柔和、不急躁。你绝对不能在此场景下输出小于 0.8 或大于 1.2 的 speed（例如 0.5、1.5、2.0 都是错误的，必须改成 0.8~1.2 范围内的值）。
  * 有不可交互 Human（NonInteractable）：更慢一些（例如 speed = 0.5~0.8），表现为小心、礼貌地远离或绕行。你绝对不能在此场景下输出小于 0.5 或大于 0.8 的 speed（例如 0.3、1.0、1.5 都是错误的，必须改成 0.5~0.8 范围内的值）。
  * 环境中没有 Human：可以稍微快一点进行探索（例如 speed = 1.2~2.0），但不要使用极端过大的速度。你绝对不能在此场景下输出小于 1.2 或大于 2.0 的 speed（例如 0.8、1.0、2.5 都是错误的，必须改成 1.2~2.0 范围内的值）。
=== 输出格式 ===
输出两行：
第1行：说明你的决策理由（例如 ""靠近可交互主人，抬爪打招呼""）。如果涉及视场约束，必须明确写出""视场内""或""视场外""。
第2行：x,y,z,speed,action_sequence,path_type
其中 speed 为导航速度系数（建议范围 0.5-2.0）；action_sequence 为动作序列，格式：wait:duration,wave:angle:duration或wave:horizontal:vertical:duration,look:angle:duration或look:horizontal:vertical:duration,continuous_wave:duration,head_look_at_human:duration（时间总和必须严格等于5秒）
支持的动作类型：wait（停留）、wave（抬手）、look（抬头）、continuous_wave（连续挥手）、head_look_at_human（头部朝向Human）
path_type 为路径类型：straight（直线）、scurve（S形曲线）。根据情境选择：直接快速移动选择straight；需要绕过障碍物或表现更自然的移动选择scurve；";
    private bool isProcessingDecision = false;//是否正在处理决策
	
	// 记忆系统：存储前5次LLM决策理由
	private Queue<string> decisionMemory = new Queue<string>();
	private const int MAX_MEMORY_COUNT = 5;
    private Coroutine decisionCoroutine;
    private float requestStartTime = 0f; // 记录请求开始的时间
    
    
    // 当前Human信息（用于视场约束）
    private Vector3 currentHumanPosition = Vector3.zero;
    private Vector3 currentHumanForward = Vector3.forward;
    private bool currentHumanIsInteractable = false;
    private bool hasCurrentHumanInfo = false; // 是否有有效的Human信息
    private void Awake()
    {
        // 自动查找组件（如果Inspector中未设置）
        if (llmConnect == null)
            llmConnect = GetComponent<DSConnect>();
        if (environmentLogger == null)
            environmentLogger = GetComponent<EnvironmentLogger>();
        if (navigationController == null)
            navigationController = GetComponent<NavigationController>();
        if (limbController == null)
            limbController = GetComponent<LimbAnimationController>();
        // 初始化并更新LLM的系统提示词
        if (llmConnect != null)
        {
            // 如果 npcCharacter 为 null，创建一个新的实例
            if (llmConnect.npcCharacter == null)
            {
                llmConnect.npcCharacter = new DSConnect.NPCCharacter();
            }
            // 使用合并的系统提示词（包含导航和肢体动作）
            llmConnect.npcCharacter.personalityPrompt = combinedSystemPrompt;
        }
    }
    private void Start()
    {
        StartCoroutine(DelayedStart());
    }
    private IEnumerator DelayedStart()
    {
        // 等待EnvironmentLogger完成初始化（至少等待第一次扫描）
        yield return new WaitForSeconds(0.5f);
        
        if (autoRequestDecision)
        {
            StartAutoDecision();
        }
    }
    /// <summary>
    /// 解析导航速度系数 speed（第4个字段，可选）
    /// 格式：x,y,z,speed,action_sequence,path_type
    /// 如果缺失或解析失败，则返回 1.0（默认速度）
    /// </summary>
    private float ParseNavigationSpeedScale(string text)
    {
        if (string.IsNullOrEmpty(text)) return 1f;

        string firstLine = text.Trim();
        if (string.IsNullOrEmpty(firstLine)) return 1f;

        string[] parts = firstLine.Split(',');
        if (parts.Length >= 4 && float.TryParse(parts[3].Trim(), out float scale))
        {
            // 做一个简单的安全限制，避免极端值
            return Mathf.Clamp(scale, 0.2f, 5f);
        }

        return 1f;
    }

    /// <summary>
    /// 开始自动决策循环
    /// </summary>
    public void StartAutoDecision()
    {
        if (decisionCoroutine != null)
        {
            StopCoroutine(decisionCoroutine);
        }
        decisionCoroutine = StartCoroutine(AutoDecisionLoop());
    }
    /// <summary>
    /// 停止自动决策循环
    /// </summary>
    public void StopAutoDecision()
    {
        if (decisionCoroutine != null)
        {
            StopCoroutine(decisionCoroutine);
            decisionCoroutine = null;
        }
    }
    /// <summary>
    /// 自动决策循环（合并模式：导航+肢体动作）
    /// </summary>
    private IEnumerator AutoDecisionLoop()
    {
        yield return new WaitForSeconds(0.5f);
        while (true)
        {
			if (!isProcessingDecision)
            {
                RequestLLMDecision();
            }
            yield return new WaitForSeconds(llmResponseTime);
        }
    }
    
    /// <summary>
    /// 手动请求LLM决策（合并导航和肢体动作）
    /// </summary>
    public void RequestLLMDecision()
    {
        // 获取当前环境信息
        string environmentInfo = environmentLogger.GetLatestEnvironmentInfo();
        Vector3 currentPosition = environmentLogger.GetLatestScanPosition();
        // 检查环境信息是否为空（但如果只是没有物体，应该继续处理）
        if (string.IsNullOrEmpty(environmentInfo))
        {
            isProcessingDecision = false;
            return;
        }

        // 构建用户消息（包含导航和肢体动作信息）
        StringBuilder userMessage = new StringBuilder();
        
        // 添加历史记忆内容（如果有）
        if (decisionMemory.Count > 0)
        {
            userMessage.AppendLine("=== 历史决策记忆（请参考以下前5次的决策理由，以保持行为连贯性）===");
            // 将队列转换为列表以便按顺序显示
            List<string> memoryList = new List<string>(decisionMemory);
            // 从旧到新显示记忆（最旧的在前面，最新的在后面）
            for (int i = 0; i < memoryList.Count; i++)
            {
                int stepNumber = memoryList.Count - i; // 最旧的显示为"第5次"，最新的显示为"第1次"
                userMessage.AppendLine($"前{stepNumber}次决策理由: {memoryList[i]}");
            }
            userMessage.AppendLine("重要提示：请参考上述历史决策，当上述历史决策包含有3次及以上相同决策时，请立即变换决策，不要重复执行相同的决策。");
            userMessage.AppendLine("=== 当前环境信息 ===");
        }
        
        userMessage.AppendLine($"当前游戏对象位置: ({currentPosition.x:F2}, {currentPosition.y:F2}, {currentPosition.z:F2})");
        userMessage.AppendLine($"环境感知信息：");
        userMessage.AppendLine(environmentInfo);
        // 检查是否有物体
        bool hasObjects = environmentInfo.Contains("数量=") && !environmentInfo.Contains("数量=0");
        string envInfoLower = environmentInfo.ToLower();
        string[] lines=environmentInfo.Split('\n');
        bool hasHuman = envInfoLower.Contains("human");
        bool isHumanInteractable = false; // Human物体是否可交互
        bool hasHumanState = false; // 是否成功解析到Human状态
        float fovAngle = 90f; // 默认值
        float fovMaxDistance = 2f; // 默认值
        if (humanFovVisualizer != null)
        {
            fovAngle = humanFovVisualizer.FovAngle;
            fovMaxDistance = humanFovVisualizer.FovMaxDistance;
        }
        
        Vector3 humanPosition = Vector3.zero; // Human物体的位置
        bool hasHumanPosition = false; // 是否成功解析到Human位置
        Vector3 humanForward = Vector3.forward; // Human物体的朝向（正z轴方向）
        bool hasHumanForward = false; // 是否成功解析到Human朝向
        // 解析Human物体的状态和位置信息
        foreach(string line in lines)
        {
            // 解析Human状态
            if(line.Contains("Human") && line.Contains("状态="))
            {
                if(line.Contains("NonInteractable"))
                {
                    isHumanInteractable = false;
                    hasHumanState = true;
                }
                else
                {
                    isHumanInteractable = true;
                    hasHumanState = true;
                }
            }
            // 解析Human位置
            if(line.Contains("Human") && line.Contains("at ("))
            {
                Regex positionRegex = new Regex(@"at\s*\(([-\d.]+),([-\d.]+),([-\d.]+)\)");
                Match posMatch = positionRegex.Match(line);
                if(posMatch.Success)
                {
                    humanPosition.x = float.Parse(posMatch.Groups[1].Value);
                    humanPosition.y = float.Parse(posMatch.Groups[2].Value);
                    humanPosition.z = float.Parse(posMatch.Groups[3].Value);
                    hasHumanPosition = true;
                }
            }
            // 解析Human朝向
            if(line.Contains("Human") && line.Contains("朝向=("))
            {
                Regex forwardRegex = new Regex(@"朝向=\(([-\d.]+),([-\d.]+),([-\d.]+)\)");
                Match forwardMatch = forwardRegex.Match(line);
                if(forwardMatch.Success)
                {
                    humanForward.x = float.Parse(forwardMatch.Groups[1].Value);
                    humanForward.y = float.Parse(forwardMatch.Groups[2].Value);
                    humanForward.z = float.Parse(forwardMatch.Groups[3].Value);
                    // 归一化朝向向量
                    if(humanForward.sqrMagnitude > 0.001f)
                    {
                        humanForward.Normalize();
                        hasHumanForward = true;
                    }
                }
            }
            
        }
        
        // 保存当前Human信息供后续使用
        if (hasHuman && hasHumanState && hasHumanPosition && hasHumanForward)
        {
            currentHumanPosition = humanPosition;
            currentHumanForward = humanForward;
            currentHumanIsInteractable = isHumanInteractable;
            hasCurrentHumanInfo = true;
        }
        else
        {
            hasCurrentHumanInfo = false;
        }
        
        if (hasObjects)
        {
            // 导航相关提示
            if (hasHuman && hasHumanState)
            {
                if (isHumanInteractable)
                {
                    // Human可交互：必须靠近
                    userMessage.AppendLine("重要：环境中有Human标签物体，状态为Interactable（可交互）。目标位置必须比当前位置更接近该Human物体的位置坐标。");
                    if (hasHumanPosition)
                    {
                        float distanceToHuman = Vector3.Distance(currentPosition, humanPosition);
                        userMessage.AppendLine($"Human物体位置: ({humanPosition.x:F2}, {humanPosition.y:F2}, {humanPosition.z:F2})，当前距离: {distanceToHuman:F2}米");
                        userMessage.AppendLine($"目标位置可以是Human物体附近的某个位置（距离Human物体1-2米的位置），或者是从当前位置向Human物体移动50%-80%距离后的位置。");
                    }
                    
                }
                else
                {
                    // Human不可交互：必须远离
                    userMessage.AppendLine("重要：环境中有Human标签物体，状态为NonInteractable（不可交互）。");
                    if(hasHumanPosition)
                    {
                        float distanceToHuman=Vector3.Distance(currentPosition, humanPosition);
                        userMessage.AppendLine($"Human（主人）位置: ({humanPosition.x:F2}, {humanPosition.y:F2}, {humanPosition.z:F2})");
                        userMessage.AppendLine($"小狗当前位置: ({currentPosition.x:F2}, {currentPosition.y:F2}, {currentPosition.z:F2})");
                        userMessage.AppendLine($"当前与主人的距离: {distanceToHuman:F2}米");
                        userMessage.AppendLine("请你先在脑海中构思至少 2-3 种不同的行动方案（例如：在当前安全距离（0.5米）附近停留观察、在附近绕一小圈、轻微远离到更安全位置等），");
                        userMessage.AppendLine("然后在这些方案中随机抽取 1 个作为最终要执行的目标位置和动作。无需强制远离很远，但要确保对主人安全、友好。");
                    }
                    
                    else
                    {
                        userMessage.AppendLine("无法精确获取主人位置，请根据环境信息自行构思 2-3 种安全方案，并随机选择其中 1 个执行。");
                    }
                }
            }
            else if (hasHuman && !hasHumanState)
            {
                // 有Human但没有状态信息，默认按可交互处理（保持原有逻辑）
                userMessage.AppendLine("重要：环境中有Human标签物体（状态未知）。目标位置必须比当前位置更接近该Human物体的位置坐标。");
            }
            // 肢体动作相关提示
            if (hasHuman && hasHumanState)
            {
                if (isHumanInteractable)
                {
                    userMessage.AppendLine("重要：环境中检测到 Human（主人），当前为可交互状态。请根据主人的位置、距离和方向，决定小狗抬爪和抬头的角度。");
                    userMessage.AppendLine("角度要求：");
                    userMessage.AppendLine("  - 抬手角度（vertical）：必须使用较大的角度（建议60-150度），表现热情友好的动作。禁止使用小角度（小于40度），那样显得不够活跃！");
                    userMessage.AppendLine("  - 抬头角度（vertical）：必须使用较大的角度（建议20-30度），表现积极关注。禁止使用小角度（小于15度），那样显得不够明显！");
                    userMessage.AppendLine("  - 水平角度：建议设置在10-45度之间，增加动作的丰富性和表现力。");
                    userMessage.AppendLine("  - 抬头范围在[0,30]，抬手范围在[0,180]，输出角度必须严格符合范围要求！");
                }
                else
                {
                    userMessage.AppendLine("重要：环境中检测到Human标签物体（不可交互状态）。可以使用较小的肢体角度（如0-30度）或保持当前角度，避免过度互动。");
                }
            }
            else if (hasHuman)
            {
                userMessage.AppendLine("重要：环境中检测到Human标签物体。请根据Human物体的位置、距离和方向，合理决策抬手和抬头角度。");
                userMessage.AppendLine("角度要求：");
                userMessage.AppendLine("  - 抬手角度（vertical）：建议使用较大的角度（50-120度），表现友好。避免使用过小的角度（小于30度）。");
                userMessage.AppendLine("  - 抬头角度（vertical）：建议使用较大的角度（18-30度），表现关注。避免使用过小的角度（小于12度）。");
                userMessage.AppendLine("  - 水平角度：建议设置在5-40度之间。");
                userMessage.AppendLine("  - 抬头范围在[0,30]，抬手范围在[0,180]，输出角度必须严格符合范围要求！");
            }
            else
            {
                userMessage.AppendLine("重要：环境中没有Human标签物体。可以使用较小的肢体角度（如0-30度）或保持当前角度。");
            }
        }
        else
        {
            userMessage.AppendLine("重要：当前环境中没有检测到标签物体。请选择一个合适的方向，移动1-2米距离。输出目标位置坐标，并使用较小的肢体角度。");
        }
        // ===== 输出格式说明（增加\"理由行 + 数据行\"，并加入导航速度系数 speed；速度必须严格遵守上文给出的区间规则）=====
        userMessage.AppendLine("现在开始，你必须输出两行内容：");
        userMessage.AppendLine("第1行：用不超过30个汉字简要说明你的决策理由（例如：靠近可交互主人，抬爪打招呼）。不要包含任何坐标或角度数值。");
        userMessage.AppendLine("第2行：仅输出一行 x,y,z,speed,action_sequence,path_type（用英文逗号分隔，不要带括号和文字）。");
        userMessage.AppendLine("其中 speed 为导航速度系数：你必须先根据是否存在 Human 以及 Human 的可交互状态，选择对应的合法区间（Interactable: 0.8~1.2；NonInteractable: 0.5~0.8；无 Human: 1.2~2.0），然后在该区间内选择一个数值输出。绝对不能输出这些区间以外的 speed。");
        userMessage.AppendLine("在正式给出第2行之前，你需要在脑中自检一次 speed 是否在对应区间内：如果不在，你必须立刻把它改成最近的合法数值（例如 1.5 改成 1.2 或 2.0，0.3 改成 0.5），然后再输出。");
        userMessage.AppendLine("=== 动作序列格式（action_sequence）===");
        userMessage.AppendLine("动作序列必须规划未来5秒的动作，时间总和必须严格等于5秒。");
        userMessage.AppendLine("动作类型：");
        userMessage.AppendLine("  - wait:duration - 停留动作（会暂停导航移动），duration为停留时间（秒）");
        userMessage.AppendLine("  - wave:angle:duration - 抬手动作（单参数格式，不暂停导航），angle为垂直角度/Z轴角度(0-180度，建议使用60-150度)，duration为动作持续时间（秒）");
        userMessage.AppendLine("  - wave:horizontal:vertical:duration - 抬手动作（双参数格式，不暂停导航），horizontal为水平角度/Y轴角度（度，建议10-45度），vertical为垂直角度/Z轴角度(0-180度，建议使用60-150度)，duration为动作持续时间（秒）");
        userMessage.AppendLine("  - look:angle:duration - 抬头动作（单参数格式，不暂停导航），angle为垂直角度/Y轴角度(0-30度，建议使用20-30度，应用时取负)，duration为动作持续时间（秒）");
        userMessage.AppendLine("  - look:horizontal:vertical:duration - 抬头动作（双参数格式，不暂停导航），horizontal为水平角度/X轴角度（度，建议5-30度），vertical为垂直角度/Y轴角度(0-30度，建议使用20-30度，应用时取负)，duration为动作持续时间（秒）");
        userMessage.AppendLine("  - continuous_wave:duration - 连续挥手动作（不暂停导航），执行4个来回，垂直角度从0度到100度，duration为动作持续时间（秒）。duration越小，挥手越快（表现兴奋急促）；duration越大，挥手越慢（表现悠闲友好）。这是一个热情友好的连续挥手动作，非常适合向主人打招呼。");
        userMessage.AppendLine("    重要：continuous_wave的duration应该根据情境和情绪变化！非常兴奋时使用1.0-2.0秒（快速挥手），正常友好时使用2.5-3.5秒（中等速度），悠闲放松时使用4.0-5.0秒（慢速挥手）。不要总是使用相同的duration值！");
        userMessage.AppendLine("  - head_look_at_human:duration - 头部朝向Human动作（不暂停导航），当感知范围内有人物标签时，自动让头部朝向对方，duration为动作持续时间（秒）。当人物在身后时，头部会转向靠近人的一侧（扭到最大允许角度）。这是一个非常适合表达关注和互动的动作，建议在需要朝向主人时使用。");
        userMessage.AppendLine("重要说明：");
        userMessage.AppendLine("  - 为了表现更生动自然的宠物行为，强烈建议优先使用双参数格式（wave:horizontal:vertical:duration 和 look:horizontal:vertical:duration）");
        userMessage.AppendLine("  - 双参数格式可以同时控制水平和垂直两个方向的角度，让动作更加丰富多样，更符合真实宠物的行为表现");
        userMessage.AppendLine("  - 单参数格式（wave:angle:duration 和 look:angle:duration）仅作为简化选项，只在特殊情况下使用");
        userMessage.AppendLine("  - 建议在动作序列中至少使用一个双参数格式的动作，以增加动作的丰富性");
        userMessage.AppendLine("示例（双参数格式 - 推荐，使用较大角度）：");
        userMessage.AppendLine("  wait:1.0,wave:25:80:2.0,look:15:25:2.0  （停留1秒，抬手水平25度垂直80度2秒，抬头水平15度垂直25度2秒，总计5秒）");
        userMessage.AppendLine("  wave:30:100:2.5,look:20:28:2.5  （抬手水平30度垂直100度2.5秒，抬头水平20度垂直28度2.5秒，总计5秒）");
        userMessage.AppendLine("  wait:1.0,wave:35:120:2.0,look:18:30:2.0  （停留1秒，抬手水平35度垂直120度2秒，抬头水平18度垂直30度2秒，总计5秒）");
        userMessage.AppendLine("  wave:40:90:3.0,look:25:22:2.0  （抬手水平40度垂直90度3秒，抬头水平25度垂直22度2秒，总计5秒）");
        userMessage.AppendLine("示例（混合格式 - 可接受，使用较大角度）：");
        userMessage.AppendLine("  wait:1.0,wave:30:85:2.0,look:25:2.0  （停留1秒，抬手双参数水平30度垂直85度2秒，抬头单参数25度2秒，总计5秒）");
        userMessage.AppendLine("  wave:25:110:2.5,look:15:26:2.5  （抬手双参数水平25度垂直110度2.5秒，抬头双参数水平15度垂直26度2.5秒，总计5秒）");
        userMessage.AppendLine("示例（单参数格式 - 仅在特殊情况下使用，使用较大角度，速度系数为0.8）：");
        userMessage.AppendLine("  wait:1.0,wave:75:2.0,look:25:2.0  （停留1秒，抬手75度2秒，抬头25度2秒，总计5秒）");
        userMessage.AppendLine("示例（包含连续挥手 - 推荐，表现热情友好，注意duration的多样性）：");
        userMessage.AppendLine("  continuous_wave:1.0,look:20:25:4  （快速挥手1秒表现兴奋，抬头水平20度垂直25度4秒，总计5秒）");
        userMessage.AppendLine("  continuous_wave:2.0,wave:30:90:3.0  （中等速度挥手2秒，抬手水平30度垂直90度3秒，总计5秒）");
        userMessage.AppendLine("  wait:1.0,continuous_wave:4.0  （停留1秒，慢速悠闲挥手4秒，总计5秒）");
        userMessage.AppendLine("  continuous_wave:2.5,look:15:26:2.5  （中等速度挥手2.5秒，抬头水平15度垂直26度2.5秒，总计5秒）");
        userMessage.AppendLine("  continuous_wave:1.5,wave:25:100:3.5  （快速兴奋挥手1.5秒，抬手水平25度垂直100度4秒，总计3.5秒）");
        userMessage.AppendLine("重要：连续挥手的duration应该根据当前情境和情绪变化！不要总是使用3.0秒，要根据兴奋程度、距离主人的远近、主人的状态等因素灵活选择1.0-5.0秒之间的不同值！");
        userMessage.AppendLine("示例（包含头部朝向Human - 推荐，表达关注互动）：");
        userMessage.AppendLine("  head_look_at_human:2.0,wave:25:90:3.0  （头部朝向Human 2秒，抬手水平25度垂直90度3秒，总计5秒）");
        userMessage.AppendLine("  wait:1.0,head_look_at_human:4.0  （停留1秒，头部朝向Human 4秒，总计5秒）");
        userMessage.AppendLine("  head_look_at_human:3.0,look:15:25:2.0  （头部朝向Human 3秒，抬头水平15度垂直25度2秒，总计5秒）");
        userMessage.AppendLine("重要：动作序列的时间总和必须严格等于5.0秒！可以任意组合五种动作（wait、wave、look、continuous_wave、head_look_at_human），但总时间必须为5秒。");
        userMessage.AppendLine("重要：优先使用双参数格式和连续挥手动作，让动作更加生动自然！连续挥手动作非常适合向主人表达热情和友好。");
        userMessage.AppendLine("重要：连续挥手的duration必须根据情境变化！非常兴奋、距离很近、主人可交互时，使用较短的duration（1.0-2.5秒）表现快速兴奋；正常友好时使用中等duration（2.5-3.5秒）；悠闲、距离较远、主人不可交互时，使用较长的duration（3.5-5.0秒）表现慢速悠闲。禁止总是使用相同的duration值（如总是3.0秒）！");
        userMessage.AppendLine("重要：必须使用较大的角度值！抬手垂直角度建议60-150度，抬头垂直角度建议20-30度。禁止使用过小的角度（抬手小于40度，抬头小于15度），那样动作不够明显和生动！");
        userMessage.AppendLine("path_type 表示路径类型：straight（直线路径，直接快速移动）或 scurve（S形曲线路径，优雅的S形移动）。");
        userMessage.AppendLine("路径选择建议：");
        userMessage.AppendLine("  - straight：需要快速直接移动时选择");
        userMessage.AppendLine("  - scurve：需要绕过障碍物、表现更自然的移动、或需要优雅的S形移动路径时选择（如绕过多个障碍物或表现更生动的移动）");

        // 允许在第1行给出简短理由，但禁止多余格式
        userMessage.AppendLine("不要输出markdown、标题或多余的装饰性文本；第1行只给简短理由，第2行只给数据。");
        
        isProcessingDecision = true;
        requestStartTime = Time.time; // 记录请求开始时间
        
        // 确保使用合并的系统提示词
        llmConnect.npcCharacter.personalityPrompt = combinedSystemPrompt;
        
        // 发送LLM请求
        
        llmConnect.SendDialogueRequest(userMessage.ToString(), OnLLMResponse);
        
    }
   
    /// <summary>
    /// 解析动作序列
    /// 新格式：x,y,z,speed,action_sequence,path_type
    /// </summary>
    private ActionSequence ParseActionSequence(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        
        string firstLine = text.Trim();
        if (string.IsNullOrEmpty(firstLine)) return null;
        
        string[] parts = firstLine.Split(',');
        if (parts.Length < 4) return null; // 至少需要：x,y,z,speed 或 x,y,z,action_sequence
        
        // 判断第4个字段是否为速度系数（纯数字）
        int actionStartIndex = 3; // 默认从索引3开始（旧格式：没有显式 speed）
        if (parts.Length >= 5 && float.TryParse(parts[3].Trim(), out _))
        {
            // 新格式：第4个字段是 speed，动作序列从第5个字段开始
            actionStartIndex = 4;
        }
        
        // 如果最后一部分是路径类型，则动作序列在倒数第二个位置
        string lastPart = parts[parts.Length - 1].Trim().ToLower();
        bool hasPathType = (lastPart == "straight" || lastPart == "scurve");
        
        int actionSequenceIndex = hasPathType ? parts.Length - 2 : parts.Length - 1;
        if (actionSequenceIndex < actionStartIndex) return null;
        
        // 合并动作序列字符串（可能包含逗号，如 wait:1.0,wave:30:1.0,look:30:1.0）
        // 需要从动作起始索引开始到 actionSequenceIndex 的所有部分合并
        string actionSequenceStr = "";
        for (int i = actionStartIndex; i <= actionSequenceIndex; i++)
        {
            if (i > actionStartIndex) actionSequenceStr += ",";
            actionSequenceStr += parts[i].Trim();
        }
        
        ActionSequence sequence = ActionSequence.Parse(actionSequenceStr);
        
        // 验证动作序列总时间为5秒
        if (sequence != null && !sequence.Validate(5f, 0.1f))
        {
            Debug.LogWarning($"[动作序列验证失败] 总时间 {sequence.totalDuration:F2}秒，期望5.0秒");
            return null;
        }
        
        return sequence;
    }
    
    /// <summary>
    /// 解析路径类型
    /// 新格式：x,y,z,speed,action_sequence,path_type
    /// </summary>
    private NavigationPathType? ParsePathType(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // 方法2：从CSV的最后一部分解析
        string firstLine = text.Trim();
        if (string.IsNullOrEmpty(firstLine)) return null;
        
        string[] parts = firstLine.Split(',');
        if (parts.Length >= 5) // 新格式至少5个部分：x,y,z,speed,action_sequence,path_type
        {
            string lastPart = parts[parts.Length - 1].Trim().ToLower();
            if (lastPart == "straight")
                return NavigationPathType.Straight;
            else if (lastPart == "scurve")
                return NavigationPathType.SCurve;
        }
        
        return null; // 默认返回null，表示未指定，将使用默认值
    }

    /// <summary>
    /// 将决策理由添加到记忆系统中（最多保存前5次）
    /// </summary>
    private void AddDecisionToMemory(string decisionReason)
    {
        if (string.IsNullOrEmpty(decisionReason)) return;
        
        // 添加到队列
        decisionMemory.Enqueue(decisionReason);
        
        // 如果超过最大数量，移除最旧的记忆
        while (decisionMemory.Count > MAX_MEMORY_COUNT)
        {
            decisionMemory.Dequeue();
        }
        
        Debug.Log($"[记忆系统] 已保存决策理由到记忆，当前记忆数量: {decisionMemory.Count}/{MAX_MEMORY_COUNT}");
    }
    
    /// <summary>
    /// 将 LLM 响应拆分为"理由行"和"数据行"
    /// - 如果只有一行，则认为没有理由行，整行作为数据行
    /// - 如果有多行，则第一行是理由行，最后一行是数据行
    /// </summary>
    private void SplitReasonAndData(string response, out string reasonLine, out string dataLine)
    {
        reasonLine = null;
        dataLine = null;

        if (string.IsNullOrEmpty(response)) return;

        string[] rawLines = response.Split('\n');
        List<string> lines = new List<string>();
        foreach (var l in rawLines)
        {
            if (!string.IsNullOrWhiteSpace(l))
            {
                lines.Add(l.Trim());
            }
        }

        if (lines.Count == 0) return;

        if (lines.Count == 1)
        {
            // 只输出了一行，兼容旧格式：这一行既是数据行，没有理由行
            dataLine = lines[0];
            return;
        }

        // 多行：第一行当作理由，最后一行当作 CSV 数据
        reasonLine = lines[0];
        dataLine = lines[lines.Count - 1];
    }
    
    /// <summary>
    /// LLM响应回调（合并决策：导航+肢体动作）
    /// </summary>
    private void OnLLMResponse(string response, bool isSuccess)
    {
        // 计算请求到响应的耗时
        float requestDuration = Time.time - requestStartTime;
        Debug.Log($"LLM响应耗时: {requestDuration:F2}秒");
        isProcessingDecision = false;
        
        if (!isSuccess)
        {
            //Debug.LogError($"LLMAgent: LLM决策请求失败 [耗时: {requestDuration:F2}秒]");
            return;
        }
        // 先拆分"理由行 + 数据行"
        SplitReasonAndData(response, out string reasonLine, out string dataLine);
        if (!string.IsNullOrEmpty(reasonLine))
        {
            Debug.Log($"[LLM REASON] {reasonLine}");
            Debug.Log($"[LLM DATA] {dataLine}");
            // 保存决策理由到记忆系统
            AddDecisionToMemory(reasonLine);
        }

        if (string.IsNullOrEmpty(dataLine))
        {
            // 兜底：如果没法识别数据行，就用原始响应，兼容旧逻辑
            Debug.Log($"[LLM DATA] {dataLine}");
            dataLine = response;
        }

		// ===== 关键：收到新决策后，立即终止旧决策 =====
		// 无论后续解析是否成功，都要先停止旧的导航和动作序列
		// 这是确保新决策能够立即打断旧决策的关键步骤
		bool wasNavigating = false;
		bool wasExecutingAction = false;
		
		if (navigationController != null)
		{
			wasNavigating = navigationController.IsNavigating();
			if (wasNavigating)
			{
				Debug.Log($"[新决策打断] 检测到旧导航正在执行，立即停止 (时间: {Time.time:F2}秒)");
				navigationController.StopNavigation();
			}
		}
		
		// 停止肢体控制器的动作序列（即使导航已停止，动作序列可能还在执行）
		if (limbController != null)
		{
			wasExecutingAction = limbController.IsExecutingActionSequence();
			if (wasExecutingAction)
			{
				Debug.Log($"[新决策打断] 检测到旧动作序列正在执行，立即停止 (时间: {Time.time:F2}秒)");
				limbController.StopActionSequence();
			}
		}
		
		if (wasNavigating || wasExecutingAction)
		{
			Debug.Log($"[新决策打断] 旧决策已完全停止，准备执行新决策");
		}

		// 解析动作序列（新格式）
		ActionSequence actionSequence = ParseActionSequence(dataLine);
		NavigationPathType? pathType = ParsePathType(dataLine);
		
		// 如果未解析到路径类型，使用默认值（直线）
		NavigationPathType finalPathType = pathType.HasValue ? pathType.Value : NavigationPathType.Straight;
		if (pathType.HasValue)
		{
			Debug.Log($"[路径类型] LLM选择: {finalPathType}");
		}
		
		if (actionSequence != null)
		{
			Debug.Log($"[动作序列] {actionSequence.ToString()}, 总时长: {actionSequence.totalDuration:F2}秒");
		}
		else
		{
			Debug.LogWarning("[动作序列] 解析失败，将使用默认动作");
		}

        // 解析目标位置
        bool hasNavigation = false;
		if (navigationController != null)
        {
			
			if (navigationController.TryParseTargetPosition(dataLine, out Vector3 targetPosition))
            {
                // 将目标位置的 y 固定为当前物体的 y，避免高度变化
                targetPosition = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
                //Debug.Log($"成功解析到目标位置(锁定Y): ({targetPosition.x:F2}, {targetPosition.y:F2}, {targetPosition.z:F2})");
                
                // 视场约束验证和自动纠正
                if (enableFovConstraint && hasCurrentHumanInfo && currentHumanIsInteractable)
                {
                    float fovAngle = 90f;
                    float fovMaxDistance = 2f;
                    if (humanFovVisualizer != null)
                    {
                        fovAngle = humanFovVisualizer.FovAngle;
                        fovMaxDistance = humanFovVisualizer.FovMaxDistance;
                    }
                    float halfAngleDeg = fovAngle / 2f;
                    
                    // 从理由行中提取期望的视场方案
                    bool desiredInsideFov = true; // 默认视场内
                    if (!string.IsNullOrEmpty(reasonLine))
                    {
                        string reasonLower = reasonLine.ToLower();
                        if (reasonLower.Contains("视场外") || reasonLower.Contains("场外"))
                        {
                            desiredInsideFov = false;
                        }
                        else if (reasonLower.Contains("视场内") || reasonLower.Contains("场内"))
                        {
                            desiredInsideFov = true;
                        }
                    }
                    
                    // 验证视场约束
                    bool isValid = ValidateFovConstraint(targetPosition, currentHumanPosition, currentHumanForward,
                        halfAngleDeg, fovMaxDistance, out bool actualInsideFov);
                    
                    if (!isValid || actualInsideFov != desiredInsideFov)
                    {
                        // 验证失败，自动纠正
                        Vector3 correctedPosition = CorrectFovPosition(targetPosition, currentHumanPosition, currentHumanForward,
                            halfAngleDeg, fovMaxDistance, desiredInsideFov);
                        
                        // 验证纠正后的位置
                        bool correctedValid = ValidateFovConstraint(correctedPosition, currentHumanPosition, currentHumanForward,
                            halfAngleDeg, fovMaxDistance, out bool correctedInsideFov);
                        
                        if (correctedValid && correctedInsideFov == desiredInsideFov)
                        {
                            Debug.LogWarning($"[FOV验证失败] LLM选择了\"{(desiredInsideFov ? "视场内" : "视场外")}\"但位置不满足约束，已自动纠正");
                            Debug.LogWarning($"  原始位置: ({targetPosition.x:F2}, {targetPosition.z:F2}) -> 纠正后: ({correctedPosition.x:F2}, {correctedPosition.z:F2})");
                            targetPosition = correctedPosition;
                        }
                        else
                        {
                            Debug.LogWarning($"[FOV验证失败] 自动纠正后仍不满足约束，使用原始位置");
                        }
                    }
                    else
                    {
                        // 验证成功
                        Debug.Log($"[FOV验证成功] 位置满足视场约束: 方案=\"{desiredInsideFov}\", 实际=\"{actualInsideFov}\"");
                    }
                }
                
                // 计算距离和角度
                navigationController.CalculateNavigationInfo(targetPosition, out float distance, out float angle);
                // 如果目标过近，则沿当前朝向推进一个最小步长，避免不移动
                if (distance < Mathf.Max(0.01f, minMoveSeparation))
                {
                    Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                    Vector3 bump = forwardFlat * Mathf.Max(0.1f, minMoveSeparation);
                    Vector3 adjusted = new Vector3(transform.position.x + bump.x, transform.position.y, transform.position.z + bump.z);
                    targetPosition = adjusted;
                }
                // 如果有Human信息且状态为Interactable，到达后朝向Human位置
                Vector3? lookAtPosition = null;
                if (hasCurrentHumanInfo && currentHumanIsInteractable)
                {
                    lookAtPosition = currentHumanPosition;
                }
                
                // 如果动作序列中包含HeadLookAtHuman动作，需要设置Human位置到LimbAnimationController
                if (actionSequence != null && limbController != null)
                {
                    bool hasHeadLookAtHuman = actionSequence.actions.Exists(a => a.type == ActionType.HeadLookAtHuman);
                    if (hasHeadLookAtHuman && hasCurrentHumanInfo)
                    {
                        limbController.SetCurrentHumanPosition(currentHumanPosition);
                    }
                    else if (hasHeadLookAtHuman && !hasCurrentHumanInfo)
                    {
                        // 如果动作序列包含HeadLookAtHuman但没有Human信息，清除位置信息
                        limbController.SetCurrentHumanPosition(null);
                    }
                }

                // 解析导航速度系数，并传递给导航控制器
                float speedScale = ParseNavigationSpeedScale(dataLine);
                navigationController.SetSpeedScale(speedScale);

                // 传递路径类型和动作序列到导航控制器
                navigationController.NavigateToPosition(targetPosition, 0f, lookAtPosition, finalPathType, actionSequence);
                hasNavigation = true;
            }
        }
        
        // 动作序列已通过NavigationController执行，这里不再单独处理肢体动作
        bool hasLimbAction = (actionSequence != null);
        // 如果都没有解析到，记录警告
        if (!hasNavigation && !hasLimbAction)
        {
            Debug.LogWarning($"✗ 无法从LLM响应中解析任何决策");
            Debug.LogWarning($"原始响应: {response}");
            Debug.LogWarning("支持的格式：x,y,z,action_sequence,path_type");
            Debug.LogWarning("动作序列格式：wait:duration,wave:angle:duration,look:angle:duration（总时间必须为5秒）");
        }
    }
    /// <summary>
    /// 设置LLM响应时间
    /// </summary>
    public void SetLLMResponseTime(float responseTime)
    {
        llmResponseTime = Mathf.Max(0.5f, responseTime);
    }
    
    /// <summary>
    /// 验证目标位置是否满足视场约束
    /// </summary>
    /// <param name="targetPosition">目标位置</param>
    /// <param name="humanPosition">Human位置</param>
    /// <param name="humanForward">Human朝向向量（已归一化）</param>
    /// <param name="fovHalfAngleDeg">视场半角（度）</param>
    /// <param name="fovMaxDistance">视场最大距离（米）</param>
    /// <param name="isInsideFov">输出：是否为视场内</param>
    /// <returns>是否满足视场约束（距离和角度都在有效范围内）</returns>
    private bool ValidateFovConstraint(Vector3 targetPosition, Vector3 humanPosition, Vector3 humanForward, 
        float fovHalfAngleDeg, float fovMaxDistance, out bool isInsideFov)
    {
        isInsideFov = false;
        
        // 计算距离（忽略Y轴）
        Vector3 toTarget = targetPosition - humanPosition;
        Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z);
        float distance = toTargetFlat.magnitude;
        
        // 距离检查：必须在视场最大距离内
        if (distance > fovMaxDistance + 0.01f) // 允许小误差
        {
            return false; // 距离超出范围
        }
        
        // 如果距离为0，视为视场内
        if (toTargetFlat.sqrMagnitude < 0.0001f)
        {
            isInsideFov = true;
            return true;
        }
        
        // 计算夹角
        Vector3 toTargetNormalized = toTargetFlat.normalized;
        Vector3 humanForwardFlat = new Vector3(humanForward.x, 0f, humanForward.z);
        if (humanForwardFlat.sqrMagnitude < 0.0001f)
        {
            humanForwardFlat = humanForward;
        }
        humanForwardFlat.Normalize();
        
        float angle = Vector3.Angle(humanForwardFlat, toTargetNormalized);
        
        // 判断是视场内还是视场外
        if (angle <= fovHalfAngleDeg + 0.5f) // 允许小误差
        {
            isInsideFov = true;
            return true; // 视场内，满足约束
        }
        else if (angle > fovHalfAngleDeg - 0.5f) // 视场外
        {
            isInsideFov = false;
            return true; // 视场外，满足约束
        }
        
        return false; // 角度在边界附近，可能有问题
    }
    
    /// <summary>
    /// 自动纠正目标位置以满足视场约束
    /// </summary>
    /// <param name="originalPosition">原始目标位置</param>
    /// <param name="humanPosition">Human位置</param>
    /// <param name="humanForward">Human朝向向量</param>
    /// <param name="fovHalfAngleDeg">视场半角（度）</param>
    /// <param name="fovMaxDistance">视场最大距离（米）</param>
    /// <param name="desiredInsideFov">期望是否为视场内</param>
    /// <returns>纠正后的位置</returns>
    private Vector3 CorrectFovPosition(Vector3 originalPosition, Vector3 humanPosition, Vector3 humanForward,
        float fovHalfAngleDeg, float fovMaxDistance, bool desiredInsideFov)
    {
        // 计算Human的平面朝向和右向量
        Vector3 humanForwardFlat = new Vector3(humanForward.x, 0f, humanForward.z);
        if (humanForwardFlat.sqrMagnitude < 0.0001f)
        {
            humanForwardFlat = humanForward;
        }
        humanForwardFlat.Normalize();
        
        Vector3 right = Vector3.Cross(Vector3.up, humanForwardFlat).normalized;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        
        // 选择目标距离（在有效范围内，稍微保守一些）
        float originalDist = Vector3.Distance(originalPosition, humanPosition);
        float targetDistance = Mathf.Min(fovMaxDistance * 0.8f, Mathf.Max(0.5f, originalDist));
        targetDistance = Mathf.Clamp(targetDistance, 0.5f, fovMaxDistance);
        
        // 根据期望方案选择角度
        float targetAngleDeg;
        if (desiredInsideFov)
        {
            // 视场内：选择0度（正前方）或小角度偏移
            targetAngleDeg = Random.Range(-fovHalfAngleDeg * 0.4f, fovHalfAngleDeg * 0.4f);
        }
        else
        {
            // 视场外：选择大于半角的角度
            if (Random.value > 0.5f)
            {
                // 身后（150-170度）
                targetAngleDeg = Random.Range(150f, 170f);
            }
            else
            {
                // 侧方（半角+15度 到 120度）
                targetAngleDeg = Random.Range(fovHalfAngleDeg + 15f, 120f);
                if (Random.value > 0.5f) targetAngleDeg = -targetAngleDeg;
            }
        }
        
        // 计算纠正后的位置
        float angleRad = targetAngleDeg * Mathf.Deg2Rad;
        Vector3 offsetDir = humanForwardFlat * Mathf.Cos(angleRad) + right * Mathf.Sin(angleRad);
        Vector3 correctedPosition = humanPosition + offsetDir.normalized * targetDistance;
        
        // 保持Y坐标不变
        correctedPosition = new Vector3(correctedPosition.x, originalPosition.y, correctedPosition.z);
        
        return correctedPosition;
    }
    
    /// <summary>
    /// 检查是否正在处理决策或导航
    /// </summary>
    private void OnDestroy()
    {
        StopAutoDecision();
    }
}

