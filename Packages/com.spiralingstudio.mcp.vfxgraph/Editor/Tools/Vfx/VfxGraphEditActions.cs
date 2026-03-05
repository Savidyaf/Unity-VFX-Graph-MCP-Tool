using System;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

namespace MCPForUnity.Editor.Tools.Vfx
{
    public static partial class VfxGraphEdit
    {
        // =====================================================================
        // Attribute Block (the primary fix for agents not using proper blocks)
        // =====================================================================

        public static object AddAttributeBlock(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? 0;
            string attribute = @params["attribute"]?.ToString();
            string composition = @params["composition"]?.ToString() ?? "Set";
            string source = @params["source"]?.ToString() ?? "Slot";
            string random = @params["random"]?.ToString() ?? "Off";
            string channels = @params["channels"]?.ToString();
            int index = @params["index"]?.ToObject<int>() ?? -1;

            if (string.IsNullOrEmpty(path) || contextId == 0 || string.IsNullOrEmpty(attribute))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError,
                    "path, contextId, and attribute are required",
                    new { availableAttributes = VfxAttributeAliases.AllBuiltInAttributes.Select(a => a.Name).ToArray() });

            if (composition == "Set") composition = "Overwrite";

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var ctx = models.FirstOrDefault(m => m.GetInstanceID() == contextId);
            if (ctx == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Context {contextId} not found");

            Type vfxBlockBase = GetVFXType("VFXBlock");
            Type setAttrType = VfxGraphReflectionCache.ResolveType("SetAttribute", vfxBlockBase);
            if (setAttrType == null)
                return VfxToolContract.Error(VfxErrorCodes.ReflectionError, "SetAttribute block type not found");

            try
            {
                var block = ScriptableObject.CreateInstance(setAttrType);
                if (block == null) return VfxToolContract.Error(VfxErrorCodes.InternalException, "Failed to create SetAttribute block");

                MethodInfo addChild = ctx.GetType().GetMethod("AddChild",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addChild == null) return VfxToolContract.Error(VfxErrorCodes.ReflectionError, "AddChild not found");

                addChild.Invoke(ctx, new object[] { block, index, false });

                var settings = new Dictionary<string, string>
                {
                    { "attribute", attribute },
                    { "Composition", composition },
                    { "Source", source },
                    { "Random", random }
                };
                if (!string.IsNullOrEmpty(channels))
                    settings["Channels"] = channels;

                ApplyBlockSettings(block, settings);
                SafeInvalidate(ctx, "kStructureChanged");
                PersistGraph(resource);

                return new
                {
                    success = true,
                    id = block.GetInstanceID(),
                    message = $"{composition} {attribute} block added to {ctx.GetType().Name}",
                    data = new { attribute, composition, source, random, channels, blockType = setAttrType.Name }
                };
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Error adding attribute block: {ex.Message}");
            }
        }

        // =====================================================================
        // Context Settings (Update Position, Age Particles, Spawn loop, etc.)
        // =====================================================================

        public static object SetContextSettings(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? 0;
            JObject settings = @params["settings"] as JObject;

            if (string.IsNullOrEmpty(path) || contextId == 0 || settings == null || !settings.HasValues)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path, contextId, and settings are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == contextId);
            if (node == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Context {contextId} not found");

            var applied = new List<string>();
            var failed = new List<object>();

            foreach (var prop in settings.Properties())
            {
                try
                {
                    FieldInfo field = FindVfxSettingField(node.GetType(), prop.Name);
                    object converted = ConvertSettingValue(field, prop.Value, out object convError);
                    if (convError != null) { failed.Add(new { setting = prop.Name, error = "conversion failed" }); continue; }

                    var setMethods = node.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.Name == "SetSettingValue" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string));

                    bool set = false;
                    foreach (var method in setMethods)
                    {
                        try { method.Invoke(node, new object[] { prop.Name, converted }); set = true; break; }
                        catch { }
                    }

                    if (!set && field != null)
                    {
                        field.SetValue(node, converted);
                        set = true;
                    }

                    if (set) applied.Add(prop.Name);
                    else failed.Add(new { setting = prop.Name, error = "no setter found" });
                }
                catch (Exception ex)
                {
                    failed.Add(new { setting = prop.Name, error = ex.Message });
                }
            }

            SafeInvalidate(node, "kSettingChanged");
            PersistGraph(resource);

