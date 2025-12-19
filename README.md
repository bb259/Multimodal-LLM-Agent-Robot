# AI Motion 项目文档
# 快速上手指南

## 第一步：了解项目结构

### 核心文件阅读顺序
1. **ActionSequence.cs** - 了解动作系统的基础数据结构
2. **EnvironmentScanner.cs** - 了解环境感知的工作原理
3. **DSConnect.cs** - 了解LLM API调用方式
4. **LLMAgent.cs** - 了解整个系统的决策流程（最重要！）
5. **NavigationController.cs** - 了解导航执行逻辑
6. **LimbAnimationController.cs** - 了解动作执行逻辑

### 
| 概念 | 说明 | 相关文件 |
| **环境感知** | 扫描周围带标签的物体 | `EnvironmentLogger`, `EnvironmentScanner` |
| **LLM决策** | 根据环境信息生成移动和动作决策 | `LLMAgent`, `DSConnect` |
| **导航执行** | 移动到目标位置 | `NavigationController` |
| **动作序列** | 5秒的动作组合（抬手、抬头等） | `ActionSequence`, `LimbAnimationController` |
| **视场约束** | Human的视野范围限制 | `LLMAgent`, `HumanFovVisualizer` |
| **交互状态** | Human的可交互/不可交互状态 | `HumanInteraction` |

---

## 第二步：运行项目

### 1. 打开Unity项目
- 打开Unity Hub

### 2. 检查场景设置
- 确认以下存在：
  - 小狗机器人（带所有核心组件）
  - Human对象（标签为"Human"，带`HumanInteraction`组件）

### 3. 配置检查清单

#### LLMAgent组件
- [ ] `autoRequestDecision` 已勾选
- [ ] `llmResponseTime` 设置为合理值
- [ ] `DSConnect` 组件已关联
- [ ] `EnvironmentLogger` 组件已关联
- [ ] `NavigationController` 组件已关联
- [ ] `LimbAnimationController` 组件已关联

#### EnvironmentLogger组件
- [ ] `scanInterval` 设置为2秒
- [ ] `scanRadius` 设置为1米
- [ ] `scanTags` 包含 `"Human"`
- [ ] `enableLogging` 已勾选

#### DSConnect组件
- [ ] API Key 已配置（已经硬编码在代码中）
- [ ] `temperature` 设置为0.7
- [ ] `maxTokens` 设置为150

#### LimbAnimationController组件
- [ ] `waveBoneName` 设置为正确的骨骼名称（如"Bone11"）
- [ ] `lookBoneName` 设置为正确的骨骼名称（如"Bone015"）

### 4. 调试的配置
- 参考.vscode文件下面的launch.json文件，注意type:unity，是运行调试成功的关键
---

## 第三步：理解关键流程
### 决策循环流程图
```
开始
  ↓
EnvironmentLogger 扫描环境（每2秒）
  ↓
生成环境信息字符串
  ↓
LLMAgent 构建提示词（包含环境信息、历史记忆、Human状态）
  ↓
DSConnect 调用DeepSeek API
  ↓
LLM返回两行：
  第1行：决策理由
  第2行：x,y,z,speed,action_sequence,path_type
  ↓
LLMAgent 解析响应
  ↓
验证视场约束（如果Human可交互）
  ↓
NavigationController 执行导航+LimbAnimationController 执行动作序列（导航与动作执行并行）
  ↓
等待下一个循环
```

### 关键代码位置

#### 1. 环境扫描入口
// EnvironmentLogger.cs:102
private IEnumerator PeriodicScanCoroutine()

#### 2. LLM决策请求
// LLMAgent.cs:188
public void RequestLLMDecision()

#### 3. LLM响应处理
// LLMAgent.cs:562
private void OnLLMResponse(string response, bool isSuccess)

#### 4. 导航执行
// NavigationController.cs:96
public void NavigateToPosition(...)

#### 5. 动作序列执行
// LimbAnimationController.cs:414
public Coroutine ExecuteActionSequence(...)
-------------------------------

## 第四步：常见修改场景

### 场景1：修改扫描半径
**位置**：`EnvironmentLogger.cs`
[SerializeField] private float scanRadius = 1f;

**或者**在Inspector中直接修改 `EnvironmentLogger` 组件的 `Scan Radius` 字段。

### 场景2：修改LLM提示词
**位置**：`LLMAgent.cs:27`
[SerializeField] private string combinedSystemPrompt = @"...";


