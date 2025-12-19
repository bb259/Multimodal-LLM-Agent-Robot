using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class EnvironmentScanner
{
    
    /// <summary>
    /// 感知结果数据结构
    /// </summary>
    [System.Serializable]
    public struct PerceivedObject
    {
        public string name;      // 物体名称
        public string tag;       // 物体标签
        public Vector3 position; // xyz位置
        public float distance;   // 距离原点的距离
        public float bearingDeg; // 方向角度（度）
        public Transform transform; // 物体的Transform引用
        public string StateLabel; // 物体状态标签
        public Vector3 forward;  // 物体自身的正Z朝向
        public Vector3 boundsmin;//人物BoxCollider边界最小值
        public Vector3 boundsmax;//人物BoxCollider边界最大值
    }
    
    /// <summary>
    /// 安全地获取游戏对象的边界信息（支持多种Collider类型）
    /// </summary>
    private static void GetObjectBounds(GameObject go, out Vector3 boundsMin, out Vector3 boundsMax)
    {
        boundsMin = Vector3.zero;
        boundsMax = Vector3.zero;
        
        if (go == null) return;
        
        // 尝试获取各种类型的Collider
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            boundsMin = collider.bounds.min;
            boundsMax = collider.bounds.max;
            return;
        }
        
        // 如果没有Collider，尝试从Renderer获取边界
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            boundsMin = renderer.bounds.min;
            boundsMax = renderer.bounds.max;
            return;
        }
        
        // 如果都没有，使用Transform位置作为默认值（创建一个小的边界框）
        Vector3 pos = go.transform.position;
        float defaultSize = 0.5f; // 默认大小
        boundsMin = pos - Vector3.one * defaultSize;
        boundsMax = pos + Vector3.one * defaultSize;
    }

    public static string BuildEnvironmentSummary(Transform origin,//transform是UnityEngine.Transform类型，表示一个游戏对象的变换（位置、旋转、缩放）
                                                 float radiusMeters,
                                                 LayerMask layerMask,
                                                 string[] tagsWhitelist,
                                                 int maxItems)
    {
        if (origin == null || radiusMeters <= 0f || maxItems <= 0)
        {
            //Debug.Log("EnvironmentScanner: 参数无效 - origin=" + (origin != null) + ", radius=" + radiusMeters + ", maxItems=" + maxItems);
            return "[环境感知|半径=0m|数量=0] 无";
        }
        
        var colliders = Physics.OverlapSphere(origin.position, radiusMeters, layerMask);
   
        // 调试：显示所有检测到的物体
        for (int i = 0; i < colliders.Length; i++)
        {
            var go = colliders[i].attachedRigidbody ? colliders[i].attachedRigidbody.gameObject : colliders[i].gameObject;
            if (go != null)
            {
                float distance = Vector3.Distance(origin.position, go.transform.position);
            }
        }
        
        // 如果没有检测到任何Collider，尝试检测所有Collider（忽略层掩码）
        if (colliders.Length == 0)
        {
            var allColliders = Physics.OverlapSphere(origin.position, radiusMeters);
            
            for (int i = 0; i < allColliders.Length; i++)
            {
                var go = allColliders[i].attachedRigidbody ? allColliders[i].attachedRigidbody.gameObject : allColliders[i].gameObject;
                if (go != null)
                {
                    float distance = Vector3.Distance(origin.position, go.transform.position);
                }
            }
        }
        // 创建一个列表来存储结果
        var results = new List<PerceivedObject>(colliders.Length);

        // Tod duplicates from multiple colliders on same object
        var seenGameObjects = new HashSet<int>();

        for (int i = 0; i < colliders.Length; i++)
        {
            var go = colliders[i].attachedRigidbody ? colliders[i].attachedRigidbody.gameObject : colliders[i].gameObject;
            if (go == null) continue;

            int id = go.GetInstanceID();
            if (seenGameObjects.Contains(id)) continue;
            seenGameObjects.Add(id);

            if (go == origin.gameObject) continue;
            if (!go.activeInHierarchy) continue;

            // 过滤掉Untagged标签的物体
            if (string.IsNullOrEmpty(go.tag) || go.tag == "Untagged")
            {
                continue;
            }
            Vector3 delta = go.transform.position - origin.position;//计算物体与原点的距离
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float dist = flat.magnitude;
            if (dist < Mathf.Epsilon) continue;

            float bearing = SignedFlatAngleDeg(origin.forward, flat);

            // 安全获取边界信息
            Vector3 boundsMin, boundsMax;
            GetObjectBounds(go, out boundsMin, out boundsMax);
            
            var item = new PerceivedObject
            {
                name = string.IsNullOrEmpty(go.name) ? go.tag : go.name,
                tag = go.tag,
                distance = dist,
                bearingDeg = bearing,
                position = go.transform.position, // 记录世界坐标
                StateLabel=go.transform.TryGetComponent(out HumanInteraction interaction)?interaction.IsInteractable?"Interactable":"NonInteractable":"",
                forward=go.transform.forward,
                boundsmin=boundsMin,
                boundsmax=boundsMax,
            };
            results.Add(item);
        }

        // Sort by distance ascending
        results.Sort((a, b) => a.distance.CompareTo(b.distance));

        // Aggregate by tag (for header)
        var tagCounts = new Dictionary<string, int>();
        for (int i = 0; i < results.Count; i++)
        {
            string tag = string.IsNullOrEmpty(results[i].tag) ? "Untagged" : results[i].tag;
            if (tagCounts.TryGetValue(tag, out int c)) tagCounts[tag] = c + 1; else tagCounts[tag] = 1;
        }

        int total = results.Count;
        if (total == 0)
        {
            return $"[环境感知|半径={radiusMeters:0.#}m|数量=0] 无";
        }

        // 简化输出格式，减少token消耗
        var sb = new StringBuilder();
        sb.AppendFormat("[环境感知|半径={0:0.#}m|数量={1}]", radiusMeters, total);

        // 只输出标签统计，不输出详细信息
        sb.Append(" ");
        int printed = 0;
        foreach (var kv in tagCounts)
        {
            if (printed > 0) sb.Append(", ");
            sb.Append(kv.Key).Append(":").Append(kv.Value);
            printed++;
        }

        sb.Append('\n');

        int limit = Mathf.Min(maxItems, results.Count);
        for (int i = 0; i < limit; i++)
        {
            var it = results[i];
            string tag = string.IsNullOrEmpty(it.tag) ? "Untagged" : it.tag;
            float bearingRounded = Mathf.Round(it.bearingDeg);
            // 添加世界坐标信息，格式：物体名(标签) at (x,y,z) 距离Xm 方向X°
            sb.AppendFormat("{0}) {1}({2}) at ({3:F1},{4:F1},{5:F1}) 距离{6:0.0}m 方向{7:+0;-0;0}° 状态={8} 朝向=({9:F2},{10:F2},{11:F2}) 边界=({12:F2},{13:F2},{14:F2},{15:F2})",
                            i + 1, 
                            it.name, 
                            tag,
                            it.position.x, it.position.y, it.position.z,
                            it.distance, 
                            bearingRounded,
                            it.StateLabel,
                            it.forward.x, it.forward.y, it.forward.z,
                            it.boundsmin.x,it.boundsmax.x,it.boundsmin.z,it.boundsmax.z);
            if (i < limit - 1) sb.Append('\n');
        }

        return sb.ToString();
    }
    
    /// <summary>
    /// 检查字符串数组是否为空（所有元素都是空字符串或null）
    /// </summary>
    private static bool IsEmptyStringArray(string[] array)
    {
        if (array == null || array.Length == 0) return true;
        
        foreach (string item in array)
        {
            if (!string.IsNullOrEmpty(item))
            {
                return false;
            }
        }
        return true;
    }

    private static string Truncate(string s, int max)
    {
        
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= max) return s;
        return s.Substring(0, max - 1) + "…";
    }

    private static float SignedFlatAngleDeg(Vector3 forward, Vector3 toTargetFlat)//计算正负角差并转换为度
    {
        Vector3 f = new Vector3(forward.x, 0f, forward.z);
        Vector3 t = new Vector3(toTargetFlat.x, 0f, toTargetFlat.z);
        if (f.sqrMagnitude < 1e-6f || t.sqrMagnitude < 1e-6f) return 0f;
        f.Normalize();
        t.Normalize();
        float angle = Vector3.SignedAngle(f, t, Vector3.up);
        return angle;
    }
    
    /// <summary>
    /// 感知半径范围内带标签物体的位置信息
    /// </summary>
    /// <param name="origin">感知原点</param>
    /// <param name="radiusMeters">感知半径（米）</param>
    /// <param name="layerMask">层掩码，如果为-1则扫描所有层</param>
    /// <param name="tagsWhitelist">标签白名单，如果为空则检测所有带标签的物体</param>
    /// <returns>范围内带标签物体的位置信息列表</returns>
    public static List<PerceivedObject> PerceiveTaggedObjects(Transform origin, 
                                                              float radiusMeters = 1f,
                                                              int layerMask = -1,
                                                              string[] tagsWhitelist = null)
    {
        List<PerceivedObject> result = new List<PerceivedObject>();
        
        if (origin == null || radiusMeters <= 0f)
        {
            Debug.LogWarning("EnvironmentScanner.PerceiveTaggedObjects: 参数无效");
            return result;
        }
        
        // 使用物理检测范围内的所有碰撞体
        Collider[] colliders = Physics.OverlapSphere(origin.position, radiusMeters, layerMask);
        
        // 用于去重（同一个GameObject可能有多个Collider）
        HashSet<int> seenGameObjects = new HashSet<int>();
        
        foreach (Collider col in colliders)
        {
            if (col == null) continue;
            
            // 获取GameObject（优先使用Rigidbody的GameObject）
            GameObject go = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (go == null) continue;
            
            // 去重检查
            int instanceId = go.GetInstanceID();
            if (seenGameObjects.Contains(instanceId)) continue;
            seenGameObjects.Add(instanceId);
            
            // 跳过感知原点自身
            if (go == origin.gameObject) continue;
            
            // 跳过未激活的物体
            if (!go.activeInHierarchy) continue;
            
            // 获取标签
            string tag = go.tag;
            
            // 过滤掉Untagged标签的物体
            if (string.IsNullOrEmpty(tag) || tag == "Untagged")
            {
                continue;
            }
            
            // 如果指定了标签白名单，检查是否在白名单中
            if (tagsWhitelist != null && tagsWhitelist.Length > 0)
            {
                bool tagMatched = false;
                foreach (string whitelistTag in tagsWhitelist)
                {
                    if (!string.IsNullOrEmpty(whitelistTag) && tag == whitelistTag)
                    {
                        tagMatched = true;
                        break;
                    }
                }
                if (!tagMatched) continue;
            }
            
            // 计算距离和方向角度
            Vector3 delta = go.transform.position - origin.position;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;
            
            // 计算方向角度（相对于origin的forward方向）
            float bearing = 0f;
            if (distance > Mathf.Epsilon)
            {
                bearing = SignedFlatAngleDeg(origin.forward, flat);
            }
            
            // 添加到结果列表
            PerceivedObject perceivedObj = new PerceivedObject
            {
                name = string.IsNullOrEmpty(go.name) ? tag : go.name,
                tag = tag,
                position = go.transform.position,
                distance = distance,
                bearingDeg = bearing,
                transform = go.transform,
                forward = go.transform.forward
                
            };
            // 安全获取边界信息
            Vector3 boundsMin, boundsMax;
            GetObjectBounds(go, out boundsMin, out boundsMax);
            perceivedObj.boundsmin = boundsMin;
            perceivedObj.boundsmax = boundsMax;
            result.Add(perceivedObj);
        }
        
        // 按距离排序（从近到远）
        result.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        return result;
    }
}