            return VfxToolContract.Success(
                $"Applied {applied.Count} settings to {node.GetType().Name}",
                new { applied, failed, contextType = node.GetType().Name });
        }

        // =====================================================================
        // Output Configuration
        // =====================================================================

        public static object ConfigureOutput(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? 0;
            JObject settings = @params["settings"] as JObject;
            string orientation = @params["orientation"]?.ToString();

            if (string.IsNullOrEmpty(path) || contextId == 0)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and contextId are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var node = models.FirstOrDefault(m => m.GetInstanceID() == contextId);
            if (node == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Context {contextId} not found");

            var applied = new List<string>();

            if (settings != null)
            {
                foreach (var prop in settings.Properties())
                {
                    try
                    {
                        var settingParams = new JObject
                        {
                            ["path"] = path,
                            ["nodeId"] = contextId,
                            ["settingName"] = prop.Name,
                            ["value"] = prop.Value
                        };
                        var result = SetNodeSetting(settingParams);
                        applied.Add(prop.Name);
                    }
                    catch { }
                }
            }

            int? orientBlockId = null;
            if (!string.IsNullOrEmpty(orientation))
            {
                try
                {
                    Type vfxBlockBase = GetVFXType("VFXBlock");
                    Type orientType = VfxGraphReflectionCache.ResolveType("Orient", vfxBlockBase);
                    if (orientType != null)
                    {
                        var orientBlock = ScriptableObject.CreateInstance(orientType);
                        MethodInfo addChild = node.GetType().GetMethod("AddChild",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        addChild?.Invoke(node, new object[] { orientBlock, 0, false });
                        orientBlockId = orientBlock.GetInstanceID();
                        applied.Add($"Orient({orientation})");
                    }
                }
                catch { }
            }

            SafeInvalidate(node, "kSettingChanged");
            PersistGraph(resource);

            return VfxToolContract.Success(
                $"Configured output {node.GetType().Name}",
                new { applied, orientBlockId, contextType = node.GetType().Name });
        }

        // =====================================================================
        // Compilation
        // =====================================================================

        public static object CompileGraph(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path is required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            SafeInvalidate(graph, "kExpressionGraphChanged");
            PersistGraph(resource);

            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch { }

            return VfxToolContract.Success($"Compilation triggered for {path}");
        }

        public static object GetCompilationStatus(JObject @params)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path is required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var errors = new List<string>();
            try
            {
                var resourceType = resource.GetType();
                var getInfoMethod = resourceType.GetMethod("GetCompileInitializeErrors",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getInfoMethod != null)
                {
                    var result = getInfoMethod.Invoke(resource, null);
                    if (result is IEnumerable enumerable)
                        foreach (var e in enumerable) errors.Add(e?.ToString());
                }
            }
            catch { }

            bool isCompiling = false;
            try
            {
                var prop = graph.GetType().GetProperty("compilationPending",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null) isCompiling = (bool)prop.GetValue(graph);
            }
            catch { }

            return VfxToolContract.Success(
                errors.Count > 0 ? $"{errors.Count} compilation errors" : "No compilation errors",
                new { isCompiling, errorCount = errors.Count, errors });
        }

        // =====================================================================
        // List Attributes
        // =====================================================================

        public static object ListAttributes(JObject @params)
        {
            string path = @params?["path"]?.ToString();
            string category = @params?["category"]?.ToString();

            var attrs = VfxAttributeAliases.AllBuiltInAttributes.AsEnumerable();
            if (!string.IsNullOrEmpty(category))
                attrs = attrs.Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase));

            var result = attrs.Select(a => new
            {
                name = a.Name, type = a.Type, defaultValue = a.DefaultValue,
                category = a.Category, readOnly = a.ReadOnly, variadic = a.Variadic,
                setBlockAlias = a.ReadOnly ? null : $"Set{char.ToUpperInvariant(a.Name[0])}{a.Name.Substring(1)}",
                getOperatorAlias = $"Get{char.ToUpperInvariant(a.Name[0])}{a.Name.Substring(1)}"
            }).ToList();

            var customAttrs = new List<object>();
            if (!string.IsNullOrEmpty(path))
            {
                ScriptableObject graph = GetGraph(path, out _, out _);
                if (graph != null)
                    customAttrs = GetCustomAttributesFromGraph(graph);
            }

            return VfxToolContract.Success(
                $"Found {result.Count} built-in + {customAttrs.Count} custom attributes",
                new { builtIn = result, custom = customAttrs });
        }

        static List<object> GetCustomAttributesFromGraph(ScriptableObject graph)
        {
            var result = new List<object>();
            try
            {
                var so = new SerializedObject(graph);
                var customAttrsProp = so.FindProperty("m_CustomAttributes");
                if (customAttrsProp != null && customAttrsProp.isArray)
                {
                    for (int i = 0; i < customAttrsProp.arraySize; i++)
                    {
                        var elem = customAttrsProp.GetArrayElementAtIndex(i);
                        var name = elem.FindPropertyRelative("name")?.stringValue;
                        var type = elem.FindPropertyRelative("type")?.stringValue;
                        var desc = elem.FindPropertyRelative("description")?.stringValue;
                        if (!string.IsNullOrEmpty(name))
                            result.Add(new { name, type, description = desc, custom = true });
                    }
                }
            }
            catch { }
            return result;
        }

        // =====================================================================
        // Custom Attributes
        // =====================================================================

        public static object AddCustomAttribute(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string attrName = @params["name"]?.ToString();
            string attrType = @params["type"]?.ToString() ?? "float";
            string description = @params["description"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(attrName))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and name are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            try
            {
                var addMethod = graph.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "TryAddCustomAttribute" || m.Name == "AddCustomAttribute")
                    .OrderByDescending(m => m.GetParameters().Length)
                    .FirstOrDefault();

                if (addMethod != null)
                {
                    var methodParams = addMethod.GetParameters();

                    // The method's 2nd param is VFXValueType (enum), not System.Type
                    object vfxValueType = null;
                    if (methodParams.Length >= 2)
                    {
                        Type enumType = methodParams[1].ParameterType;
                        if (enumType.IsEnum)
                        {
                            string enumName = ResolveVFXValueTypeName(attrType);
                            vfxValueType = Enum.Parse(enumType, enumName, true);
                        }
                        else
                            vfxValueType = ResolveVFXSlotType(attrType);
                    }

                    object[] args;
                    if (methodParams.Length >= 5)
                        args = new object[] { attrName, vfxValueType, description, false, null };
                    else if (methodParams.Length >= 3)
                        args = new object[] { attrName, vfxValueType, description };
                    else
                        args = new object[] { attrName, vfxValueType };

                    addMethod.Invoke(graph, args);
                }
                else
                {
                    var so = new SerializedObject(graph);
                    var customAttrs = so.FindProperty("m_CustomAttributes");
                    if (customAttrs != null && customAttrs.isArray)
                    {
                        customAttrs.arraySize++;
                        var newElem = customAttrs.GetArrayElementAtIndex(customAttrs.arraySize - 1);
                        var nameProp = newElem.FindPropertyRelative("name");
                        if (nameProp != null) nameProp.stringValue = attrName;
                        var typeProp = newElem.FindPropertyRelative("type");
                        if (typeProp != null) typeProp.stringValue = attrType;
                        var descProp = newElem.FindPropertyRelative("description");
                        if (descProp != null) descProp.stringValue = description;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else
                    {
                        return VfxToolContract.Error(VfxErrorCodes.ReflectionError,
                            "Custom attribute API not available in this VFX Graph version");
                    }
                }

                SafeInvalidate(graph, "kStructureChanged");
                PersistGraph(resource);

                return VfxToolContract.Success(
                    $"Added custom attribute '{attrName}' ({attrType})",
                    new { name = attrName, type = attrType, description });
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Error adding custom attribute: {ex.Message}");
            }
        }

        static string ResolveVFXValueTypeName(string typeName)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "float": return "Float";
                case "float2": case "vector2": return "Float2";
                case "float3": case "vector3": return "Float3";
                case "float4": case "vector4": return "Float4";
                case "int": case "int32": return "Int32";
                case "uint": case "uint32": return "Uint32";
                case "bool": case "boolean": return "Boolean";
                case "color": return "Float4";
                case "texture2d": return "Texture2D";
                case "texture3d": return "Texture3D";
                case "mesh": return "Mesh";
                case "buffer": case "graphicsbuffer": return "Buffer";
                default: return typeName;
            }
        }

        public static object RemoveCustomAttribute(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string attrName = @params["name"]?.ToString();

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(attrName))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and name are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            try
            {
                var removeMethod = graph.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "RemoveCustomAttribute" || m.Name == "TryRemoveCustomAttribute");

                if (removeMethod != null)
                    removeMethod.Invoke(graph, new object[] { attrName });

                SafeInvalidate(graph, "kStructureChanged");
                PersistGraph(resource);

                return VfxToolContract.Success($"Removed custom attribute '{attrName}'");
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Error removing custom attribute: {ex.Message}");
            }
        }

        // =====================================================================
        // Bounds
        // =====================================================================

        public static object SetBounds(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? 0;
            JToken centerToken = @params["center"];
            JToken sizeToken = @params["size"];

            if (string.IsNullOrEmpty(path) || contextId == 0)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and contextId are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var ctx = models.FirstOrDefault(m => m.GetInstanceID() == contextId);
            if (ctx == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Context {contextId} not found");

            var applied = new List<string>();

            try
            {
                if (centerToken != null)
                {
                    SetSlotValueByName(ctx, "bounds_center", centerToken, typeof(Vector3));
                    SetSlotValueByName(ctx, "Bounds_center", centerToken, typeof(Vector3));
                    applied.Add("center");
                }
                if (sizeToken != null)
                {
                    SetSlotValueByName(ctx, "bounds_size", sizeToken, typeof(Vector3));
                    SetSlotValueByName(ctx, "Bounds_size", sizeToken, typeof(Vector3));
                    applied.Add("size");
                }

                SafeInvalidate(ctx, "kSettingChanged");
                PersistGraph(resource);

                return VfxToolContract.Success($"Set bounds on {ctx.GetType().Name}", new { applied });
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Error setting bounds: {ex.Message}");
            }
        }

        static void SetSlotValueByName(ScriptableObject node, string slotName, JToken valueToken, Type expectedType)
        {
            var inputSlots = node.GetType().GetProperty("inputSlots",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (inputSlots == null) return;

            var slots = inputSlots.GetValue(node) as IEnumerable;
            if (slots == null) return;

            foreach (var slot in slots)
            {
                var nameProp = slot.GetType().GetProperty("name",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                string name = nameProp?.GetValue(slot) as string;
                if (name == null || !name.Equals(slotName, StringComparison.OrdinalIgnoreCase)) continue;

                var valueProp = slot.GetType().GetProperty("value",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (valueProp == null) continue;

                object converted = ConvertJTokenToType(valueToken, expectedType);
                if (converted != null) valueProp.SetValue(slot, converted);
                return;
            }
        }

        // =====================================================================
        // Block Activation & Reorder
        // =====================================================================

        public static object SetBlockActivation(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int blockId = @params["blockId"]?.ToObject<int>() ?? 0;
            bool? enabled = @params["enabled"]?.ToObject<bool>();

            if (string.IsNullOrEmpty(path) || blockId == 0)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and blockId are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var block = models.FirstOrDefault(m => m.GetInstanceID() == blockId);
            if (block == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Block {blockId} not found");

            try
            {
                if (enabled.HasValue)
                {
                    var enabledProp = block.GetType().GetProperty("enabled",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (enabledProp != null && enabledProp.CanWrite)
                        enabledProp.SetValue(block, enabled.Value);
                    else
                    {
                        var so = new SerializedObject(block);
                        var sp = so.FindProperty("m_Disabled");
                        if (sp != null)
                        {
                            sp.boolValue = !enabled.Value;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }

                SafeInvalidate(block, "kSettingChanged");
                PersistGraph(resource);

                return VfxToolContract.Success(
                    $"Block {block.GetType().Name} activation set to {enabled}",
                    new { blockId, blockType = block.GetType().Name, enabled });
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Error setting activation: {ex.Message}");
            }
        }

        public static object ReorderBlock(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int blockId = @params["blockId"]?.ToObject<int>() ?? 0;
            int newIndex = @params["newIndex"]?.ToObject<int>() ?? -1;

            if (string.IsNullOrEmpty(path) || blockId == 0 || newIndex < 0)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path, blockId, and newIndex are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var block = models.FirstOrDefault(m => m.GetInstanceID() == blockId);
            if (block == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Block {blockId} not found");

            try
            {
                MethodInfo getParent = block.GetType().GetMethod("GetParent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var parent = getParent?.Invoke(block, null) as ScriptableObject;
                if (parent == null) return VfxToolContract.Error(VfxErrorCodes.NotFound, "Parent context not found");

                MethodInfo removeChild = parent.GetType().GetMethod("RemoveChild",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo addChild = parent.GetType().GetMethod("AddChild",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (removeChild == null || addChild == null)
                    return VfxToolContract.Error(VfxErrorCodes.ReflectionError, "RemoveChild/AddChild not found");

                removeChild.Invoke(parent, new object[] { block, false });
                addChild.Invoke(parent, new object[] { block, newIndex, false });

                SafeInvalidate(parent, "kStructureChanged");
                PersistGraph(resource);

                return VfxToolContract.Success(
                    $"Reordered {block.GetType().Name} to index {newIndex}",
                    new { blockId, newIndex, parentType = parent.GetType().Name });
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Error reordering block: {ex.Message}");
            }
        }

        // =====================================================================
        // Buffer Pipeline Composite
        // =====================================================================

        public static object SetupBufferPipeline(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string bufferPropertyName = @params["bufferPropertyName"]?.ToString() ?? "DataBuffer";
            string structType = @params["structType"]?.ToString() ?? "Vector3";
            int capacity = @params["capacity"]?.ToObject<int>() ?? 10000;
            string indexAttribute = @params["indexAttribute"]?.ToString() ?? "particleId";
            bool generateStruct = @params["generateStruct"]?.ToObject<bool>() ?? false;
            JArray structFields = @params["structFields"] as JArray;

            if (string.IsNullOrEmpty(path))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path is required");

            var createdNodes = new List<object>();

            var addPropResult = AddProperty(new JObject
            {
                ["path"] = path,
                ["name"] = bufferPropertyName,
                ["type"] = "GraphicsBuffer",
                ["exposed"] = true
            });
            createdNodes.Add(new { step = "add_buffer_property", result = addPropResult });

            var addSampleBufferResult = AddNode(new JObject
            {
                ["path"] = path,
                ["type"] = "SampleBuffer",
                ["position"] = new JArray(-400, 300)
            });
            createdNodes.Add(new { step = "add_sample_buffer", result = addSampleBufferResult });

            int sampleBufferId = ExtractId(addSampleBufferResult);
            if (sampleBufferId != 0 && !string.IsNullOrEmpty(structType))
            {
                var configResult = SetNodeSetting(new JObject
                {
                    ["path"] = path,
                    ["nodeId"] = sampleBufferId,
                    ["settingName"] = "m_Type",
                    ["value"] = structType
                });
                createdNodes.Add(new { step = "configure_sample_buffer_type", result = configResult });
            }

            string structCode = null;
            if (generateStruct && structFields != null && structFields.Count > 0)
            {
                structCode = GenerateVFXTypeStruct(structType, structFields);
            }

            return VfxToolContract.Success(
                $"Buffer pipeline created with property '{bufferPropertyName}'",
                new
                {
                    steps = createdNodes,
                    sampleBufferId,
                    bufferPropertyName,
                    structType,
                    generatedStruct = structCode,
                    nextSteps = new[]
                    {
                        "Connect the buffer property node to the SampleBuffer's Buffer input",
                        "Connect a GetAttribute (particleId) to the SampleBuffer's Index input",
                        "Connect SampleBuffer outputs to Initialize/Update block inputs",
                        structCode != null ? "Create the generated struct script file in your project" : null
                    }.Where(s => s != null).ToArray()
                });
        }

        static int ExtractId(object result)
        {
            if (result == null) return 0;
            var idProp = result.GetType().GetProperty("id");
            if (idProp != null) return (int)idProp.GetValue(result);
            var field = result.GetType().GetField("id");
            return field != null ? (int)field.GetValue(result) : 0;
        }

        static string GenerateVFXTypeStruct(string structName, JArray fields)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.VFX;");
            sb.AppendLine();
            sb.AppendLine("[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]");
            sb.AppendLine($"public struct {structName}");
            sb.AppendLine("{");
            foreach (var field in fields)
            {
                string fname = field["name"]?.ToString() ?? "field";
                string ftype = field["type"]?.ToString() ?? "float";
                string csType = ftype.ToLowerInvariant() switch
                {
                    "float" => "float",
                    "float2" or "vector2" => "Vector2",
                    "float3" or "vector3" => "Vector3",
                    "float4" or "vector4" => "Vector4",
                    "int" => "int",
                    "uint" => "uint",
                    "bool" => "bool",
                    "color" => "Color",
                    "matrix4x4" => "Matrix4x4",
                    _ => ftype
                };
                sb.AppendLine($"    public {csType} {fname};");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        // =====================================================================
        // Batch Execution
        // =====================================================================

        public static object BatchExecute(JObject @params)
        {
            string path = @params["path"]?.ToString();
            JArray operations = @params["operations"] as JArray;

            if (string.IsNullOrEmpty(path) || operations == null || operations.Count == 0)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and operations array are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null) return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var refMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var results = new List<object>();
            int successCount = 0;

            VfxGraphPersistenceService.DeferPersistence = true;
            try
            {
                for (int i = 0; i < operations.Count; i++)
                {
                    var op = operations[i] as JObject;
                    if (op == null) { results.Add(new { index = i, success = false, error = "null operation" }); continue; }

                    string opName = op["op"]?.ToString() ?? op["action"]?.ToString();
                    string refName = op["ref"]?.ToString();

                    var opParams = new JObject(op);
                    opParams["path"] = path;
                    ResolveSymbolicRefs(opParams, refMap);

                    object opResult;
                    try
                    {
                        opResult = DispatchBatchOp(opName, opParams);
                    }
                    catch (Exception ex)
                    {
                        opResult = new { success = false, error = ex.Message };
                    }

                    int newId = ExtractId(opResult);
                    if (!string.IsNullOrEmpty(refName) && newId != 0)
                        refMap[refName] = newId;

                    bool opSuccess = IsSuccessResult(opResult);
                    if (opSuccess) successCount++;

                    if (opSuccess && newId == 0 && string.IsNullOrEmpty(refName))
                        results.Add(new { index = i, success = true });
                    else
                        results.Add(new { index = i, op = opName, @ref = refName, id = newId != 0 ? (int?)newId : null, success = opSuccess });
                }
            }
            finally
            {
                VfxGraphPersistenceService.DeferPersistence = false;
            }

            SafeInvalidate(graph, "kStructureChanged");
            VfxGraphPersistenceService.Persist(resource);

            bool allSucceeded = successCount == operations.Count;

            object resultData;
            if (allSucceeded)
            {
                var significantResults = results.Where(r =>
                {
                    var j = JObject.FromObject(r);
                    return j["id"] != null || j["ref"]?.ToString() != null;
                }).ToList();

                resultData = new
                {
                    totalOperations = operations.Count,
                    allSucceeded = true,
                    results = significantResults.Count > 0 ? significantResults : null,
                    refs = refMap.Count > 0 ? (object)refMap : null
                };
            }
            else
            {
                resultData = new
                {
                    totalOperations = operations.Count,
                    allSucceeded = false,
                    succeeded = successCount,
                    failed = operations.Count - successCount,
                    results,
                    refs = refMap.Count > 0 ? (object)refMap : null
                };
            }

            return VfxToolContract.Success(
                $"Batch complete: {successCount}/{operations.Count} operations succeeded",
                resultData);
        }

        static object DispatchBatchOp(string op, JObject @params)
        {
            switch (op?.ToLowerInvariant())
            {
                case "add_node": return AddNode(@params);
                case "remove_node": return RemoveNode(@params);
                case "add_block": return AddBlock(@params);
                case "remove_block": return RemoveBlock(@params);
                case "add_attribute_block": return AddAttributeBlock(@params);
                case "link_contexts": return LinkContexts(@params);
                case "link_gpu_event": return LinkGPUEvent(@params);
                case "connect_nodes": return ConnectNodes(@params);
                case "disconnect_nodes": return DisconnectNodes(@params);
                case "set_node_property": return SetNodeProperty(@params);
                case "set_node_setting": return SetNodeSetting(@params);
                case "set_capacity": return SetCapacity(@params);
                case "set_space": return SetSpace(@params);
                case "set_bounds": return SetBounds(@params);
                case "set_context_settings": return SetContextSettings(@params);
                case "configure_output": return ConfigureOutput(@params);
                case "add_property": return AddProperty(@params);
                case "set_property_value": return SetPropertyDefaultValue(@params);
                case "set_hlsl_code": return SetHLSLCode(@params);
                case "set_block_activation": return SetBlockActivation(@params);
                case "reorder_block": return ReorderBlock(@params);
                case "set_data_settings": return SetDataSettings(@params);
                case "move_node": return MoveNode(@params);
                case "duplicate_node": return DuplicateNode(@params);
                case "remove_property": return RemoveProperty(@params);
                default: return VfxToolContract.Error(VfxErrorCodes.UnknownAction, $"Unknown batch op: {op}");
            }
        }

        static void ResolveSymbolicRefs(JObject obj, Dictionary<string, int> refMap)
        {
            string[] refKeys = { "contextId", "nodeId", "blockId", "fromContextId", "toContextId",
                                 "fromNodeId", "toNodeId", "contextRef", "fromRef", "toRef" };

            foreach (string key in refKeys)
            {
                string val = obj[key]?.ToString();
                if (val == null) continue;

                string lookupKey = key.EndsWith("Ref") ? key : null;
                string targetKey = key.EndsWith("Ref") ? key.Substring(0, key.Length - 3) + "Id" : key;

                if (val.StartsWith("$") && refMap.TryGetValue(val, out int resolvedId))
                {
                    obj[targetKey] = resolvedId;
                    if (lookupKey != null) obj.Remove(lookupKey);
                }
            }
        }

        // =====================================================================
        // Data Object Settings (strip mode, capacity model, etc.)
        // =====================================================================

        public static object SetDataSettings(JObject @params)
        {
            string path = @params["path"]?.ToString();
            int contextId = @params["contextId"]?.ToObject<int>() ?? 0;
            JObject settings = @params["settings"] as JObject;

            if (string.IsNullOrEmpty(path) || contextId == 0 || settings == null || !settings.HasValues)
                return VfxToolContract.Error(VfxErrorCodes.ValidationError,
                    "path, contextId, and settings (object) are required");

            ScriptableObject graph = GetGraph(path, out Object resource, out string error);
            if (graph == null)
                return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, error ?? "Could not load graph");

            var models = new List<ScriptableObject>();
            GetModelsRecursively(graph, models);
            var context = models.FirstOrDefault(m => m.GetInstanceID() == contextId);
            if (context == null)
                return VfxToolContract.Error(VfxErrorCodes.NotFound, $"Context {contextId} not found");

            try
            {
                MethodInfo getDataMethod = context.GetType().GetMethod("GetData",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (getDataMethod == null)
                    return VfxToolContract.Error(VfxErrorCodes.ValidationError,
                        $"{context.GetType().Name} does not have a GetData() method (not a context?)");

                object dataObj = getDataMethod.Invoke(context, null);
                if (dataObj == null)
                    return VfxToolContract.Error(VfxErrorCodes.NotFound,
                        "GetData() returned null -- context may not own a data model yet");

                var applied = new List<string>();
                var failed = new List<string>();

                foreach (var prop in settings.Properties())
                {
                    string settingName = prop.Name;
                    JToken valueToken = prop.Value;

                    FieldInfo field = FindSettingFieldOnObject(dataObj, settingName);
                    if (field == null)
                    {
                        failed.Add($"{settingName}: field not found");
                        continue;
                    }

                    try
                    {
                        object converted = ConvertDataSettingValue(field, valueToken);
                        field.SetValue(dataObj, converted);
                        applied.Add($"{settingName} = {converted}");
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{settingName}: {ex.Message}");
                    }
                }

                if (applied.Count > 0)
                {
                    SafeInvalidate(graph, "kStructureChanged");
                    VfxGraphPersistenceService.Persist(resource);
                }

                return VfxToolContract.Success(
                    $"Set {applied.Count} data settings on {context.GetType().Name} ({failed.Count} failed)",
                    new { applied, failed, dataType = dataObj.GetType().Name });
            }
            catch (Exception ex)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException,
                    $"Error setting data settings: {ex.Message}");
            }
        }

        private static FieldInfo FindSettingFieldOnObject(object obj, string name)
        {
            Type currentType = obj.GetType();
            while (currentType != null && currentType != typeof(ScriptableObject) && currentType != typeof(object))
            {
                foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                        return field;
                    bool hasSetting = field.GetCustomAttributes(true)
                        .Any(a => a.GetType().Name.Contains("VFXSetting"));
                    if (hasSetting && string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                        return field;
                }
                currentType = currentType.BaseType;
            }
            return null;
        }

        private static object ConvertDataSettingValue(FieldInfo field, JToken value)
        {
            Type ft = field.FieldType;

            if (ft.IsEnum)
            {
                string strVal = value.ToString();
                if (int.TryParse(strVal, out int intEnum))
                    return Enum.ToObject(ft, intEnum);
                return Enum.Parse(ft, strVal, true);
            }
            if (ft == typeof(int)) return value.ToObject<int>();
            if (ft == typeof(uint)) return value.ToObject<uint>();
            if (ft == typeof(float)) return value.ToObject<float>();
            if (ft == typeof(bool)) return value.ToObject<bool>();
            if (ft == typeof(string)) return value.ToString();

            return value.ToObject(ft);
        }

        // =====================================================================
        // ECS Integration Helpers
        // =====================================================================

        public static object CreateFromRecipe(JObject @params)
        {
            string path = @params["path"]?.ToString();
            string recipe = @params["recipe"]?.ToString();

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(recipe))
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "path and recipe are required");

            var recipeOps = ResolveRecipe(recipe, @params);
            if (recipeOps == null)
                return VfxToolContract.Error(VfxErrorCodes.NotFound,
                    $"Unknown recipe '{recipe}'",
                    new { available = new[] { "ecs_buffer_particles", "simple_spawn_particles", "gpu_event_chain", "particle_strip_trail" } });

            var batchParams = new JObject
            {
                ["path"] = path,
                ["operations"] = recipeOps
            };

            return BatchExecute(batchParams);
        }

        static JArray ResolveRecipe(string recipe, JObject @params)
        {
            int capacity = @params["capacity"]?.ToObject<int>() ?? 10000;
            string bufferName = @params["bufferName"]?.ToString() ?? "EntityBuffer";
            string structType = @params["structType"]?.ToString() ?? "Vector3";

            switch (recipe.ToLowerInvariant())
            {
                case "ecs_buffer_particles":
                    return new JArray
                    {
                        Op("add_node", "$spawn", new JObject { ["type"] = "VFXBasicSpawner", ["position"] = new JArray(-600, 0) }),
                        Op("add_node", "$init", new JObject { ["type"] = "VFXBasicInitialize", ["position"] = new JArray(-200, 0) }),
                        Op("add_node", "$update", new JObject { ["type"] = "VFXBasicUpdate", ["position"] = new JArray(200, 0) }),
                        Op("add_node", "$output", new JObject { ["type"] = "VFXPlanarPrimitiveOutput", ["position"] = new JArray(600, 0) }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$spawn", ["toContextId"] = "$init" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$init", ["toContextId"] = "$update" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$update", ["toContextId"] = "$output" }),
                        Op("set_capacity", null, new JObject { ["contextId"] = "$init", ["capacity"] = capacity }),
                        Op("add_property", "$buf", new JObject { ["name"] = bufferName, ["type"] = "GraphicsBuffer", ["exposed"] = true }),
                        Op("add_node", "$sampler", new JObject { ["type"] = "SampleBuffer", ["position"] = new JArray(-200, -300) }),
                        Op("add_attribute_block", null, new JObject { ["contextId"] = "$init", ["attribute"] = "position", ["composition"] = "Overwrite" }),
                        Op("add_attribute_block", null, new JObject { ["contextId"] = "$update", ["attribute"] = "position", ["composition"] = "Overwrite" }),
                        Op("add_block", null, new JObject { ["contextId"] = "$spawn", ["blockType"] = "ConstantSpawnRate" })
                    };

                case "simple_spawn_particles":
                    return new JArray
                    {
                        Op("add_node", "$spawn", new JObject { ["type"] = "VFXBasicSpawner", ["position"] = new JArray(-600, 0) }),
                        Op("add_node", "$init", new JObject { ["type"] = "VFXBasicInitialize", ["position"] = new JArray(-200, 0) }),
                        Op("add_node", "$update", new JObject { ["type"] = "VFXBasicUpdate", ["position"] = new JArray(200, 0) }),
                        Op("add_node", "$output", new JObject { ["type"] = "VFXPlanarPrimitiveOutput", ["position"] = new JArray(600, 0) }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$spawn", ["toContextId"] = "$init" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$init", ["toContextId"] = "$update" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$update", ["toContextId"] = "$output" }),
                        Op("set_capacity", null, new JObject { ["contextId"] = "$init", ["capacity"] = capacity }),
                        Op("add_block", null, new JObject { ["contextId"] = "$spawn", ["blockType"] = "ConstantSpawnRate" }),
                        Op("add_attribute_block", null, new JObject { ["contextId"] = "$init", ["attribute"] = "lifetime", ["composition"] = "Overwrite" }),
                        Op("add_attribute_block", null, new JObject { ["contextId"] = "$init", ["attribute"] = "velocity", ["composition"] = "Overwrite", ["random"] = "Uniform" }),
                        Op("add_attribute_block", null, new JObject { ["contextId"] = "$init", ["attribute"] = "color", ["composition"] = "Overwrite" })
                    };

                case "gpu_event_chain":
                    return new JArray
                    {
                        Op("add_node", "$spawn", new JObject { ["type"] = "VFXBasicSpawner", ["position"] = new JArray(-600, 0) }),
                        Op("add_node", "$init", new JObject { ["type"] = "VFXBasicInitialize", ["position"] = new JArray(-200, 0) }),
                        Op("add_node", "$update", new JObject { ["type"] = "VFXBasicUpdate", ["position"] = new JArray(200, 0) }),
                        Op("add_node", "$output", new JObject { ["type"] = "VFXPlanarPrimitiveOutput", ["position"] = new JArray(600, 0) }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$spawn", ["toContextId"] = "$init" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$init", ["toContextId"] = "$update" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$update", ["toContextId"] = "$output" }),
                        Op("add_block", null, new JObject { ["contextId"] = "$update", ["blockType"] = "TriggerEventOnDie" }),
                        Op("add_node", "$childInit", new JObject { ["type"] = "VFXBasicInitialize", ["position"] = new JArray(-200, 500) }),
                        Op("add_node", "$childUpdate", new JObject { ["type"] = "VFXBasicUpdate", ["position"] = new JArray(200, 500) }),
                        Op("add_node", "$childOutput", new JObject { ["type"] = "VFXPlanarPrimitiveOutput", ["position"] = new JArray(600, 500) }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$childInit", ["toContextId"] = "$childUpdate" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$childUpdate", ["toContextId"] = "$childOutput" })
                    };

                case "particle_strip_trail":
                    int stripCap = @params["stripCapacity"]?.ToObject<int>() ?? 4096;
                    int perStrip = @params["particlePerStripCount"]?.ToObject<int>() ?? 32;
                    return new JArray
                    {
                        Op("add_node", "$spawn", new JObject { ["type"] = "VFXBasicSpawner", ["position"] = new JArray(-600, 0) }),
                        Op("add_node", "$init", new JObject { ["type"] = "VFXBasicInitialize", ["position"] = new JArray(-200, 0), ["dataType"] = "ParticleStrip" }),
                        Op("set_data_settings", null, new JObject { ["contextId"] = "$init", ["settings"] = new JObject { ["dataType"] = 1, ["stripCapacity"] = stripCap, ["particlePerStripCount"] = perStrip } }),
                        Op("add_node", "$update", new JObject { ["type"] = "VFXBasicUpdate", ["position"] = new JArray(200, 0) }),
                        Op("add_node", "$output", new JObject { ["type"] = "VFXQuadStripOutput", ["position"] = new JArray(600, 0) }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$spawn", ["toContextId"] = "$init" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$init", ["toContextId"] = "$update" }),
                        Op("link_contexts", null, new JObject { ["fromContextId"] = "$update", ["toContextId"] = "$output" }),
                        Op("set_capacity", null, new JObject { ["contextId"] = "$init", ["capacity"] = capacity }),
                        Op("add_block", null, new JObject { ["contextId"] = "$spawn", ["blockType"] = "ConstantSpawnRate" }),
                        Op("add_attribute_block", null, new JObject { ["contextId"] = "$init", ["attribute"] = "lifetime", ["composition"] = "Overwrite" })
                    };

                default:
                    return null;
            }
        }

        static JObject Op(string action, string refName, JObject extra)
        {
            var obj = new JObject { ["op"] = action };
            if (refName != null) obj["ref"] = refName;
            foreach (var prop in extra.Properties())
                obj[prop.Name] = prop.Value;
            return obj;
        }

        static bool IsSuccessResult(object result)
        {
            if (result == null) return false;
            var prop = result.GetType().GetProperty("success") ?? result.GetType().GetField("success")?.DeclaringType?.GetProperty("success");
            try
            {
                var field = result.GetType().GetField("success");
                if (field != null) return (bool)field.GetValue(result);
                var p = result.GetType().GetProperty("success");
                if (p != null) return (bool)p.GetValue(result);
            }
            catch { }
            return false;
        }
    }
}
