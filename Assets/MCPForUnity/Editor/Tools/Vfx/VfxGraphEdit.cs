using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools.Vfx
{
    public static class VfxGraphEdit
    {
        // Helper to get internal types without crashing if they change
        private static Type GetVFXType(string typeName)
        {
            return VfxGraphReflectionCache.GetEditorVfxType(typeName);
        }

        public static object AddNode(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string nodeType = @params["type"]?.ToString();
            
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(nodeType))
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path and Type are required" };

            // 1. Load Graph (Reflection)
            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            // 2. Find Node Type based on name (e.g., "VFXNodeAdd")
            // We search all types that inherit from VFXNode (or just by name if we can't find VFXNode type easily)
            Type vfxNodeType = GetVFXType("VFXNode"); // Base class
            
            Type typeToInstantiate = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name.Equals(nodeType, StringComparison.OrdinalIgnoreCase) 
                                     && (vfxNodeType == null || vfxNodeType.IsAssignableFrom(t)));

            if (typeToInstantiate == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Node type '{nodeType}' not found" };

            try 
            {
                // 3. Create Instance
                ScriptableObject instance = ScriptableObject.CreateInstance(typeToInstantiate);
                if (instance == null) return new { success = false, message = "Failed to create instance" };
                
                // 4. Set Position (VFXModel.position is Vector2)
                if (@params["position"] != null)
                {
                    var pos = ManageVfxCommon.ParseVec2(@params["position"]);
                    PropertyInfo posProp = instance.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    posProp?.SetValue(instance, pos);
                }

                // 5. Add to Graph (VFXModel.AddChild(model)) -> No, usually it's graph.AddChild(model) or we add to the root?
                // Actually in the previous code I used VFXModel.AddChild on the graph.
                // Because VFXGraph inherits from VFXModel? Check docs or assume previous code 'addMethod.Invoke(graph...)' was correct.
                // 5. Add to Graph
                // AddChild(VFXModel model, int index, bool notify)
                MethodInfo addMethod = VfxGraphReflectionCache.GetMethodCached(graph.GetType(), "AddChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addMethod == null) throw new VfxToolReflectionException("AddChild method not found on Graph");

                // Use notify=false to prevent cascade invalidation crash from corrupt parameters.
                // VFXModel.AddChild inserts into m_Children before Invalidate, so with notify=true
                // the node gets added but any exception during invalidation reports false failure.
                addMethod.Invoke(graph, new object[] { instance, -1, false });

                SafeInvalidate(graph, "kStructureChanged");

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, id = instance.GetInstanceID(), message = $"Added {nodeType}" };
            }
            catch (VfxToolReflectionException ex)
            {
                return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = ex.Message };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error adding node: {ex.Message}" };
            }
        }

        /// <summary>
        /// Removes a node (operator, context, or parameter) from the VFX Graph by instance ID.
        /// Blocks are redirected to RemoveBlock. Contexts with active flow links return a warning.
        /// </summary>
        public static object RemoveNode(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? (@params["id"]?.ToObject<int>() ?? 0);

            if (string.IsNullOrEmpty(path) || nodeId == 0)
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path and nodeId are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);

            if (node == null)
                return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Node {nodeId} not found" };

            // If it's a block, redirect to RemoveBlock
            Type vfxBlockBase = GetVFXType("VFXBlock");
            if (vfxBlockBase != null && vfxBlockBase.IsAssignableFrom(node.GetType()))
            {
                return RemoveBlock(new JObject { ["path"] = path, ["blockId"] = nodeId });
            }

            try
            {
                string nodeTypeName = node.GetType().Name;

                MethodInfo removeMethod = graph.GetType().GetMethod("RemoveChild",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (removeMethod == null)
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = "RemoveChild method not found on graph" };

                // Use notify=false to prevent cascade invalidation crash from corrupt parameters
                removeMethod.Invoke(graph, new object[] { node, false });

                SafeInvalidate(graph, "kStructureChanged");

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Removed node {nodeTypeName} (id:{nodeId})" };
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error removing node: {inner.Message}", detail = inner.StackTrace };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error removing node: {ex.Message}" };
            }
        }

        /// <summary>
        /// Moves a node to a new position in the VFX Graph editor canvas.
        /// </summary>
        public static object MoveNode(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? (@params["id"]?.ToObject<int>() ?? 0);
            JToken positionToken = @params["position"];

            if (string.IsNullOrEmpty(path) || nodeId == 0 || positionToken == null)
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path, nodeId, and position are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);

            if (node == null)
                return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Node {nodeId} not found" };

            try
            {
                var pos = ManageVfxCommon.ParseVec2(positionToken);
                PropertyInfo posProp = node.GetType().GetProperty("position",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (posProp == null)
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = "position property not found on node" };

                posProp.SetValue(node, pos);

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Moved {node.GetType().Name} to ({pos.x}, {pos.y})", data = new { x = pos.x, y = pos.y } };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error moving node: {ex.Message}" };
            }
        }

        /// <summary>
        /// Duplicates a node in the VFX Graph, copying its type, position (with offset), and settings.
        /// Connections are NOT copied — the caller must reconnect.
        /// </summary>
        public static object DuplicateNode(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? (@params["id"]?.ToObject<int>() ?? 0);

            if (string.IsNullOrEmpty(path) || nodeId == 0)
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path and nodeId are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var sourceNode = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);

            if (sourceNode == null)
                return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Node {nodeId} not found" };

            try
            {
                ScriptableObject newNode = ScriptableObject.CreateInstance(sourceNode.GetType());
                if (newNode == null)
                    return new { success = false, message = $"Failed to create instance of {sourceNode.GetType().Name}" };

                // Copy position with offset
                PropertyInfo posProp = sourceNode.GetType().GetProperty("position",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (posProp != null)
                {
                    try
                    {
                        Vector2 srcPos = (Vector2)posProp.GetValue(sourceNode);
                        posProp.SetValue(newNode, srcPos + new Vector2(50, 50));
                    }
                    catch { }
                }

                // Copy VFXSetting-attributed fields
                Type currentType = sourceNode.GetType();
                while (currentType != null && currentType != typeof(ScriptableObject))
                {
                    foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        bool hasSettingAttr = field.GetCustomAttributes(true)
                            .Any(a => a.GetType().Name.Contains("VFXSetting"));
                        if (hasSettingAttr)
                        {
                            try { field.SetValue(newNode, field.GetValue(sourceNode)); }
                            catch { }
                        }
                    }
                    currentType = currentType.BaseType;
                }

                // Add to graph
                MethodInfo addMethod = VfxGraphReflectionCache.GetMethodCached(
                    graph.GetType(), "AddChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addMethod == null)
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = "AddChild method not found on graph" };

                // Use notify=false to prevent cascade invalidation crash from corrupt parameters
                addMethod.Invoke(graph, new object[] { newNode, -1, false });

                SafeInvalidate(graph, "kStructureChanged");

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    id = newNode.GetInstanceID(),
                    message = $"Duplicated {sourceNode.GetType().Name} (source:{nodeId} → new:{newNode.GetInstanceID()})",
                    data = new { sourceId = nodeId, newId = newNode.GetInstanceID(), type = sourceNode.GetType().Name }
                };
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error duplicating node: {inner.Message}" };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error duplicating node: {ex.Message}" };
            }
        }

        public static object ConnectNodes(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path)) return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path is required" };

            int parentId = @params["parentNodeId"]?.ToObject<int>() ?? (@params["fromNodeId"]?.ToObject<int>() ?? 0);
            string parentSlotName = @params["parentSlot"]?.ToString() ?? @params["fromSlot"]?.ToString();
            int childId = @params["childNodeId"]?.ToObject<int>() ?? (@params["toNodeId"]?.ToObject<int>() ?? 0);
            string childSlotName = @params["childSlot"]?.ToString() ?? @params["toSlot"]?.ToString();

            if (parentId == 0 || childId == 0) return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Parent and Child Node IDs are required" };
            // Allow empty slot names (operators sometimes have empty output slot names)
            // if (string.IsNullOrEmpty(parentSlotName) || string.IsNullOrEmpty(childSlotName)) return new { success = false, message = "Slot names are required" };            
            if (parentSlotName == null) parentSlotName = "";
            if (childSlotName == null) childSlotName = "";

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            // Find nodes
            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);

            var parentNode = models.FirstOrDefault(m => m.GetInstanceID() == parentId);
            var childNode = models.FirstOrDefault(m => m.GetInstanceID() == childId);

            if (parentNode == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Parent node {parentId} not found" };
            if (childNode == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Child node {childId} not found" };

            // Find Slots
            object parentSlot = FindSlot(parentNode, parentSlotName, true); // Output
            object childSlot = FindSlot(childNode, childSlotName, false);   // Input

            if (parentSlot == null)
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.NotFound,
                    message = $"Output slot '{parentSlotName}' not found on parent",
                    details = new { availableSlots = CollectSlotInfo(parentNode, true) }
                };
            }
            if (childSlot == null)
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.NotFound,
                    message = $"Input slot '{childSlotName}' not found on child",
                    details = new { availableSlots = CollectSlotInfo(childNode, false) }
                };
            }

            try 
            {
                // Call link on child slot: childSlot.Link(parentSlot)
                // Link(VFXSlot other, bool notify)
                MethodInfo linkMethod = VfxGraphReflectionCache.GetMethodCached(childSlot.GetType(), "Link", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (linkMethod == null) throw new VfxToolReflectionException("Link method not found on Slot");

                bool result = (bool)linkMethod.Invoke(childSlot, new object[] { parentSlot, true });
                
                if (result)
                {
                    EditorUtility.SetDirty(resource);
                    AssetDatabase.SaveAssets();
                    return new { success = true, message = $"Connected {parentNode.name}:{parentSlotName} -> {childNode.name}:{childSlotName}" };
                }
                else
                {
                    return new { success = false, message = "Link failed (type mismatch or circular dependency?)" };
                }
            }
            catch (VfxToolReflectionException ex)
            {
                return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = ex.Message };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error linking nodes: {ex.Message}" };
            }
        }

        public static object DisconnectNodes(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path is required" };

            int parentId = @params["parentNodeId"]?.ToObject<int>() ?? (@params["fromNodeId"]?.ToObject<int>() ?? 0);
            string parentSlotName = @params["parentSlot"]?.ToString() ?? @params["fromSlot"]?.ToString() ?? "";
            int childId = @params["childNodeId"]?.ToObject<int>() ?? (@params["toNodeId"]?.ToObject<int>() ?? 0);
            string childSlotName = @params["childSlot"]?.ToString() ?? @params["toSlot"]?.ToString() ?? "";

            if (parentId == 0 || childId == 0)
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Parent and Child Node IDs are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);

            var parentNode = models.FirstOrDefault(m => m.GetInstanceID() == parentId);
            var childNode = models.FirstOrDefault(m => m.GetInstanceID() == childId);

            if (parentNode == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Parent node {parentId} not found" };
            if (childNode == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Child node {childId} not found" };

            object parentSlot = FindSlot(parentNode, parentSlotName, true);
            object childSlot = FindSlot(childNode, childSlotName, false);

            if (parentSlot == null)
                return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Output slot '{parentSlotName}' not found on parent" };
            if (childSlot == null)
                return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Input slot '{childSlotName}' not found on child" };

            var errors = new List<string>();
            bool disconnected = false;

            var unlinkMethods = childSlot.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "Unlink")
                .ToArray();
            foreach (var method in unlinkMethods)
            {
                var parms = method.GetParameters();
                try
                {
                    if (parms.Length == 1)
                    {
                        method.Invoke(childSlot, new[] { parentSlot });
                        disconnected = true;
                        break;
                    }
                    if (parms.Length == 2)
                    {
                        method.Invoke(childSlot, new object[] { parentSlot, true });
                        disconnected = true;
                        break;
                    }
                }
                catch (TargetInvocationException tie)
                {
                    errors.Add($"Unlink({parms.Length}p): {tie.InnerException?.Message ?? tie.Message}");
                }
            }

            if (!disconnected)
            {
                var unlinkAllMethods = childSlot.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "UnlinkAll")
                    .ToArray();
                foreach (var method in unlinkAllMethods)
                {
                    var parms = method.GetParameters();
                    try
                    {
                        if (parms.Length == 0)
                            method.Invoke(childSlot, null);
                        else if (parms.Length == 1)
                            method.Invoke(childSlot, new object[] { true });
                        disconnected = true;
                        break;
                    }
                    catch (TargetInvocationException tie)
                    {
                        errors.Add($"UnlinkAll({parms.Length}p): {tie.InnerException?.Message ?? tie.Message}");
                    }
                }
            }

            if (!disconnected)
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.InternalException,
                    message = "Could not disconnect slots",
                    details = new { errors }
                };
            }

            VfxGraphPersistenceService.Persist(resource);
            return new { success = true, message = $"Disconnected {parentNode.name}:{parentSlotName} -> {childNode.name}:{childSlotName}" };
        }

        public static object GetConnections(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path is required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);

            var connections = new List<object>();
            foreach (var node in models)
            {
                if (node == graph) continue;
                foreach (var input in EnumerateSlots(node, false))
                {
                    string inputName = GetSlotName(input);
                    foreach (var sourceSlot in EnumerateLinkedSourceSlots(input))
                    {
                        var sourceNode = GetSlotOwner(sourceSlot);
                        connections.Add(new
                        {
                            fromNodeId = sourceNode != null ? sourceNode.GetInstanceID() : 0,
                            fromNodeType = sourceNode != null ? sourceNode.GetType().Name : "unknown",
                            fromSlot = GetSlotName(sourceSlot),
                            toNodeId = node.GetInstanceID(),
                            toNodeType = node.GetType().Name,
                            toSlot = inputName
                        });
                    }
                }
            }

            return new
            {
                success = true,
                message = $"Found {connections.Count} data connections",
                data = new { count = connections.Count, connections }
            };
        }

        public static object SaveGraph(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path is required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            SafeInvalidate(graph, "kStructureChanged");
            VfxGraphPersistenceService.Persist(resource);
            return new { success = true, message = $"Saved graph: {path}" };
        }

        public static object SetNodeProperty(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;
            string propName = @params["property"]?.ToString();
            JToken valueToken = @params["value"];

            if (string.IsNullOrEmpty(path) || nodeId == 0 || string.IsNullOrEmpty(propName) || valueToken == null)
                return new { success = false, message = "Path, NodeId, Property, and Value are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
            if (node == null) return new { success = false, message = $"Node {nodeId} not found" };

            // For property setting, we look for input slots
            object inputSlot = FindSlot(node, propName, false);
            if (inputSlot == null)
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.NotFound,
                    message = $"Input property '{propName}' not found on node",
                    details = new { availableProperties = CollectSlotInfo(node, false) }
                };
            }

            try 
            {
                PropertyInfo valueProp = inputSlot.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (valueProp == null) return new { success = false, message = "Value property not found on Slot" };

                object valueToSet = null;
                object currentValue = valueProp.GetValue(inputSlot);
                Type targetType = currentValue?.GetType();

                if (targetType != null)
                {
                    if (targetType == typeof(float)) valueToSet = valueToken.ToObject<float>();
                    else if (targetType == typeof(int)) valueToSet = valueToken.ToObject<int>();
                    else if (targetType == typeof(bool)) valueToSet = valueToken.ToObject<bool>();
                    else if (targetType == typeof(Vector2)) valueToSet = ManageVfxCommon.ParseVec2(valueToken);
                    else if (targetType == typeof(Vector3)) valueToSet = ManageVfxCommon.ParseVector3(valueToken); // Line 174
                    else if (targetType == typeof(Vector4)) valueToSet = ManageVfxCommon.ParseVector4(valueToken);
                    else if (targetType == typeof(Color)) valueToSet = ManageVfxCommon.ParseColor(valueToken);
                    else if (targetType == typeof(AnimationCurve)) valueToSet = ManageVfxCommon.ParseAnimationCurve(valueToken);
                    else if (targetType == typeof(Gradient)) valueToSet = ManageVfxCommon.ParseGradient(valueToken);
                    else if (targetType == typeof(string)) valueToSet = valueToken.ToString();
                    else 
                    {
                         // Try generic conversion
                         try { valueToSet = valueToken.ToObject(targetType); } catch {}
                    }
                }
                
                if (valueToSet == null) valueToSet = valueToken.ToString();

                valueProp.SetValue(inputSlot, valueToSet);
                
                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Set {propName} to {valueToSet}" };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error setting property: {ex.Message}" };
            }
        }

        private static void GetModelsRecursively(ScriptableObject model, List<ScriptableObject> result)
        {
            if (model == null) return;
            result.Add(model);

            try
            {
                // Use GetProperties to avoid AmbiguousMatchException on types with inherited 'children'
                PropertyInfo childrenProp = model.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(p => p.Name == "children");

                if (childrenProp != null)
                {
                    var children = childrenProp.GetValue(model) as IEnumerable;
                    if (children != null)
                    {
                        foreach (var child in children)
                        {
                            if (child is ScriptableObject so)
                                GetModelsRecursively(so, result);
                        }
                    }
                }
            }
            catch (System.Exception) { /* Skip nodes that can't be traversed */ }
        }

        private static object FindSlot(ScriptableObject model, string name, bool isOutput)
        {
            string propName = isOutput ? "outputSlots" : "inputSlots";
            PropertyInfo slotsProp = model.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (slotsProp == null) return null;

            var slots = slotsProp.GetValue(model) as IEnumerable;
            if (slots == null) return null;

            // Collect slots into a list for indexed access
            var slotList = new List<object>();
            foreach (var s in slots) slotList.Add(s);

            // 1. Try exact name match
            foreach (var slot in slotList)
            {
                PropertyInfo nameProp = slot.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (nameProp != null)
                {
                    string slotName = nameProp.GetValue(slot) as string;
                    if (slotName == name) return slot;
                }
            }

            // 2. Try case-insensitive name match
            foreach (var slot in slotList)
            {
                PropertyInfo nameProp = slot.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (nameProp != null)
                {
                    string slotName = nameProp.GetValue(slot) as string;
                    if (string.Equals(slotName, name, StringComparison.OrdinalIgnoreCase)) return slot;
                }
            }

            // 3. Try index-based lookup (e.g., "0", "1", "[0]", "[1]")
            string indexStr = name?.Trim('[', ']');
            if (int.TryParse(indexStr, out int index) && index >= 0 && index < slotList.Count)
            {
                return slotList[index];
            }

            // 4. If name is empty and there's exactly one slot, return it
            if (string.IsNullOrEmpty(name) && slotList.Count == 1)
            {
                return slotList[0];
            }

            return null;
        }

        private static IEnumerable<object> EnumerateSlots(ScriptableObject model, bool isOutput)
        {
            if (model == null) yield break;

            string propName = isOutput ? "outputSlots" : "inputSlots";
            PropertyInfo slotsProp = model.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (slotsProp == null) yield break;

            var slots = slotsProp.GetValue(model) as IEnumerable;
            if (slots == null) yield break;

            foreach (var slot in slots)
            {
                if (slot != null) yield return slot;
            }
        }

        private static IEnumerable<object> EnumerateLinkedSourceSlots(object inputSlot)
        {
            if (inputSlot == null) yield break;

            var linkedField = inputSlot.GetType().GetField("link", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (linkedField == null) yield break;

            object linkedObj = linkedField.GetValue(inputSlot);
            if (linkedObj == null) yield break;

            if (linkedObj is IEnumerable enumerable)
            {
                foreach (var entry in enumerable)
                {
                    object slot = ExtractLinkedSlot(entry);
                    if (slot != null) yield return slot;
                }
                yield break;
            }

            {
                object slot = ExtractLinkedSlot(linkedObj);
                if (slot != null) yield return slot;
            }
        }

        private static object ExtractLinkedSlot(object linkEntry)
        {
            if (linkEntry == null) return null;

            // Direct slot reference
            if (linkEntry.GetType().Name.Contains("VFXSlot"))
                return linkEntry;

            var slotField = linkEntry.GetType().GetField("slot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (slotField != null)
                return slotField.GetValue(linkEntry);

            var sourceField = linkEntry.GetType().GetField("source", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sourceField != null)
                return sourceField.GetValue(linkEntry);

            return null;
        }

        private static ScriptableObject GetSlotOwner(object slot)
        {
            if (slot == null) return null;

            var ownerProp = slot.GetType().GetProperty("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ownerProp != null)
                return ownerProp.GetValue(slot) as ScriptableObject;

            var ownerField = slot.GetType().GetField("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ownerField != null)
                return ownerField.GetValue(slot) as ScriptableObject;

            return null;
        }

        private static string GetSlotName(object slot)
        {
            if (slot == null) return string.Empty;
            var nameProp = slot.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (nameProp == null) return string.Empty;
            return nameProp.GetValue(slot)?.ToString() ?? string.Empty;
        }

        private static List<object> CollectSlotInfo(ScriptableObject model, bool isOutput)
        {
            var result = new List<object>();
            string propName = isOutput ? "outputSlots" : "inputSlots";
            PropertyInfo slotsProp = model.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (slotsProp == null) return result;

            var slots = slotsProp.GetValue(model) as IEnumerable;
            if (slots == null) return result;

            int idx = 0;
            foreach (var slot in slots)
            {
                string slotName = "";
                string slotType = slot.GetType().Name;

                PropertyInfo nameProp = slot.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (nameProp != null)
                    slotName = nameProp.GetValue(slot) as string ?? "";

                // Try to get the value type
                PropertyInfo valueProp = slot.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                string valueType = "unknown";
                if (valueProp != null)
                {
                    try
                    {
                        var val = valueProp.GetValue(slot);
                        valueType = val?.GetType().Name ?? valueProp.PropertyType.Name;
                    }
                    catch { valueType = valueProp.PropertyType.Name; }
                }

                result.Add(new { index = idx, name = slotName, type = slotType, valueType = valueType });
                idx++;
            }
            return result;
        }

        private static ScriptableObject GetGraph(string path, out UnityEngine.Object resource, out string error)
        {
            resource = null;
            error = null;

            // 1. Get UnityEditor.VFX.VisualEffectResource type
            var unityEditorVfxModule = System.Linq.Enumerable.FirstOrDefault(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "UnityEditor.VFXModule");
            var resourceType = unityEditorVfxModule?.GetType("UnityEditor.VFX.VisualEffectResource");
            
            if (resourceType == null)
            {
                error = "Could not find type UnityEditor.VFX.VisualEffectResource";
                return null;
            }

            // 2. Call GetResourceAtPath
            var getResourceMethod = resourceType.GetMethod("GetResourceAtPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getResourceMethod == null)
            {
                 error = "Could not find method GetResourceAtPath on VisualEffectResource";
                 return null;
            }

            // Changed to UnityEngine.Object to avoid invalid cast exception (VisualEffectResource is not ScriptableObject)
            resource = getResourceMethod.Invoke(null, new object[] { path }) as UnityEngine.Object;
            
            if (resource == null) 
            {
                 error = $"GetResourceAtPath('{path}') returned null. Asset might not exist or is not a VisualEffectResource.";
                 return null;
            }

            // 3. Get UnityEditor.VFX.VisualEffectResourceExtensions type
            var vfxGraphEditorAsm = System.Linq.Enumerable.FirstOrDefault(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Unity.VisualEffectGraph.Editor");
            var extensionsType = vfxGraphEditorAsm?.GetType("UnityEditor.VFX.VisualEffectResourceExtensions");

            if (extensionsType == null)
            {
                error = "Could not find type UnityEditor.VFX.VisualEffectResourceExtensions";
                return null;
            }

            // 4. Call GetOrCreateGraph
            var getGraphMethod = extensionsType.GetMethod("GetOrCreateGraph", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
             if (getGraphMethod == null)
            {
                 error = "Could not find method GetOrCreateGraph on VisualEffectResourceExtensions";
                 return null;
            }

            var graph = getGraphMethod.Invoke(null, new object[] { resource }) as ScriptableObject;
            if (graph == null)
            {
                error = "GetOrCreateGraph returned null.";
            }

            return graph;
        }

        /// <summary>
        /// Returns all nodes in the graph with their instance IDs, types, positions, slot information,
        /// blocks (for contexts), and settings.
        /// This is the primary introspection tool for deterministic graph building.
        /// </summary>
        public static object GetGraphInfo(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new { success = false, message = "Path is required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);

            Type vfxContextType = GetVFXType("VFXContext");
            Type vfxBlockBase = GetVFXType("VFXBlock");

            var nodeInfos = new List<object>();
            foreach (var model in models)
            {
                if (model == graph) continue; // Skip the graph root itself

                // Skip blocks — they'll be nested inside their parent context
                if (vfxBlockBase != null && vfxBlockBase.IsAssignableFrom(model.GetType()))
                    continue;

                // Get position
                Vector2 pos = Vector2.zero;
                PropertyInfo posProp = model.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (posProp != null)
                {
                    try { pos = (Vector2)posProp.GetValue(model); } catch { }
                }

                var inputSlots = CollectSlotInfo(model, false);
                var outputSlots = CollectSlotInfo(model, true);

                // Collect blocks if this is a context
                List<object> blocks = null;
                if (vfxContextType != null && vfxContextType.IsAssignableFrom(model.GetType()))
                {
                    blocks = new List<object>();
                    var directChildren = new List<ScriptableObject>();
                    GetDirectChildren(model, directChildren);

                    int blockIdx = 0;
                    foreach (var child in directChildren)
                    {
                        if (vfxBlockBase != null && vfxBlockBase.IsAssignableFrom(child.GetType()))
                        {
                            var blockInputSlots = CollectSlotInfo(child, false);
                            var blockOutputSlots = CollectSlotInfo(child, true);
                            blocks.Add(new
                            {
                                id = child.GetInstanceID(),
                                type = child.GetType().Name,
                                index = blockIdx,
                                inputSlots = blockInputSlots,
                                outputSlots = blockOutputSlots,
                                hint = "This block's id can be used with set_node_property, get_node_settings, and set_node_setting."
                            });
                            blockIdx++;
                        }
                    }
                }

                // Collect flow links for contexts
                List<object> flowOutputLinks = null;
                if (vfxContextType != null && vfxContextType.IsAssignableFrom(model.GetType()))
                {
                    flowOutputLinks = CollectFlowLinks(model, vfxContextType);
                }

                bool isContext = vfxContextType != null && vfxContextType.IsAssignableFrom(model.GetType());

                var nodeData = new Dictionary<string, object>
                {
                    { "id", model.GetInstanceID() },
                    { "type", model.GetType().Name },
                    { "name", GetNodeDisplayName(model) },
                    { "position", new { x = pos.x, y = pos.y } },
                    { "inputSlots", inputSlots },
                    { "outputSlots", outputSlots },
                    { "isContext", isContext }
                };

                if (isContext && TryGetContextCapacity(model, out int contextCapacity))
                {
                    nodeData["capacity"] = contextCapacity;
                }

                if (blocks != null)
                    nodeData["blocks"] = blocks;
                if (flowOutputLinks != null)
                    nodeData["flowOutputLinks"] = flowOutputLinks;

                nodeInfos.Add(nodeData);
            }

            return new
            {
                success = true,
                message = $"Found {nodeInfos.Count} nodes in {path}",
                data = new
                {
                    assetPath = path,
                    nodeCount = nodeInfos.Count,
                    nodes = nodeInfos
                }
            };
        }

        /// <summary>
        /// Collects flow output connections for a VFXContext node.
        /// </summary>
        private static List<object> CollectFlowLinks(ScriptableObject contextNode, Type vfxContextType)
        {
            var links = new List<object>();
            try
            {
                // VFXContext has outputFlowSlot array — each element has a link list
                var outputFlowProp = contextNode.GetType().GetProperty("outputFlowSlot",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (outputFlowProp == null)
                {
                    // Try field instead
                    var outputFlowField = contextNode.GetType().GetField("outputFlowSlot",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (outputFlowField != null)
                    {
                        var flowSlots = outputFlowField.GetValue(contextNode) as Array;
                        if (flowSlots != null)
                        {
                            for (int i = 0; i < flowSlots.Length; i++)
                            {
                                var flowSlot = flowSlots.GetValue(i);
                                CollectLinksFromFlowSlot(flowSlot, i, links);
                            }
                        }
                    }
                }
                else
                {
                    var flowSlots = outputFlowProp.GetValue(contextNode) as Array;
                    if (flowSlots != null)
                    {
                        for (int i = 0; i < flowSlots.Length; i++)
                        {
                            var flowSlot = flowSlots.GetValue(i);
                            CollectLinksFromFlowSlot(flowSlot, i, links);
                        }
                    }
                }
            }
            catch { }
            return links;
        }

        private static void CollectLinksFromFlowSlot(object flowSlot, int index, List<object> links)
        {
            if (flowSlot == null) return;
            try
            {
                // Each flow slot has a 'link' field which contains linked contexts
                var linkField = flowSlot.GetType().GetField("link",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (linkField == null) return;

                var linkedList = linkField.GetValue(flowSlot) as IEnumerable;
                if (linkedList == null) return;

                foreach (var linked in linkedList)
                {
                    // Each link item has a 'context' field pointing to the linked VFXContext
                    var contextField = linked.GetType().GetField("context",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (contextField == null) continue;

                    var linkedContext = contextField.GetValue(linked) as ScriptableObject;
                    if (linkedContext != null)
                    {
                        links.Add(new
                        {
                            fromFlowIndex = index,
                            toContextId = linkedContext.GetInstanceID(),
                            toContextType = linkedContext.GetType().Name
                        });
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Lists all available VFX node types that can be used with vfx_add_node.
        /// Optionally filter by a search term.
        /// </summary>
        public static object ListNodeTypes(JObject @params)
        {
            string filter = @params["filter"]?.ToString();
            string category = @params["category"]?.ToString(); // e.g., "operator", "context", "block"

            Type vfxModelType = GetVFXType("VFXModel");
            if (vfxModelType == null)
                return new { success = false, message = "Could not find VFXModel base type" };

            var nodeTypes = new List<object>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Cache category types once (avoid repeated assembly scans)
            Type ctxType = GetVFXType("VFXContext");
            Type blkType = GetVFXType("VFXBlock");
            Type opType = GetVFXType("VFXOperator");
            Type slotType = GetVFXType("VFXSlot");

            foreach (var asm in assemblies)
            {
                string asmName = asm.GetName().Name;
                if (asmName.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) < 0 &&
                    asmName.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t.IsAbstract || !vfxModelType.IsAssignableFrom(t)) continue;
                    if (t == vfxModelType) continue;

                    string typeName = t.Name;
                    string typeCategory = "unknown";

                    // Categorize by inheritance (using cached types)
                    if (ctxType != null && ctxType.IsAssignableFrom(t))
                        typeCategory = "context";
                    else if (blkType != null && blkType.IsAssignableFrom(t))
                        typeCategory = "block";
                    else if (opType != null && opType.IsAssignableFrom(t))
                        typeCategory = "operator";
                    else if (slotType != null && slotType.IsAssignableFrom(t))
                        typeCategory = "slot";

                    // Apply filters
                    if (!string.IsNullOrEmpty(filter) && typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (!string.IsNullOrEmpty(category) && !string.Equals(typeCategory, category, StringComparison.OrdinalIgnoreCase))
                        continue;

                    nodeTypes.Add(new { name = typeName, category = typeCategory, fullName = t.FullName });
                }
            }

            // Sort by category then name
            nodeTypes.Sort((a, b) =>
            {
                string catA = a.GetType().GetProperty("category")?.GetValue(a) as string ?? "";
                string catB = b.GetType().GetProperty("category")?.GetValue(b) as string ?? "";
                int cat = string.Compare(catA, catB, StringComparison.Ordinal);
                if (cat != 0) return cat;
                string nameA = a.GetType().GetProperty("name")?.GetValue(a) as string ?? "";
                string nameB = b.GetType().GetProperty("name")?.GetValue(b) as string ?? "";
                return string.Compare(nameA, nameB, StringComparison.Ordinal);
            });

            return new
            {
                success = true,
                message = $"Found {nodeTypes.Count} node types",
                data = new { count = nodeTypes.Count, types = nodeTypes }
            };
        }

        // ==================== P0: CONTEXT FLOW LINKING ====================

        /// <summary>
        /// Links two VFXContext nodes via their flow ports (Spawn→Init→Update→Output).
        /// This is different from data slot connections — flow links determine the system pipeline.
        /// </summary>
        public static object LinkContexts(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new { success = false, message = "Path is required" };

            int fromId = @params["fromContextId"]?.ToObject<int>() ?? 0;
            int toId = @params["toContextId"]?.ToObject<int>() ?? 0;
            int fromIndex = @params["fromFlowIndex"]?.ToObject<int>() ?? 0;
            int toIndex = @params["toFlowIndex"]?.ToObject<int>() ?? 0;

            if (fromId == 0 || toId == 0)
                return new { success = false, message = "fromContextId and toContextId are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);

            var fromNode = models.FirstOrDefault(m => m.GetInstanceID() == fromId);
            var toNode = models.FirstOrDefault(m => m.GetInstanceID() == toId);

            if (fromNode == null) return new { success = false, message = $"Source context {fromId} not found" };
            if (toNode == null) return new { success = false, message = $"Target context {toId} not found" };

            // Verify both are VFXContext types
            Type vfxContextType = GetVFXType("VFXContext");
            if (vfxContextType == null)
                return new { success = false, message = "Could not find VFXContext type" };

            if (!vfxContextType.IsAssignableFrom(fromNode.GetType()))
                return new { success = false, message = $"Node {fromId} ({fromNode.GetType().Name}) is not a VFXContext" };
            if (!vfxContextType.IsAssignableFrom(toNode.GetType()))
                return new { success = false, message = $"Node {toId} ({toNode.GetType().Name}) is not a VFXContext" };

            try
            {
                // VFXContext has flow input/output ports accessed via internal methods
                // The linking is done via: toContext.LinkFrom(fromContext, fromFlowIndex, toFlowIndex)
                // Or: fromContext.LinkTo(toContext, fromFlowIndex, toFlowIndex)
                MethodInfo linkToMethod = vfxContextType.GetMethod("LinkTo",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (linkToMethod == null)
                {
                    // Try alternative: LinkFrom
                    MethodInfo linkFromMethod = vfxContextType.GetMethod("LinkFrom",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (linkFromMethod == null)
                        return new { success = false, message = "Neither LinkTo nor LinkFrom found on VFXContext" };

                    // LinkFrom(VFXContext from, int fromIndex, int toIndex)
                    linkFromMethod.Invoke(toNode, new object[] { fromNode, fromIndex, toIndex });
                }
                else
                {
                    // LinkTo(VFXContext to, int fromIndex, int toIndex)
                    linkToMethod.Invoke(fromNode, new object[] { toNode, fromIndex, toIndex });
                }

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    message = $"Linked context {fromNode.GetType().Name}(id:{fromId}) → {toNode.GetType().Name}(id:{toId})"
                };
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                int srcFlowCount = GetFlowSlotCount(fromNode, "outputFlowSlot");
                int tgtFlowCount = GetFlowSlotCount(toNode, "inputFlowSlot");
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.InternalException,
                    message = $"Error linking contexts: {inner.Message}",
                    details = new
                    {
                        sourceType = fromNode.GetType().Name,
                        targetType = toNode.GetType().Name,
                        sourceOutputFlowSlots = srcFlowCount,
                        targetInputFlowSlots = tgtFlowCount,
                        requestedFromFlowIndex = fromIndex,
                        requestedToFlowIndex = toIndex,
                        remediation = "Ensure the source context type can flow to the target type. " +
                                      "Standard flow: Spawner → Initialize → Update → Output. " +
                                      "For GPU Events, use link_gpu_event or add a TriggerEvent block first."
                    }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error linking contexts: {ex.Message}" };
            }
        }

        // ==================== P0: BLOCK MANAGEMENT ====================

        /// <summary>
        /// Adds a VFXBlock to a VFXContext at the specified index.
        /// Blocks are the core particle logic units (Set Velocity, Gravity, etc.)
        /// </summary>
        public static object AddBlock(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? 0;
            string blockType = @params["blockType"]?.ToString() ?? @params["type"]?.ToString();
            int index = @params["index"]?.ToObject<int>() ?? -1; // -1 = append

            if (string.IsNullOrEmpty(path) || contextId == 0 || string.IsNullOrEmpty(blockType))
                return new { success = false, message = "Path, contextId, and blockType (or type) are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var contextNode = models.FirstOrDefault(m => m.GetInstanceID() == contextId);

            if (contextNode == null) return new { success = false, message = $"Context node {contextId} not found" };

            Type vfxContextType = GetVFXType("VFXContext");
            if (vfxContextType == null || !vfxContextType.IsAssignableFrom(contextNode.GetType()))
                return new { success = false, message = $"Node {contextId} is not a VFXContext" };

            // Find the block type
            Type vfxBlockBase = GetVFXType("VFXBlock");
            if (vfxBlockBase == null)
                return new { success = false, message = "Could not find VFXBlock base type" };

            Type typeToCreate = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.Name.Equals(blockType, StringComparison.OrdinalIgnoreCase)
                                     && vfxBlockBase.IsAssignableFrom(t)
                                     && !t.IsAbstract);

            if (typeToCreate == null)
                return new { success = false, message = $"Block type '{blockType}' not found (must be a VFXBlock subclass)" };

            try
            {
                // Create the block instance
                ScriptableObject blockInstance = ScriptableObject.CreateInstance(typeToCreate);
                if (blockInstance == null)
                    return new { success = false, message = $"Failed to create instance of {blockType}" };

                // Add block to context using AddChild
                MethodInfo addChildMethod = contextNode.GetType().GetMethod("AddChild",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (addChildMethod == null)
                    return new { success = false, message = "AddChild method not found on context" };

                // Use notify=false to prevent cascade invalidation crash from corrupt parameters
                addChildMethod.Invoke(contextNode, new object[] { blockInstance, index, false });

                SafeInvalidate(contextNode, "kStructureChanged");

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    id = blockInstance.GetInstanceID(),
                    message = $"Added block {blockType} to context {contextNode.GetType().Name}",
                    blockType = typeToCreate.Name
                };
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return new { success = false, message = $"Error adding block: {inner.Message}", detail = inner.StackTrace };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error adding block: {ex.Message}" };
            }
        }

        /// <summary>
        /// Removes a VFXBlock from its parent context by instance ID.
        /// </summary>
        public static object RemoveBlock(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int blockId = @params["blockId"]?.ToObject<int>() ?? (@params["id"]?.ToObject<int>() ?? 0);

            if (string.IsNullOrEmpty(path) || blockId == 0)
                return new { success = false, message = "Path and blockId (or id) are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var blockNode = models.FirstOrDefault(m => m.GetInstanceID() == blockId);

            if (blockNode == null) return new { success = false, message = $"Block {blockId} not found" };

            Type vfxBlockBase = GetVFXType("VFXBlock");
            if (vfxBlockBase == null || !vfxBlockBase.IsAssignableFrom(blockNode.GetType()))
                return new { success = false, message = $"Node {blockId} is not a VFXBlock" };

            try
            {
                // Get parent (the context that contains this block)
                // VFXModel has a GetParent() method or a parent property
                PropertyInfo parentProp = blockNode.GetType().GetProperty("GetParent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                ScriptableObject parentContext = null;

                // Try GetParent() method
                MethodInfo getParentMethod = blockNode.GetType().GetMethod("GetParent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (getParentMethod != null)
                {
                    parentContext = getParentMethod.Invoke(blockNode, null) as ScriptableObject;
                }

                if (parentContext == null)
                {
                    // Fallback: find context that contains this block
                    Type vfxContextType = GetVFXType("VFXContext");
                    foreach (var model in models)
                    {
                        if (vfxContextType != null && vfxContextType.IsAssignableFrom(model.GetType()))
                        {
                            // Check if this context contains the block
                            var contextChildren = new List<ScriptableObject>();
                            GetDirectChildren(model, contextChildren);
                            if (contextChildren.Any(c => c.GetInstanceID() == blockId))
                            {
                                parentContext = model;
                                break;
                            }
                        }
                    }
                }

                if (parentContext == null)
                    return new { success = false, message = "Could not find parent context for block" };

                // RemoveChild(VFXModel model, bool notify)
                MethodInfo removeMethod = parentContext.GetType().GetMethod("RemoveChild",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (removeMethod == null)
                    return new { success = false, message = "RemoveChild method not found on context" };

                // Use notify=false to prevent cascade invalidation crash from corrupt parameters
                removeMethod.Invoke(parentContext, new object[] { blockNode, false });

                SafeInvalidate(parentContext, "kStructureChanged");

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Removed block {blockNode.GetType().Name} from {parentContext.GetType().Name}" };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error removing block: {ex.Message}" };
            }
        }

        /// <summary>
        /// Gets direct children of a VFXModel (non-recursive, for block enumeration).
        /// </summary>
        private static void GetDirectChildren(ScriptableObject model, List<ScriptableObject> result)
        {
            try
            {
                var childrenProp = model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(p => p.Name == "children" && typeof(IEnumerable).IsAssignableFrom(p.PropertyType));

                if (childrenProp == null) return;
                var children = childrenProp.GetValue(model) as IEnumerable;
                if (children == null) return;

                foreach (var child in children)
                {
                    if (child is ScriptableObject so)
                        result.Add(so);
                }
            }
            catch { }
        }

        /// <summary>
        /// Lists available VFXBlock types that can be added to contexts.
        /// </summary>
        public static object ListBlockTypes(JObject @params)
        {
            string filter = @params["filter"]?.ToString();

            Type vfxBlockBase = GetVFXType("VFXBlock");
            if (vfxBlockBase == null)
                return new { success = false, message = "Could not find VFXBlock base type" };

            var blockTypes = new List<object>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.GetName().Name;
                if (asmName.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) < 0 &&
                    asmName.IndexOf("VisualEffect", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t.IsAbstract || !vfxBlockBase.IsAssignableFrom(t)) continue;
                    if (t == vfxBlockBase) continue;

                    string typeName = t.Name;

                    if (!string.IsNullOrEmpty(filter) && typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // Try to determine compatible contexts from VFXContextType flags attribute
                    string compatibleContexts = "unknown";
                    try
                    {
                        // VFXBlock has a compatibleContexts property
                        var compatProp = t.GetProperty("compatibleContexts",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (compatProp != null)
                        {
                            // Create temp instance to read
                            var tempBlock = ScriptableObject.CreateInstance(t);
                            if (tempBlock != null)
                            {
                                var val = compatProp.GetValue(tempBlock);
                                compatibleContexts = val?.ToString() ?? "unknown";
                                UnityEngine.Object.DestroyImmediate(tempBlock);
                            }
                        }
                    }
                    catch { }

                    blockTypes.Add(new { name = typeName, fullName = t.FullName, compatibleContexts });
                }
            }

            blockTypes.Sort((a, b) =>
            {
                string nameA = a.GetType().GetProperty("name")?.GetValue(a) as string ?? "";
                string nameB = b.GetType().GetProperty("name")?.GetValue(b) as string ?? "";
                return string.Compare(nameA, nameB, StringComparison.Ordinal);
            });

            return new
            {
                success = true,
                message = $"Found {blockTypes.Count} block types",
                data = new { count = blockTypes.Count, types = blockTypes }
            };
        }

        // ==================== P1: NODE SETTINGS ====================

        /// <summary>
        /// Sets an internal setting on a VFXModel node (context, block, or operator).
        /// Settings are distinct from slot values — they control node behavior and available ports.
        /// </summary>
        public static object SetNodeSetting(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;
            string settingName = @params["settingName"]?.ToString() ?? @params["setting"]?.ToString();
            JToken valueToken = @params["value"];

            if (string.IsNullOrEmpty(path) || nodeId == 0 || string.IsNullOrEmpty(settingName) || valueToken == null)
                return new { success = false, message = "Path, nodeId, settingName (or setting), and value are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
            if (node == null) return new { success = false, message = $"Node {nodeId} not found" };

            try
            {
                // Find the setting field via VFXSettingAttribute to get the correct type
                FieldInfo settingField = null;
                Type currentType = node.GetType();
                while (currentType != null && currentType != typeof(ScriptableObject))
                {
                    foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.Name == settingName &&
                            field.GetCustomAttributes(true).Any(a => a.GetType().Name.Contains("VFXSetting")))
                        {
                            settingField = field;
                            break;
                        }
                    }
                    if (settingField != null) break;
                    currentType = currentType.BaseType;
                }

                object convertedValue = null;

                if (settingField != null)
                {
                    // We know the exact setting field — convert value to the correct type
                    Type settingType = settingField.FieldType;
                    if (settingType.IsEnum)
                    {
                        string valStr = valueToken.ToString();
                        if (int.TryParse(valStr, out int intVal))
                            convertedValue = Enum.ToObject(settingType, intVal);
                        else
                            convertedValue = Enum.Parse(settingType, valStr, true);
                    }
                    else if (settingType == typeof(float))
                        convertedValue = valueToken.ToObject<float>();
                    else if (settingType == typeof(int))
                        convertedValue = valueToken.ToObject<int>();
                    else if (settingType == typeof(bool))
                        convertedValue = valueToken.ToObject<bool>();
                    else if (settingType == typeof(string))
                        convertedValue = valueToken.ToString();
                    else if (settingType == typeof(uint))
                        convertedValue = valueToken.ToObject<uint>();
                    else if (settingType.Name == "SerializableType")
                    {
                        string requestedType = valueToken.Type == JTokenType.Object
                            ? valueToken["typeName"]?.ToString() ?? valueToken["type"]?.ToString()
                            : valueToken.ToString();
                        convertedValue = ConvertToSerializableType(settingType, requestedType);
                        if (convertedValue == null)
                        {
                            return new
                            {
                                success = false,
                                error_code = VfxErrorCodes.ValidationError,
                                message = $"Could not resolve SerializableType from '{requestedType}'",
                                details = new { setting = settingName, nodeType = node.GetType().Name }
                            };
                        }
                    }
                    else
                    {
                        try { convertedValue = valueToken.ToObject(settingType); }
                        catch { convertedValue = valueToken.ToString(); }
                    }
                }
                else
                {
                    // Fallback: try standard JToken conversions
                    if (valueToken.Type == JTokenType.Integer)
                        convertedValue = valueToken.ToObject<int>();
                    else if (valueToken.Type == JTokenType.Float)
                        convertedValue = valueToken.ToObject<float>();
                    else if (valueToken.Type == JTokenType.Boolean)
                        convertedValue = valueToken.ToObject<bool>();
                    else
                        convertedValue = valueToken.ToString();
                }

                // Use SetSettingValue — resolve ambiguity by finding the right overload
                var setMethods = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "SetSettingValue")
                    .ToArray();

                bool settingApplied = false;
                foreach (var setMethod in setMethods)
                {
                    var parms = setMethod.GetParameters();
                    if (parms.Length == 2 && parms[0].ParameterType == typeof(string))
                    {
                        try
                        {
                            setMethod.Invoke(node, new object[] { settingName, convertedValue });
                        }
                        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is InvalidCastException || tie.InnerException is ArgumentException)
                        {
                            // Type mismatch (e.g. int vs uint) — retry with uint/long conversions
                            if (convertedValue is int intVal)
                            {
                                convertedValue = (uint)intVal;
                                setMethod.Invoke(node, new object[] { settingName, convertedValue });
                            }
                            else throw;
                        }
                        settingApplied = true;
                        break;
                    }
                }

                if (!settingApplied)
                {
                    // Fallback: set the field directly
                    if (settingField != null)
                    {
                        settingField.SetValue(node, convertedValue);
                        settingApplied = true;
                    }
                    else
                    {
                        return new { success = false, message = $"Could not find SetSettingValue method or field '{settingName}' on {node.GetType().Name}" };
                    }
                }

                // Call Invalidate to trigger recompilation of the node
                var invalidateMethods = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "Invalidate")
                    .ToArray();

                if (invalidateMethods.Length > 0)
                {
                    try
                    {
                        // Invalidate(InvalidationCause cause, VFXModel model) — try with the kSettingChanged enum
                        var invalidationCauseType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                            .FirstOrDefault(t => t.Name == "InvalidationCause" && t.IsEnum);

                        if (invalidationCauseType != null)
                        {
                            object settingChanged = Enum.Parse(invalidationCauseType, "kSettingChanged", true);
                            // Find the overload that takes (InvalidationCause, VFXModel)
                            var invalidateMethod = invalidateMethods.FirstOrDefault(m => m.GetParameters().Length >= 1
                                && m.GetParameters()[0].ParameterType == invalidationCauseType);

                            if (invalidateMethod != null)
                            {
                                var parms = invalidateMethod.GetParameters();
                                if (parms.Length == 1)
                                    invalidateMethod.Invoke(node, new object[] { settingChanged });
                                else if (parms.Length >= 2)
                                    invalidateMethod.Invoke(node, new object[] { settingChanged, node });
                            }
                        }
                    }
                    catch { /* Not critical if invalidation fails */ }
                }

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Set setting '{settingName}' to {convertedValue} on {node.GetType().Name}" };
            }
            catch (TargetInvocationException tie)
            {
                var inner = tie.InnerException ?? tie;
                return new { success = false, message = $"Error setting node setting: {inner.Message}", detail = inner.StackTrace };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error setting node setting: {ex.Message}" };
            }
        }

        /// <summary>
        /// Lists the available settings on a particular node.
        /// Uses multiple strategies: GetSettings() method, VFXSetting attribute scan, and field scanning.
        /// </summary>
        public static object GetNodeSettings(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;

            if (string.IsNullOrEmpty(path) || nodeId == 0)
                return new { success = false, message = "Path and nodeId are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
            if (node == null) return new { success = false, message = $"Node {nodeId} not found" };

            try
            {
                var settingsList = new List<object>();
                string discoveryMethod = "none";

                // Strategy 1: Try GetSettings() — any overload
                var allGetSettings = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "GetSettings")
                    .ToArray();

                foreach (var method in allGetSettings)
                {
                    try
                    {
                        var parms = method.GetParameters();
                        object result;
                        if (parms.Length == 0)
                            result = method.Invoke(node, null);
                        else if (parms.Length == 1 && parms[0].ParameterType == typeof(bool))
                            result = method.Invoke(node, new object[] { true });
                        else
                            continue;

                        if (result is IEnumerable settings)
                        {
                            foreach (var setting in settings)
                            {
                                var info = ExtractSettingInfo(setting, node);
                                if (info != null) settingsList.Add(info);
                            }
                            if (settingsList.Count > 0)
                            {
                                discoveryMethod = $"GetSettings({parms.Length} params)";
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // Strategy 2: Scan for [VFXSetting] attributed fields in the type hierarchy
                if (settingsList.Count == 0)
                {
                    Type vfxSettingAttrType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                        .FirstOrDefault(t => t.Name == "VFXSettingAttribute" || t.Name == "VFXSetting");

                    if (vfxSettingAttrType != null)
                    {
                        Type currentType = node.GetType();
                        while (currentType != null && currentType != typeof(ScriptableObject))
                        {
                            foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            {
                                bool hasSettingAttr = field.GetCustomAttributes(true)
                                    .Any(a => a.GetType().Name.Contains("VFXSetting"));

                                if (hasSettingAttr)
                                {
                                    object value = null;
                                    try { value = field.GetValue(node); } catch { }

                                    string[] enumValues = null;
                                    if (value != null && value.GetType().IsEnum)
                                        enumValues = Enum.GetNames(value.GetType());

                                    settingsList.Add(new
                                    {
                                        name = field.Name,
                                        type = field.FieldType.Name,
                                        value = value?.ToString(),
                                        enumValues
                                    });
                                }
                            }
                            currentType = currentType.BaseType;
                        }
                        if (settingsList.Count > 0)
                            discoveryMethod = "VFXSettingAttribute scan";
                    }
                }

                // Strategy 3: Try GetSetting/GetSettingValue with known common setting names
                if (settingsList.Count == 0)
                {
                    MethodInfo getSettingValueMethod = node.GetType().GetMethod("GetSettingValue",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (getSettingValueMethod != null)
                    {
                        string[] commonSettings = new[] {
                            "m_Composition", "composition", "blendMode", "sortMode",
                            "useAlphaClipping", "castShadows", "receiveShadows",
                            "spaceMode", "space", "attributeSpace",
                            "primitiveType", "shaderGraph", "topology",
                            "useSoftParticle", "colorMapping", "uvMode",
                            "attribute", "source", "channels", "sampleMode"
                        };

                        foreach (var settingName in commonSettings)
                        {
                            try
                            {
                                object val = getSettingValueMethod.Invoke(node, new object[] { settingName });
                                if (val != null)
                                {
                                    string[] enumValues = null;
                                    if (val.GetType().IsEnum)
                                        enumValues = Enum.GetNames(val.GetType());

                                    settingsList.Add(new
                                    {
                                        name = settingName,
                                        type = val.GetType().Name,
                                        value = val.ToString(),
                                        enumValues
                                    });
                                }
                            }
                            catch { /* Setting doesn't exist on this node */ }
                        }
                        if (settingsList.Count > 0)
                            discoveryMethod = "GetSettingValue probe";
                    }
                }

                return new
                {
                    success = true,
                    message = $"Found {settingsList.Count} settings on {node.GetType().Name} (via {discoveryMethod})",
                    data = new
                    {
                        nodeType = node.GetType().Name,
                        nodeFullType = node.GetType().FullName,
                        discoveryMethod,
                        settings = settingsList
                    }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error getting settings: {ex.Message}" };
            }
        }

        /// <summary>
        /// Extracts setting info from a VFXSetting struct/object returned by GetSettings().
        /// </summary>
        private static object ExtractSettingInfo(object setting, ScriptableObject node)
        {
            try
            {
                string sName = "";
                string sType = "";
                object sValue = null;

                // VFXSetting might be a struct with 'name' and 'field' fields or properties
                var nameField = setting.GetType().GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var nameProp = setting.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (nameField != null)
                    sName = nameField.GetValue(setting) as string ?? "";
                else if (nameProp != null)
                    sName = nameProp.GetValue(setting) as string ?? "";

                var fieldField = setting.GetType().GetField("field", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldField != null)
                {
                    var fieldInfo = fieldField.GetValue(setting) as FieldInfo;
                    if (fieldInfo != null)
                    {
                        sType = fieldInfo.FieldType.Name;
                        try { sValue = fieldInfo.GetValue(node); } catch { }
                    }
                }

                // Try value property/field as fallback
                if (sValue == null)
                {
                    var valueField = setting.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var valueProp = setting.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (valueField != null) { try { sValue = valueField.GetValue(setting); } catch { } }
                    else if (valueProp != null) { try { sValue = valueProp.GetValue(setting); } catch { } }
                }

                string[] enumValues = null;
                if (sValue != null && sValue.GetType().IsEnum)
                    enumValues = Enum.GetNames(sValue.GetType());

                if (string.IsNullOrEmpty(sName)) return null;

                return new { name = sName, type = sType, value = sValue?.ToString(), enumValues };
            }
            catch { return null; }
        }
        // =====================================================================
        // PHASE 2: Blackboard / Exposed Property Management
        // =====================================================================

        /// <summary>
        /// Adds a new exposed property to the VFX Graph blackboard.
        /// </summary>
        public static object AddProperty(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string propName = @params["name"]?.ToString();
            string propTypeName = @params["type"]?.ToString();
            bool exposed = @params["exposed"]?.ToObject<bool>() ?? true;
            JToken defaultValue = @params["value"];

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(propTypeName))
                return new { success = false, message = "Path, name, and type are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            try
            {
                Type vfxSlotType = ResolveVFXSlotType(propTypeName);
                if (vfxSlotType == null)
                    return new { success = false, message = $"Unknown property type '{propTypeName}'. Supported: float, int, uint, bool, Vector2, Vector3, Vector4, Color, Gradient, AnimationCurve, Texture2D, Texture3D, Cubemap, Mesh, GraphicsBuffer" };

                Type vfxParameterType = GetVFXType("VFXParameter");
                if (vfxParameterType == null)
                    return new { success = false, message = "VFXParameter type not found" };

                ScriptableObject param = ScriptableObject.CreateInstance(vfxParameterType);

                // Step 1: Set name and exposed flag via SerializedObject BEFORE adding to graph
                var so = new SerializedObject(param);

                var exposedNameSP = so.FindProperty("m_ExposedName");
                if (exposedNameSP != null)
                    exposedNameSP.stringValue = propName;
                else
                    param.name = propName;

                var exposedSP = so.FindProperty("m_Exposed");
                if (exposedSP != null)
                    exposedSP.boolValue = exposed;

                so.ApplyModifiedPropertiesWithoutUndo();

                // Step 2: Call Init(Type) to create output slots BEFORE adding to graph.
                // Without this, VFXParameter.type getter crashes on outputSlots[0] during
                // graph invalidation because no slots exist yet.
                MethodInfo initMethod = vfxParameterType.GetMethod("Init",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (initMethod != null)
                {
                    initMethod.Invoke(param, new object[] { vfxSlotType });
                }
                else
                {
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError,
                        message = "VFXParameter.Init method not found — cannot initialize parameter slots" };
                }

                // Step 3: AddChild with notify=false to prevent cascade invalidation crash.
                // VFXModel.AddChild triggers Invalidate(kStructureChanged) when notify=true,
                // which calls BuildParameterInfo on ALL parameters. Using false defers this.
                MethodInfo addMethod = VfxGraphReflectionCache.GetMethodCached(
                    graph.GetType(), "AddChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addMethod == null)
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = "AddChild method not found on graph" };

                addMethod.Invoke(graph, new object[] { param, -1, false });

                // Step 4: Safe manual invalidation now that the parameter is fully initialized
                SafeInvalidate(graph, "kStructureChanged");

                // Step 5: Set default value if provided
                if (defaultValue != null)
                {
                    try { SetParameterDefaultValue(param, vfxSlotType, defaultValue); }
                    catch { /* Non-critical */ }
                }

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    message = $"Added {(exposed ? "exposed" : "internal")} property '{propName}' of type {propTypeName}",
                    data = new { id = param.GetInstanceID(), name = propName, type = propTypeName, exposed }
                };
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                string inner = tie.InnerException?.Message ?? tie.Message;
                string innerStack = tie.InnerException?.StackTrace ?? "";
                return new { success = false, message = $"Error adding property: {inner}", stackTrace = innerStack };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error adding property: {ex.Message}", stackTrace = ex.StackTrace };
            }
        }

        /// <summary>
        /// Lists all properties (parameters) in the VFX Graph blackboard.
        /// </summary>
        public static object ListProperties(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return new { success = false, message = "Path is required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            try
            {
                Type vfxParameterType = GetVFXType("VFXParameter");
                if (vfxParameterType == null)
                    return new { success = false, message = "VFXParameter type not found" };

                var models = new List<ScriptableObject>();
                GetModelsRecursively(graph, models);

                var properties = new List<object>();
                foreach (var model in models)
                {
                    if (!vfxParameterType.IsAssignableFrom(model.GetType()))
                        continue;

                    string pName = "";
                    bool isExposed = false;
                    string pType = "";

                    var exposedNameProp = model.GetType().GetProperty("exposedName",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (exposedNameProp != null)
                        pName = exposedNameProp.GetValue(model) as string ?? "";

                    var exposedProp = model.GetType().GetProperty("exposed",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (exposedProp != null)
                        isExposed = (bool)exposedProp.GetValue(model);

                    // Get output slot type via reflection
                    var getOutputSlotMethod = model.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "GetOutputSlot" && m.GetParameters().Length == 1);
                    if (getOutputSlotMethod != null)
                    {
                        try
                        {
                            var slot = getOutputSlotMethod.Invoke(model, new object[] { 0 });
                            if (slot != null)
                                pType = slot.GetType().Name;
                        }
                        catch { /* No output slots */ }
                    }

                    properties.Add(new
                    {
                        id = model.GetInstanceID(),
                        name = pName,
                        type = pType,
                        exposed = isExposed
                    });
                }

                return new
                {
                    success = true,
                    message = $"Found {properties.Count} properties",
                    data = new { properties }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error listing properties: {ex.Message}" };
            }
        }

        /// <summary>
        /// Removes a property from the VFX Graph blackboard.
        /// </summary>
        public static object RemoveProperty(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;
            string propName = @params["name"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return new { success = false, message = "Path is required" };
            if (nodeId == 0 && string.IsNullOrEmpty(propName))
                return new { success = false, message = "Either nodeId or name is required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            try
            {
                Type vfxParameterType = GetVFXType("VFXParameter");
                var models = new List<ScriptableObject>();
                GetModelsRecursively(graph, models);

                ScriptableObject target = null;
                if (nodeId != 0)
                {
                    target = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
                }
                else
                {
                    // Find by name
                    foreach (var model in models)
                    {
                        if (vfxParameterType != null && !vfxParameterType.IsAssignableFrom(model.GetType()))
                            continue;
                        var nameProp = model.GetType().GetProperty("exposedName",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (nameProp != null && (nameProp.GetValue(model) as string) == propName)
                        {
                            target = model;
                            break;
                        }
                    }
                }

                if (target == null)
                    return new { success = false, message = "Property not found" };

                // Use notify=false to prevent cascade invalidation crash from corrupt parameters
                MethodInfo removeMethod = VfxGraphReflectionCache.GetMethodCached(
                    graph.GetType(), "RemoveChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (removeMethod == null)
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = "RemoveChild method not found on graph" };

                removeMethod.Invoke(graph, new object[] { target, false });

                SafeInvalidate(graph, "kStructureChanged");

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Removed property '{propName ?? nodeId.ToString()}'" };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error removing property: {ex.Message}" };
            }
        }

        /// <summary>
        /// Sets the default value of a property in the VFX Graph blackboard.
        /// </summary>
        public static object SetPropertyDefaultValue(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;
            string propName = @params["name"]?.ToString();
            JToken valueToken = @params["value"];

            if (string.IsNullOrEmpty(path) || valueToken == null)
                return new { success = false, message = "Path and value are required" };
            if (nodeId == 0 && string.IsNullOrEmpty(propName))
                return new { success = false, message = "Either nodeId or name is required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            try
            {
                Type vfxParameterType = GetVFXType("VFXParameter");
                var models = new List<ScriptableObject>();
                GetModelsRecursively(graph, models);

                ScriptableObject target = null;
                if (nodeId != 0)
                {
                    target = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
                }
                else
                {
                    foreach (var model in models)
                    {
                        if (vfxParameterType != null && !vfxParameterType.IsAssignableFrom(model.GetType()))
                            continue;
                        var nameProp = model.GetType().GetProperty("exposedName",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (nameProp != null && (nameProp.GetValue(model) as string) == propName)
                        {
                            target = model;
                            break;
                        }
                    }
                }

                if (target == null)
                    return new { success = false, message = "Property not found" };

                // Get the output slot to determine the type
                var outputSlots = new List<object>();
                var getOutputSlot = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "GetOutputSlot")
                    .FirstOrDefault();

                if (getOutputSlot != null)
                {
                    var slot = getOutputSlot.Invoke(target, new object[] { 0 });
                    if (slot != null)
                    {
                        // Get the slot's value property type
                        var valueProp = slot.GetType().GetProperty("value",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (valueProp != null)
                        {
                            Type slotValueType = valueProp.PropertyType;
                            object converted = ConvertJTokenToType(valueToken, slotValueType);
                            if (converted != null)
                            {
                                valueProp.SetValue(slot, converted);
                            }
                        }
                    }
                }

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Set default value for '{propName ?? nodeId.ToString()}'" };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error setting property value: {ex.Message}" };
            }
        }

        // =====================================================================
        // PHASE 3: Custom HLSL + GraphicsBuffer Helper
        // =====================================================================

        /// <summary>
        /// Sets HLSL code on a Custom HLSL block or operator node.
        /// </summary>
        public static object SetHLSLCode(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;
            string code = @params["code"]?.ToString();

            if (string.IsNullOrEmpty(path) || nodeId == 0 || string.IsNullOrEmpty(code))
                return new { success = false, message = "Path, nodeId, and code are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
            if (node == null) return new { success = false, message = $"Node {nodeId} not found" };

            try
            {
                // Custom HLSL blocks/operators store code in m_HLSLCode field
                FieldInfo hlslField = null;
                Type currentType = node.GetType();
                while (currentType != null && currentType != typeof(ScriptableObject))
                {
                    hlslField = currentType.GetField("m_HLSLCode",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (hlslField != null) break;

                    // Also try m_BodyContent / sourceCode etc
                    hlslField = currentType.GetField("m_BodyContent",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (hlslField != null) break;

                    hlslField = currentType.GetField("sourceCode",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (hlslField != null) break;

                    currentType = currentType.BaseType;
                }

                if (hlslField == null)
                {
                    // Fallback: try SetSettingValue
                    var setMethods = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "SetSettingValue")
                        .ToArray();

                    foreach (var setMethod in setMethods)
                    {
                        var parms = setMethod.GetParameters();
                        if (parms.Length == 2 && parms[0].ParameterType == typeof(string))
                        {
                            try
                            {
                                setMethod.Invoke(node, new object[] { "m_HLSLCode", code });
                                EditorUtility.SetDirty(resource);
                                AssetDatabase.SaveAssets();
                                return new { success = true, message = "Set HLSL code via SetSettingValue" };
                            }
                            catch { }

                            try
                            {
                                setMethod.Invoke(node, new object[] { "m_BodyContent", code });
                                EditorUtility.SetDirty(resource);
                                AssetDatabase.SaveAssets();
                                return new { success = true, message = "Set HLSL code via SetSettingValue (m_BodyContent)" };
                            }
                            catch { }
                        }
                    }

                    return new { success = false, message = $"No HLSL code field found on {node.GetType().Name}. Is this a Custom HLSL block/operator?" };
                }

                hlslField.SetValue(node, code);

                // Invalidate to recompile
                InvalidateNode(node);

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Set HLSL code on {node.GetType().Name} ({hlslField.Name})", data = new { fieldName = hlslField.Name, codeLength = code.Length } };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error setting HLSL code: {ex.Message}" };
            }
        }

        /// <summary>
        /// Creates a C# helper MonoBehaviour script that sets up a GraphicsBuffer and binds it to a VFX.
        /// </summary>
        public static object CreateGraphicsBufferHelper(JObject @params)
        {
            string scriptName = @params["scriptName"]?.ToString() ?? "VFXBufferBinder";
            string vfxPropertyName = @params["propertyName"]?.ToString() ?? "Buffer";
            int stride = @params["stride"]?.ToObject<int>() ?? 16; // default float4
            int count = @params["count"]?.ToObject<int>() ?? 1024;
            string scriptFolder = @params["folder"]?.ToString() ?? "Assets/Scripts";

            string scriptContent = $@"using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Auto-generated helper that creates a GraphicsBuffer and binds it to a VFX Graph property.
/// Attach this to the same GameObject as a VisualEffect component.
/// </summary>
[RequireComponent(typeof(VisualEffect))]
public class {scriptName} : MonoBehaviour
{{
    [Header(""Buffer Config"")]
    public string propertyName = ""{vfxPropertyName}"";
    public int bufferCount = {count};
    public int bufferStride = {stride};

    private GraphicsBuffer _buffer;
    private VisualEffect _vfx;

    void OnEnable()
    {{
        _vfx = GetComponent<VisualEffect>();
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCount, bufferStride);

        // Initialize with zeros
        var data = new byte[bufferCount * bufferStride];
        _buffer.SetData(data);

        _vfx.SetGraphicsBuffer(propertyName, _buffer);
    }}

    void OnDisable()
    {{
        _buffer?.Release();
        _buffer = null;
    }}

    /// <summary>
    /// Call from external scripts to update buffer data.
    /// </summary>
    public void SetData<T>(T[] data) where T : struct
    {{
        if (_buffer != null)
        {{
            _buffer.SetData(data);
            _vfx.SetGraphicsBuffer(propertyName, _buffer);
        }}
    }}

    /// <summary>
    /// Resize the buffer at runtime.
    /// </summary>
    public void Resize(int newCount)
    {{
        _buffer?.Release();
        bufferCount = newCount;
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCount, bufferStride);
        _vfx.SetGraphicsBuffer(propertyName, _buffer);
    }}
}}
";

            try
            {
                string fullPath = System.IO.Path.Combine(scriptFolder, scriptName + ".cs");
                // Ensure directory exists
                string dir = System.IO.Path.GetDirectoryName(fullPath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllText(fullPath, scriptContent);
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    message = $"Created GraphicsBuffer helper script at {fullPath}",
                    data = new { path = fullPath, propertyName = vfxPropertyName, stride, count },
                    warning = "Script creation triggered AssetDatabase.Refresh(). Tools may be briefly unavailable during domain reload."
                };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error creating script: {ex.Message}" };
            }
        }

        // =====================================================================
        // PHASE 4: GPU Events + Space Settings
        // =====================================================================

        /// <summary>
        /// Links a GPU Event context from a source context (e.g. Update triggers GPU Event on death).
        /// </summary>
        public static object LinkGPUEvent(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int sourceContextId = @params["sourceContextId"]?.ToObject<int>() ?? 0;
            int gpuEventContextId = @params["gpuEventContextId"]?.ToObject<int>() ?? 0;
            int sourceFlowIndex = @params["sourceFlowIndex"]?.ToObject<int>() ?? -1;

            if (string.IsNullOrEmpty(path) || sourceContextId == 0 || gpuEventContextId == 0)
                return new { success = false, error_code = VfxErrorCodes.ValidationError, message = "Path, sourceContextId, and gpuEventContextId are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);

            var sourceContext = models.FirstOrDefault(m => m.GetInstanceID() == sourceContextId);
            var gpuEventContext = models.FirstOrDefault(m => m.GetInstanceID() == gpuEventContextId);

            if (sourceContext == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Source context {sourceContextId} not found" };
            if (gpuEventContext == null) return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"GPU Event context {gpuEventContextId} not found" };

            try
            {
                Type vfxContextType = GetVFXType("VFXContext");
                if (vfxContextType == null)
                    return new { success = false, error_code = VfxErrorCodes.ReflectionError, message = "VFXContext type not found" };

                // Collect flow slot info for diagnostics
                int outputFlowCount = GetFlowSlotCount(sourceContext, "outputFlowSlot");
                int inputFlowCount = GetFlowSlotCount(gpuEventContext, "inputFlowSlot");

                var candidateFlowIndices = BuildCandidateFlowIndices(sourceFlowIndex, outputFlowCount);

                // Ensure graph state is fully up to date before linking.
                SafeInvalidate(sourceContext, "kSettingChanged");
                SafeInvalidate(graph, "kStructureChanged");
                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                // Try LinkTo first, then LinkFrom, capturing actual errors
                var errors = new List<string>();
                bool linked = false;
                int linkedFromIndex = -1;

                var linkToMethods = sourceContext.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "LinkTo")
                    .ToArray();
                var linkFromMethods = gpuEventContext.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "LinkFrom")
                    .ToArray();

                foreach (int candidateIndex in candidateFlowIndices)
                {
                    foreach (var method in linkToMethods)
                    {
                        var parms = method.GetParameters();
                        if (parms.Length >= 1)
                        {
                            try
                            {
                                if (parms.Length == 3)
                                    method.Invoke(sourceContext, new object[] { gpuEventContext, candidateIndex, 0 });
                                else if (parms.Length == 1)
                                    method.Invoke(sourceContext, new object[] { gpuEventContext });
                                linked = true;
                                linkedFromIndex = candidateIndex;
                                break;
                            }
                            catch (TargetInvocationException tie)
                            {
                                errors.Add($"flow:{candidateIndex} LinkTo({parms.Length}p): {tie.InnerException?.Message ?? tie.Message}");
                            }
                        }
                    }

                    if (linked) break;
                    foreach (var method in linkFromMethods)
                    {
                        var parms = method.GetParameters();
                        if (parms.Length >= 1)
                        {
                            try
                            {
                                if (parms.Length == 3)
                                    method.Invoke(gpuEventContext, new object[] { sourceContext, candidateIndex, 0 });
                                else if (parms.Length == 1)
                                    method.Invoke(gpuEventContext, new object[] { sourceContext });
                                linked = true;
                                linkedFromIndex = candidateIndex;
                                break;
                            }
                            catch (TargetInvocationException tie)
                            {
                                errors.Add($"flow:{candidateIndex} LinkFrom({parms.Length}p): {tie.InnerException?.Message ?? tie.Message}");
                            }
                        }
                    }

                    if (linked) break;
                }

                if (!linked)
                {
                    return new
                    {
                        success = false,
                        error_code = VfxErrorCodes.InternalException,
                        message = "Could not link GPU Event. GPU Events require a TriggerEvent block (e.g. TriggerEventAlways, TriggerEventOnDie) in the source context to create the GPU event flow output.",
                        details = new
                        {
                            sourceType = sourceContext.GetType().Name,
                            targetType = gpuEventContext.GetType().Name,
                            sourceOutputFlowSlots = outputFlowCount,
                            targetInputFlowSlots = inputFlowCount,
                            attemptedFlowIndices = candidateFlowIndices,
                            linkErrors = errors,
                            remediation = "1) Add a TriggerEventAlways or TriggerEventOnDie block to the source context via add_block. " +
                                          "2) Then call link_gpu_event again — the source context will have an additional GPU event flow output. " +
                                          "3) Alternatively, use link_contexts with the correct fromFlowIndex for the GPU event output."
                        }
                    };
                }

                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new
                {
                    success = true,
                    message = $"Linked GPU Event from {sourceContext.GetType().Name}[{sourceContextId}] flow:{linkedFromIndex} → {gpuEventContext.GetType().Name}[{gpuEventContextId}]",
                    data = new
                    {
                        sourceFlowIndex = linkedFromIndex,
                        sourceOutputFlowSlots = outputFlowCount,
                        targetInputFlowSlots = inputFlowCount
                    }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error_code = VfxErrorCodes.InternalException, message = $"Error linking GPU Event: {ex.Message}" };
            }
        }

        /// <summary>
        /// Sets system particle capacity for a context by walking from VFXContext to underlying VFXData.
        /// </summary>
        public static object SetCapacity(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? @params["nodeId"]?.ToObject<int>() ?? 0;
            int capacity = @params["capacity"]?.ToObject<int>() ?? -1;

            if (string.IsNullOrEmpty(path) || contextId == 0 || capacity <= 0)
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.ValidationError,
                    message = "path, contextId (or nodeId), and positive capacity are required"
                };
            }

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var contextNode = models.FirstOrDefault(m => m.GetInstanceID() == contextId);
            if (contextNode == null)
            {
                return new { success = false, error_code = VfxErrorCodes.NotFound, message = $"Context {contextId} not found" };
            }

            if (!TryGetContextData(contextNode, out object contextData))
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.NotFound,
                    message = "Could not resolve VFXData for context",
                    details = new { contextType = contextNode.GetType().Name, contextId }
                };
            }

            if (!TrySetCapacityOnData(contextData, capacity, out int previousCapacity, out string appliedVia))
            {
                return new
                {
                    success = false,
                    error_code = VfxErrorCodes.ValidationError,
                    message = "Could not set capacity on context data",
                    details = new
                    {
                        contextType = contextNode.GetType().Name,
                        dataType = contextData.GetType().Name,
                        expected = "capacity property or m_Capacity field"
                    }
                };
            }

            SafeInvalidate(contextNode, "kSettingChanged");
            SafeInvalidate(graph, "kStructureChanged");
            EditorUtility.SetDirty(resource);
            AssetDatabase.SaveAssets();

            return new
            {
                success = true,
                message = $"Set capacity to {capacity} on {contextNode.GetType().Name}[{contextId}]",
                data = new
                {
                    contextId,
                    contextType = contextNode.GetType().Name,
                    previousCapacity,
                    capacity,
                    appliedVia
                }
            };
        }

        /// <summary>
        /// Gets the number of flow slots (input or output) on a context node.
        /// </summary>
        private static int GetFlowSlotCount(ScriptableObject contextNode, string slotFieldName)
        {
            try
            {
                var field = contextNode.GetType().GetField(slotFieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    var slots = field.GetValue(contextNode) as Array;
                    return slots?.Length ?? 0;
                }
                var prop = contextNode.GetType().GetProperty(slotFieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                {
                    var slots = prop.GetValue(contextNode) as Array;
                    return slots?.Length ?? 0;
                }
            }
            catch { }
            return -1;
        }

        private static List<int> BuildCandidateFlowIndices(int requestedIndex, int outputFlowCount)
        {
            if (requestedIndex >= 0)
                return new List<int> { requestedIndex };

            var indices = new List<int>();
            if (outputFlowCount <= 0)
                return new List<int> { 0 };

            // Prefer event-like non-zero slots first, then fallback to all available.
            for (int i = 1; i < outputFlowCount; i++) indices.Add(i);
            if (!indices.Contains(0)) indices.Add(0);
            return indices;
        }

        /// <summary>
        /// Sets the simulation space on a context or block (Local vs World).
        /// This is critical for controlling particle simulation behavior.
        /// </summary>
        public static object SetSpace(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int nodeId = @params["nodeId"]?.ToObject<int>() ?? 0;
            string space = @params["space"]?.ToString(); // "Local" or "World"

            if (string.IsNullOrEmpty(path) || nodeId == 0 || string.IsNullOrEmpty(space))
                return new { success = false, message = "Path, nodeId, and space (Local/World) are required" };

            ScriptableObject graph = GetGraph(path, out UnityEngine.Object resource, out string error);
            if (graph == null) return new { success = false, message = error ?? "Could not load graph" };

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == nodeId);
            if (node == null) return new { success = false, message = $"Node {nodeId} not found" };

            try
            {
                // VFXSpace enum: Local=0, World=1
                // Try multiple approaches to set space

                // Approach 1: Use SetSettingValue for space-related settings
                string[] spaceSettingNames = new[] { "space", "spaceMode", "attributeSpace", "m_Space" };
                bool spaceSet = false;

                // Find the correct space enum type
                var vfxCoordinateSpaceType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == "VFXCoordinateSpace" && t.IsEnum);

                // Also try VFXSpace
                if (vfxCoordinateSpaceType == null)
                {
                    vfxCoordinateSpaceType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                        .FirstOrDefault(t => t.Name == "VFXSpace" && t.IsEnum);
                }

                foreach (var settingName in spaceSettingNames)
                {
                    // Check if this node has a field with [VFXSetting] attribute matching the setting name
                    FieldInfo settingField = null;
                    Type currentType = node.GetType();
                    while (currentType != null && currentType != typeof(ScriptableObject))
                    {
                        foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            if (field.Name == settingName &&
                                field.GetCustomAttributes(true).Any(a => a.GetType().Name.Contains("VFXSetting")))
                            {
                                settingField = field;
                                break;
                            }
                        }
                        if (settingField != null) break;
                        currentType = currentType.BaseType;
                    }

                    if (settingField != null)
                    {
                        object spaceValue;
                        if (settingField.FieldType.IsEnum)
                        {
                            spaceValue = Enum.Parse(settingField.FieldType, space, true);
                        }
                        else if (settingField.FieldType == typeof(bool))
                        {
                            spaceValue = space.Equals("World", StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            spaceValue = space.Equals("World", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                        }

                        // Use SetSettingValue via the correct overload
                        var setMethods = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(m => m.Name == "SetSettingValue")
                            .ToArray();

                        foreach (var setMethod in setMethods)
                        {
                            var parms = setMethod.GetParameters();
                            if (parms.Length == 2 && parms[0].ParameterType == typeof(string))
                            {
                                try
                                {
                                    setMethod.Invoke(node, new object[] { settingName, spaceValue });
                                    spaceSet = true;
                                    break;
                                }
                                catch { }
                            }
                        }

                        if (!spaceSet)
                        {
                            settingField.SetValue(node, spaceValue);
                            spaceSet = true;
                        }

                        if (spaceSet) break;
                    }
                }

                // Approach 2: Try SetSpace method directly on the node
                if (!spaceSet)
                {
                    var setSpaceMethods = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "SetSpace" || m.Name == "SetSpaceLink")
                        .ToArray();

                    foreach (var method in setSpaceMethods)
                    {
                        try
                        {
                            var parms = method.GetParameters();
                            if (parms.Length >= 1)
                            {
                                object spaceVal;
                                if (parms[0].ParameterType.IsEnum)
                                    spaceVal = Enum.Parse(parms[0].ParameterType, space, true);
                                else if (parms[0].ParameterType == typeof(int))
                                    spaceVal = space.Equals("World", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                                else
                                    continue;

                                if (parms.Length == 1)
                                    method.Invoke(node, new object[] { spaceVal });
                                else
                                    method.Invoke(node, new object[] { spaceVal, 0 });
                                spaceSet = true;
                                break;
                            }
                        }
                        catch { continue; }
                    }
                }

                // Approach 3: Set space on all input slots (blocks often inherit space from their slots)
                if (!spaceSet && vfxCoordinateSpaceType != null)
                {
                    var getInputSlot = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "GetInputSlot")
                        .FirstOrDefault();

                    var getNbInputSlots = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "GetNbInputSlots")
                        .FirstOrDefault();

                    if (getInputSlot != null && getNbInputSlots != null)
                    {
                        int nbSlots = (int)getNbInputSlots.Invoke(node, null);
                        for (int i = 0; i < nbSlots; i++)
                        {
                            var slot = getInputSlot.Invoke(node, new object[] { i });
                            if (slot != null)
                            {
                                var spaceProp = slot.GetType().GetProperty("space",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (spaceProp != null && spaceProp.CanWrite)
                                {
                                    try
                                    {
                                        object spaceVal = Enum.Parse(spaceProp.PropertyType, space, true);
                                        spaceProp.SetValue(slot, spaceVal);
                                        spaceSet = true;
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                if (!spaceSet)
                    return new { success = false, message = $"Could not set space on {node.GetType().Name}. No space-related setting or property found." };

                InvalidateNode(node);
                EditorUtility.SetDirty(resource);
                AssetDatabase.SaveAssets();

                return new { success = true, message = $"Set space to '{space}' on {node.GetType().Name}" };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Error setting space: {ex.Message}" };
            }
        }

        // =====================================================================
        // Shared Helpers
        // =====================================================================

        /// <summary>
        /// Resolves a user-friendly type name to the actual System.Type for VFX properties.
        /// </summary>
        private static Type ResolveVFXSlotType(string typeName)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "float": return typeof(float);
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "bool": return typeof(bool);
                case "vector2": return typeof(Vector2);
                case "vector3": return typeof(Vector3);
                case "vector4": return typeof(Vector4);
                case "color": return typeof(Color);
                case "gradient": return typeof(Gradient);
                case "animationcurve": case "curve": return typeof(AnimationCurve);
                case "texture2d": case "texture": return typeof(Texture2D);
                case "texture3d": return typeof(Texture3D);
                case "cubemap": return typeof(Cubemap);
                case "mesh": return typeof(Mesh);
                case "graphicsbuffer": case "buffer":
                    return typeof(GraphicsBuffer);
                default: return null;
            }
        }

        /// <summary>
        /// Converts a JToken to a target Type for setting property default values.
        /// </summary>
        private static object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                if (targetType == typeof(float)) return token.ToObject<float>();
                if (targetType == typeof(int)) return token.ToObject<int>();
                if (targetType == typeof(uint)) return token.ToObject<uint>();
                if (targetType == typeof(bool)) return token.ToObject<bool>();
                if (targetType == typeof(string)) return token.ToString();
                if (targetType == typeof(Vector2))
                {
                    var arr = token as JArray;
                    if (arr != null && arr.Count >= 2)
                        return new Vector2(arr[0].ToObject<float>(), arr[1].ToObject<float>());
                    return default(Vector2);
                }
                if (targetType == typeof(Vector3))
                {
                    var arr = token as JArray;
                    if (arr != null && arr.Count >= 3)
                        return new Vector3(arr[0].ToObject<float>(), arr[1].ToObject<float>(), arr[2].ToObject<float>());
                    return default(Vector3);
                }
                if (targetType == typeof(Vector4))
                {
                    var arr = token as JArray;
                    if (arr != null && arr.Count >= 4)
                        return new Vector4(arr[0].ToObject<float>(), arr[1].ToObject<float>(), arr[2].ToObject<float>(), arr[3].ToObject<float>());
                    return default(Vector4);
                }
                if (targetType == typeof(Color))
                {
                    var arr = token as JArray;
                    if (arr != null && arr.Count >= 3)
                    {
                        float a = arr.Count >= 4 ? arr[3].ToObject<float>() : 1f;
                        return new Color(arr[0].ToObject<float>(), arr[1].ToObject<float>(), arr[2].ToObject<float>(), a);
                    }
                    return Color.white;
                }
                return token.ToObject(targetType);
            }
            catch { return null; }
        }

        /// <summary>
        /// Attempts to set a VFXParameter's default value on its output slot.
        /// </summary>
        private static void SetParameterDefaultValue(ScriptableObject param, Type slotType, JToken valueToken)
        {
            var getOutputSlot = param.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "GetOutputSlot")
                .FirstOrDefault();

            if (getOutputSlot != null)
            {
                var slot = getOutputSlot.Invoke(param, new object[] { 0 });
                if (slot != null)
                {
                    var valueProp = slot.GetType().GetProperty("value",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (valueProp != null)
                    {
                        object converted = ConvertJTokenToType(valueToken, valueProp.PropertyType);
                        if (converted != null)
                            valueProp.SetValue(slot, converted);
                    }
                }
            }
        }

        /// <summary>
        /// Calls Invalidate on a VFX node to trigger recompilation (using safe method resolution).
        /// </summary>
        private static void InvalidateNode(ScriptableObject node)
        {
            SafeInvalidate(node, "kSettingChanged");
        }

        /// <summary>
        /// Safely invokes Invalidate on a VFX model (node, context, or graph) with the given cause.
        /// Wrapped in try/catch because invalidation can crash if any VFXParameter in the graph
        /// has uninitialized slots. This is non-fatal for structural mutations since the actual
        /// Add/RemoveChild already succeeded before invalidation runs.
        /// </summary>
        private static void SafeInvalidate(ScriptableObject model, string causeName)
        {
            if (model == null) return;
            VfxGraphPersistenceService.TryInvalidate(model, causeName);
        }

        private static string GetNodeDisplayName(ScriptableObject model)
        {
            if (model == null) return string.Empty;

            if (model.GetType().Name.IndexOf("VFXParameter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    var so = new SerializedObject(model);
                    var exposedNameProp = so.FindProperty("m_ExposedName");
                    if (exposedNameProp != null && !string.IsNullOrWhiteSpace(exposedNameProp.stringValue))
                        return exposedNameProp.stringValue;
                }
                catch
                {
                    // Keep default fallback below.
                }
            }

            return model.name;
        }

        private static object ConvertToSerializableType(Type serializableTypeType, string requestedTypeName)
        {
            if (serializableTypeType == null || string.IsNullOrWhiteSpace(requestedTypeName))
                return null;

            Type resolvedType = ResolveRuntimeTypeByName(requestedTypeName);
            if (resolvedType == null)
                return null;

            // Common constructor signature: SerializableType(Type type)
            ConstructorInfo ctor = serializableTypeType.GetConstructor(new[] { typeof(Type) });
            if (ctor != null)
                return ctor.Invoke(new object[] { resolvedType });

            object instance = Activator.CreateInstance(serializableTypeType);
            if (instance == null) return null;

            // Fallback to internal members used by some Unity versions.
            var typeField = serializableTypeType.GetField("m_Type", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (typeField != null)
            {
                typeField.SetValue(instance, resolvedType);
                return instance;
            }

            var typeProp = serializableTypeType.GetProperty("type", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (typeProp != null && typeProp.CanWrite)
            {
                typeProp.SetValue(instance, resolvedType);
                return instance;
            }

            return null;
        }

        private static Type ResolveRuntimeTypeByName(string requestedTypeName)
        {
            Type resolved = Type.GetType(requestedTypeName, false);
            if (resolved != null) return resolved;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    resolved = assembly.GetTypes().FirstOrDefault(t =>
                        string.Equals(t.FullName, requestedTypeName, StringComparison.Ordinal) ||
                        string.Equals(t.Name, requestedTypeName, StringComparison.Ordinal));
                    if (resolved != null) return resolved;
                }
                catch
                {
                    // Ignore bad reflection assemblies and continue.
                }
            }

            return null;
        }

        private static bool TryGetContextData(ScriptableObject contextNode, out object contextData)
        {
            contextData = null;
            if (contextNode == null) return false;

            try
            {
                var dataProp = contextNode.GetType().GetProperty("data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (dataProp != null)
                    contextData = dataProp.GetValue(contextNode);
            }
            catch
            {
                contextData = null;
            }

            if (contextData != null) return true;

            try
            {
                var dataField = contextNode.GetType().GetField("m_Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (dataField != null)
                    contextData = dataField.GetValue(contextNode);
            }
            catch
            {
                contextData = null;
            }

            return contextData != null;
        }

        private static bool TrySetCapacityOnData(object contextData, int capacity, out int previousCapacity, out string appliedVia)
        {
            previousCapacity = -1;
            appliedVia = null;
            if (contextData == null) return false;

            Type dataType = contextData.GetType();

            var capProp = dataType.GetProperty("capacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (capProp != null && capProp.CanWrite && capProp.PropertyType == typeof(uint))
            {
                var current = capProp.GetValue(contextData);
                previousCapacity = current is uint prev ? (int)prev : previousCapacity;
                capProp.SetValue(contextData, (uint)capacity);
                appliedVia = "property:capacity(uint)";
                return true;
            }
            if (capProp != null && capProp.CanWrite && capProp.PropertyType == typeof(int))
            {
                var current = capProp.GetValue(contextData);
                previousCapacity = current is int prev ? prev : previousCapacity;
                capProp.SetValue(contextData, capacity);
                appliedVia = "property:capacity(int)";
                return true;
            }

            var mCapacityField = dataType.GetField("m_Capacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mCapacityField != null && mCapacityField.FieldType == typeof(uint))
            {
                var current = mCapacityField.GetValue(contextData);
                previousCapacity = current is uint prev ? (int)prev : previousCapacity;
                mCapacityField.SetValue(contextData, (uint)capacity);
                appliedVia = "field:m_Capacity(uint)";
                return true;
            }
            if (mCapacityField != null && mCapacityField.FieldType == typeof(int))
            {
                var current = mCapacityField.GetValue(contextData);
                previousCapacity = current is int prev ? prev : previousCapacity;
                mCapacityField.SetValue(contextData, capacity);
                appliedVia = "field:m_Capacity(int)";
                return true;
            }

            return false;
        }

        private static bool TryGetContextCapacity(ScriptableObject contextNode, out int capacity)
        {
            capacity = 0;
            if (!TryGetContextData(contextNode, out object contextData))
                return false;

            Type dataType = contextData.GetType();

            var capProp = dataType.GetProperty("capacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (capProp != null)
            {
                var value = capProp.GetValue(contextData);
                if (value is uint asUInt) { capacity = (int)asUInt; return true; }
                if (value is int asInt) { capacity = asInt; return true; }
            }

            var mCapacityField = dataType.GetField("m_Capacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mCapacityField != null)
            {
                var value = mCapacityField.GetValue(contextData);
                if (value is uint asUInt) { capacity = (int)asUInt; return true; }
                if (value is int asInt) { capacity = asInt; return true; }
            }

            return false;
        }
    }
}
