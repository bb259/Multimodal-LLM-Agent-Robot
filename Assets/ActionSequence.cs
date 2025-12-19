using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动作类型枚举
/// </summary>
public enum ActionType
{
    Wait,              // 停留（会暂停导航）
    Wave,              // 抬手（不暂停导航）
    Look,              // 抬头（不暂停导航）
    ContinuousWave,    // 连续挥手（不暂停导航，垂直角度从0度到100度，4个来回）
    HeadLookAtHuman    // 头部朝向Human（原子动作，感知范围内自动朝向，背对时转向靠近人的一侧）
}

/// <summary>
/// 单个动作项
/// </summary>
[Serializable]
public class ActionItem
{
    public ActionType type;      // 动作类型
    public float duration;       // 持续时间（秒）
    public float targetValue;    // 目标值（对于Wave和Look是角度，对于Wait忽略）
    public float targetValue2;  // 第二个目标值（对于Wave是垂直角度/Y轴，对于Look是水平角度/X轴，对于Wait忽略）

    public ActionItem(ActionType type, float duration, float targetValue = 0f, float targetValue2 = 0f)
    {
        this.type = type;
        this.duration = duration;
        this.targetValue = targetValue;
        this.targetValue2 = targetValue2;
    }
}

/// <summary>
/// 动作序列：包含多个动作项，总时间应为5秒
/// </summary>
[Serializable]
public class ActionSequence
{
    public List<ActionItem> actions = new List<ActionItem>();
    public float totalDuration => CalculateTotalDuration();

    private float CalculateTotalDuration()
    {
        float total = 0f;
        foreach (var action in actions)
        {
            total += action.duration;
        }
        return total;
    }

    /// <summary>
    /// 验证动作序列是否有效（总时间应为5秒）
    /// </summary>
    public bool Validate(float expectedDuration = 5f, float tolerance = 0.1f)
    {
        float total = totalDuration;
        return Mathf.Abs(total - expectedDuration) <= tolerance;
    }

    /// <summary>
    /// 从字符串解析动作序列
    /// </summary>
    public static ActionSequence Parse(string sequenceStr)
    {
        ActionSequence sequence = new ActionSequence();
        
        if (string.IsNullOrEmpty(sequenceStr))
            return sequence;

        string[] parts = sequenceStr.Split(',');
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            string[] tokens = trimmed.Split(':');
            if (tokens.Length < 2)
                continue;

            string actionTypeStr = tokens[0].Trim().ToLower();
            ActionType actionType;

            if (actionTypeStr == "wait")
            {
                // wait:duration
                if (tokens.Length >= 2 && float.TryParse(tokens[1].Trim(), out float duration))
                {
                    sequence.actions.Add(new ActionItem(ActionType.Wait, duration));
                }
            }
            else if (actionTypeStr == "wave")
            {
                // 支持两种格式：
                // wave:angle:duration (单参数，兼容旧格式)
                // wave:horizontal:vertical:duration (双参数，水平角度和垂直角度)
                if (tokens.Length >= 4 && 
                    float.TryParse(tokens[1].Trim(), out float horizontal) &&
                    float.TryParse(tokens[2].Trim(), out float vertical) &&
                    float.TryParse(tokens[3].Trim(), out float duration1))
                {
                    // 双参数格式：wave:horizontal:vertical:duration
                    sequence.actions.Add(new ActionItem(ActionType.Wave, duration1, horizontal, vertical));
                }
                else if (tokens.Length >= 3 && 
                    float.TryParse(tokens[1].Trim(), out float angle) &&
                    float.TryParse(tokens[2].Trim(), out float duration2))
                {
                    // 单参数格式：wave:angle:duration (兼容旧格式，vertical默认为0)
                    sequence.actions.Add(new ActionItem(ActionType.Wave, duration2, angle, 0f));
                }
            }
            else if (actionTypeStr == "look")
            {
                // 支持两种格式：
                // look:angle:duration (单参数，兼容旧格式)
                // look:horizontal:vertical:duration (双参数，水平角度/X轴和垂直角度/Y轴)
                if (tokens.Length >= 4 && 
                    float.TryParse(tokens[1].Trim(), out float horizontal) &&
                    float.TryParse(tokens[2].Trim(), out float vertical) &&
                    float.TryParse(tokens[3].Trim(), out float duration3))
                {
                    // 双参数格式：look:horizontal:vertical:duration
                    sequence.actions.Add(new ActionItem(ActionType.Look, duration3, horizontal, vertical));
                }
                else if (tokens.Length >= 3 && 
                    float.TryParse(tokens[1].Trim(), out float angle) &&
                    float.TryParse(tokens[2].Trim(), out float duration4))
                {
                    // 单参数格式：look:angle:duration (兼容旧格式，horizontal默认为0)
                    sequence.actions.Add(new ActionItem(ActionType.Look, duration4, angle, 0f));
                }
            }
            else if (actionTypeStr == "continuous_wave" || actionTypeStr == "continuouswave")
            {
                // 连续挥手格式：continuous_wave:duration
                // 执行4个来回，垂直角度从0度到100度
                if (tokens.Length >= 2 && float.TryParse(tokens[1].Trim(), out float duration5))
                {
                    sequence.actions.Add(new ActionItem(ActionType.ContinuousWave, duration5));
                }
            }
            else if (actionTypeStr == "head_look_at_human" || actionTypeStr == "headlookathuman")
            {
                // 头部朝向Human格式：head_look_at_human:duration
                // 感知范围内自动朝向Human位置，背对时转向靠近人的一侧
                if (tokens.Length >= 2 && float.TryParse(tokens[1].Trim(), out float duration6))
                {
                    sequence.actions.Add(new ActionItem(ActionType.HeadLookAtHuman, duration6));
                }
            }
        }

        return sequence;
    }

    public override string ToString()
    {
        List<string> parts = new List<string>();
        foreach (var action in actions)
        {
            switch (action.type)
            {
                case ActionType.Wait:
                    parts.Add($"wait:{action.duration:F2}");
                    break;
                case ActionType.Wave:
                    if (action.targetValue2 != 0f)
                    {
                        // 双参数格式
                        parts.Add($"wave:{action.targetValue:F1}:{action.targetValue2:F1}:{action.duration:F2}");
                    }
                    else
                    {
                        // 单参数格式（兼容旧格式）
                        parts.Add($"wave:{action.targetValue:F1}:{action.duration:F2}");
                    }
                    break;
                case ActionType.Look:
                    if (action.targetValue2 != 0f)
                    {
                        // 双参数格式
                        parts.Add($"look:{action.targetValue:F1}:{action.targetValue2:F1}:{action.duration:F2}");
                    }
                    else
                    {
                        // 单参数格式（兼容旧格式）
                        parts.Add($"look:{action.targetValue:F1}:{action.duration:F2}");
                    }
                    break;
                case ActionType.ContinuousWave:
                    // 连续挥手格式：continuous_wave:duration
                    parts.Add($"continuous_wave:{action.duration:F2}");
                    break;
                case ActionType.HeadLookAtHuman:
                    // 头部朝向Human格式：head_look_at_human:duration
                    parts.Add($"head_look_at_human:{action.duration:F2}");
                    break;
            }
        }
        return string.Join(",", parts);
    }
}