### 场景3：添加新的动作类型 **这个很重要，是你后面添加新的动作的核心代码**
1. **在 `ActionType` 枚举中添加**（`ActionSequence.cs:8`）
public enum ActionType
{
    // ... 现有类型
    NewAction,  // 添加新类型
}

2. **在 `ActionSequence.Parse()` 中添加解析逻辑**（`ActionSequence.cs:63`）

3. **在 `LimbAnimationController.ExecuteActionSequenceCoroutine()` 中添加执行逻辑**（`LimbAnimationController.cs:447`）

### 场景5：修改日志路径
**位置**：`EnvironmentLogger.cs:64`
string defaultLogDirectory = @"修改为你的路径"; 


### 场景6：修改API配置
**位置**：`DSConnect.cs:11`
[SerializeField] private string apiKey = "sk-f572fb23f9c34edfb6a439ad49610e86";
[SerializeField] private string modelName = "deepseek-chat";
[SerializeField] private string apiUrl = "https://api.deepseek.com/v1/chat/completions";

---

## 第六步：理解LLM输出格式
### 输出格式化
LLM必须输出两行：
**第1行**：决策理由（中文，不超过30字）
**第2行**：数据行（CSV格式，无空格）

### 举例
```
靠近可交互主人，抬爪打招呼
1.5,0,2.0,1.0,wave:25:90:2.0,look:20:25:3.0,straight
```
**解析**：
- `x=1.5, y=0, z=2.0`：目标位置
- `speed=1.0`：速度系数（在0.8-1.2范围内，因为有可交互Human）
- `action_sequence=wave:25:90:2.0,look:20:25:3.0`：
  - 抬手（水平25度，垂直90度）持续2秒
  - 抬头（水平20度，垂直25度）持续3秒
  - 总计5秒
- `path_type=straight`：直线路径
**重要**：
- 所有动作的duration总和必须等于5秒
- 可以任意组合多个动作
- 用逗号分隔多个动作
------------------------------

## 第七步：常见问题排查

### 问题1：游戏对象不移动
**检查清单**：
- [ ] `LLMAgent.autoRequestDecision` 是否勾选？
- [ ] Console中是否有LLM响应？
- [ ] `NavigationController` 组件是否正常？

### 问题2：动作不执行
**检查清单**：
- [ ] 骨骼名称是否正确？
- [ ] `LimbAnimationController` 组件是否正常？
- [ ] 动作序列格式是否正确？
- [ ] Console中是否有错误信息？

### 问题3：LLM响应格式错误
**检查清单**：
- [ ] API Key是否有效？
- [ ] 网络连接是否正常？
- [ ] `temperature` 和 `maxTokens` 设置是否合理？
- [ ] 系统提示词是否完整？
**调试方法**：
- 查看Console中的LLM响应原始文本
- 检查 `LLMAgent.OnLLMResponse()` 方法中的解析逻辑

### 问题4：环境扫描不到物体
**检查清单**：
- [ ] 物体是否有标签（需要不是"Untagged"）？
- [ ] 物体是否在扫描半径内？
- [ ] 物体是否有Collider？
- [ ] `scanTags` 数组是否包含该标签？

### 待完善功能！！！！！！！！！！！！！！

1. **避障系统**
   - 当前有边界信息收集，但未实现避障逻辑
   - 可以参考 `EnvironmentScanner` 中的边界信息

2. **更丰富的动作**
   - 当前支持抬手、抬头、连续挥手
   - 头部朝向人的这个原子动作还没做，可先做这个
   - 可以扩展更多原子动作，这个工作可能还需要持续一段时间，不断挖掘lovot的原子动作

3. **多Human支持**
   - 当前主要针对单个Human
   - 可以扩展为支持多个Human的优先级选择

4. **环境复杂度提高**
   - 当前场景中主要只有对人物进行交互
   - 后续可将机器人放入居家or办公场景，配合避障系统

5. **传感器仿真**
   - 目前实现了简单的鱼眼和双目，比较菜...
   - 可参考lovot的传感器配置，后续实现传感器感知到环境信息反馈再将信息输入到大模型
  
6. **各种反馈机制**
   - 触摸反馈

7. **LLM记忆系统**
   - 实现机器人有长记忆功能
   - 可以再多研究一下如何设计
  
### 优化建议
   **性能优化**
   - 主要是大模型和代码逻辑上、运行效率上的优化
   - 我搭的比较草
----
本项目为内部项目，仅供学习和开发使用。
---
**最后更新**：2025.12.19
