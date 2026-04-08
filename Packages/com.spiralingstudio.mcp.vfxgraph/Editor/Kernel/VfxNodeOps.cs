// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxNodeOps.cs
//
// Phase 4B — IVfxNodeOps real implementations.
//
// This is the ONLY layer in the kernel that touches Unity's VFX graph
// mutation APIs. Phase 3 committed this as a stub; phase 4B fills every
// method body. Exceptions:
//   - DiscardChanges: already real (unchanged).
//   - AddSubgraphRef: stays NotImplementedException for Lane 4C.
//
// HARD CONSTRAINTS (from Lane 4B spec):
//   1. Zero reflection on UnityEditor.VFX.* types.
//      VFXSetting.field.SetValue uses the FieldInfo VFX Graph itself exposes —
//      that is NOT us reflecting.
//   2. Always use VfxMcpKernelHelpers.LoadGraphFromAsset(asset) for VFXGraph.
//   3. Mutating callers already gate on BusyGate + Transaction; NodeOps does
//      not repeat that gate here (tools own the gate).

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine;
using UnityEngine.VFX;
using SpiralingStudio.VfxMcp.Generated;

namespace SpiralingStudio.VfxMcp.Kernel
{
    internal sealed class VfxNodeOps : IVfxNodeOps
    {
        // ─────────────────────────── AddOperator ───────────────────────────

        public string AddOperator(string graphAssetPath, string typeFqn, Vector2 pos)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Asset not found: {graphAssetPath}", null);

            // SOFT-FORK BRIDGE — never call asset.GetResource().GetOrCreateGraph() directly.
            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null)
                throw new VfxValidationException("graph_load_failed",
                    $"Could not load VFXGraph from {graphAssetPath}", null);

            var op = VfxNodeWrappers.CreateOperator(typeFqn);
            graph.AddChild(op);
            op.position = pos;

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            return VfxKernelContainer.Identity.Mint(guid, op);
        }

        // ─────────────────────────── AddContext ────────────────────────────

        public string AddContext(string graphAssetPath, string typeFqn, Vector2 pos)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Asset not found: {graphAssetPath}", null);

            // SOFT-FORK BRIDGE
            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null)
                throw new VfxValidationException("graph_load_failed",
                    $"Could not load VFXGraph from {graphAssetPath}", null);

            var ctx = VfxNodeWrappers.CreateContext(typeFqn);
            graph.AddChild(ctx);
            ctx.position = pos;

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            return VfxKernelContainer.Identity.Mint(guid, ctx);
        }

        // ─────────────────────────── AddBlock ──────────────────────────────

        public string AddBlock(string graphAssetPath, string parentContextToken, string typeFqn, int index)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Asset not found: {graphAssetPath}", null);

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);

            var parentModel = VfxKernelContainer.Identity.Resolve(guid, parentContextToken);
            if (parentModel == null)
                throw new VfxIdentityException("node_lost",
                    $"Parent context token {parentContextToken} not found in {graphAssetPath}", null);

            var context = parentModel as VFXContext;
            if (context == null)
                throw new VfxValidationException("invalid_parent",
                    $"Token {parentContextToken} does not resolve to a VFXContext " +
                    $"(got {parentModel.GetType().Name})", null);

            var block = VfxNodeWrappers.CreateBlock(typeFqn);
            // VFXModel.AddChild(model, index=-1, notify=true)
            context.AddChild(block, index);

            return VfxKernelContainer.Identity.Mint(guid, block);
        }

        // ─────────────────────────── AddParameter ──────────────────────────
        // Erratum P-B4: replicates VFXViewController.cs:1223 bookkeeping.
        // We cannot call VFXViewController.AddVFXParameter without a controller
        // instance; instead we replicate the exact bookkeeping manually.

        public string AddParameter(string graphAssetPath, string typeFqn, Vector2 pos)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Asset not found: {graphAssetPath}", null);

            // SOFT-FORK BRIDGE
            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null)
                throw new VfxValidationException("graph_load_failed",
                    $"Could not load VFXGraph from {graphAssetPath}", null);

            // replicates VFXViewController.cs:1223 — AddVFXParameter bookkeeping
            var param = VfxNodeWrappers.CreateParameter(typeFqn);
            graph.AddChild(param);
            param.position = pos;

            // collapsed = true  (VFXViewController.cs:1235)
            param.collapsed = true;

            // order = max existing + 1  (VFXViewController.cs:1237-1241)
            int order = 0;
            var existingParams = new List<VFXParameter>();
            foreach (var child in graph.children)
            {
                if (child is VFXParameter p)
                    existingParams.Add(p);
            }
            if (existingParams.Count > 0)
                order = existingParams.Select(p => p.order).Max() + 1;
            param.order = order;

            // m_ExposedName = "New <TypeName>"  (VFXViewController.cs:1242)
            // SetSettingValue is on VFXModel (public) — not reflection.
            System.Type paramType = param.type;
            string niceName = paramType != null
                ? ObjectNames.NicifyVariableName(paramType.Name)
                : typeFqn;
            param.SetSettingValue("m_ExposedName", $"New {niceName}");

            // seed default value for non-primitive types  (VFXViewController.cs:1244-1247)
            if (paramType != null && !paramType.IsPrimitive)
            {
                var defaultVal = VFXTypeExtension.GetDefaultField(paramType);
                if (defaultVal != null)
                    param.value = defaultVal;
            }

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            return VfxKernelContainer.Identity.Mint(guid, param);
        }

        // ─────────────────────────── AddSubgraphRef ────────────────────────
        // Lane 4C — implemented.

        public string AddSubgraphRef(string parentGraphPath, string subgraphAssetPath, Vector2 pos)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(parentGraphPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Parent graph not found: {parentGraphPath}", null);

            // SOFT-FORK BRIDGE — never call asset.GetResource().GetOrCreateGraph() directly.
            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null)
                throw new VfxValidationException("graph_load_failed",
                    $"Could not load VFXGraph from {parentGraphPath}", null);

            // Determine subgraph kind from file extension.
            // ERRATUM P-B5 + NEW-1: namespaces are UnityEditor.VFX.* (no .Operator. nesting).
            // Both VFXSubgraphOperator and VFXSubgraphBlock carry [VFXInfo] so the
            // VFXLibrary.Get*() catalogue entries exist — no ScriptableObject fallback needed.
            string ext = System.IO.Path.GetExtension(subgraphAssetPath).ToLowerInvariant();

            VFXModel refModel;
            switch (ext)
            {
                case ".vfxoperator":
                    refModel = VfxNodeWrappers.CreateOperator("UnityEditor.VFX.VFXSubgraphOperator");
                    break;
                case ".vfxblock":
                    refModel = VfxNodeWrappers.CreateBlock("UnityEditor.VFX.VFXSubgraphBlock");
                    break;
                default:
                    throw new VfxValidationException("validation_error",
                        $"Subgraph asset must be .vfxblock or .vfxoperator; got '{ext}'", null);
            }

            // BindAsset validates the asset class matches the model type and assigns m_Subgraph.
            VfxSubgraphWrappers.BindAsset(refModel, subgraphAssetPath);
            graph.AddChild(refModel);
            refModel.position = pos;

            string guid = AssetDatabase.AssetPathToGUID(parentGraphPath);
            return VfxKernelContainer.Identity.Mint(guid, refModel);
        }

        // ─────────────────────────── RemoveNode ────────────────────────────

        public void RemoveNode(string graphAssetPath, string token)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Asset not found: {graphAssetPath}", null);

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            if (model == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in {graphAssetPath}", null);

            var parent = model.GetParent();
            parent?.RemoveChild(model);
        }

        // ─────────────────────────── Connect ───────────────────────────────

        public void Connect(string graphAssetPath,
            string fromToken, string fromSlotName,
            string toToken,   string toSlotName)
        {
            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);

            var fromModel = VfxKernelContainer.Identity.Resolve(guid, fromToken);
            if (fromModel == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {fromToken} not found in {graphAssetPath}", null);

            var toModel = VfxKernelContainer.Identity.Resolve(guid, toToken);
            if (toModel == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {toToken} not found in {graphAssetPath}", null);

            var fromContainer = fromModel as IVFXSlotContainer;
            var toContainer   = toModel   as IVFXSlotContainer;

            if (fromContainer == null)
                throw new VfxValidationException("invalid_node",
                    $"Token {fromToken} ({fromModel.GetType().Name}) is not an IVFXSlotContainer", null);
            if (toContainer == null)
                throw new VfxValidationException("invalid_node",
                    $"Token {toToken} ({toModel.GetType().Name}) is not an IVFXSlotContainer", null);

            // output slot on the "from" side, input slot on the "to" side
            var outputSlot = FindSlotByName(fromContainer, fromSlotName, input: false);
            if (outputSlot == null)
                throw new VfxValidationException("slot_not_found",
                    $"Output slot '{fromSlotName}' not found on {fromModel.GetType().Name} ({fromToken})", null);

            var inputSlot = FindSlotByName(toContainer, toSlotName, input: true);
            if (inputSlot == null)
                throw new VfxValidationException("slot_not_found",
                    $"Input slot '{toSlotName}' not found on {toModel.GetType().Name} ({toToken})", null);

            // VFXSlot.Link(VFXSlot other, bool notify = true) — verified VFXSlot.cs:799
            bool linked = outputSlot.Link(inputSlot);
            if (!linked)
                throw new VfxValidationException("link_failed",
                    $"VFXSlot.Link returned false — types may be incompatible: " +
                    $"'{fromSlotName}' → '{toSlotName}'", null);
        }

        // ─────────────────────────── Disconnect ────────────────────────────

        public void Disconnect(string graphAssetPath,
            string fromToken, string fromSlotName,
            string toToken,   string toSlotName)
        {
            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);

            var fromModel = VfxKernelContainer.Identity.Resolve(guid, fromToken);
            if (fromModel == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {fromToken} not found in {graphAssetPath}", null);

            var toModel = VfxKernelContainer.Identity.Resolve(guid, toToken);
            if (toModel == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {toToken} not found in {graphAssetPath}", null);

            var fromContainer = fromModel as IVFXSlotContainer;
            var toContainer   = toModel   as IVFXSlotContainer;

            if (fromContainer == null)
                throw new VfxValidationException("invalid_node",
                    $"Token {fromToken} ({fromModel.GetType().Name}) is not an IVFXSlotContainer", null);
            if (toContainer == null)
                throw new VfxValidationException("invalid_node",
                    $"Token {toToken} ({toModel.GetType().Name}) is not an IVFXSlotContainer", null);

            var outputSlot = FindSlotByName(fromContainer, fromSlotName, input: false);
            if (outputSlot == null)
                throw new VfxValidationException("slot_not_found",
                    $"Output slot '{fromSlotName}' not found on {fromModel.GetType().Name} ({fromToken})", null);

            var inputSlot = FindSlotByName(toContainer, toSlotName, input: true);
            if (inputSlot == null)
                throw new VfxValidationException("slot_not_found",
                    $"Input slot '{toSlotName}' not found on {toModel.GetType().Name} ({toToken})", null);

            // VFXSlot.Unlink(VFXSlot other, bool notify = true) — verified VFXSlot.cs:821
            outputSlot.Unlink(inputSlot);
        }

        // ─────────────────────────── SetSetting ────────────────────────────

        public void SetSetting(string graphAssetPath, string token, string name, object value)
        {
            // F-A5 (v0.3.1): @graph synthetic token dispatches to the VFXGraph itself.
            if (token == "@graph")
            {
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
                if (asset == null)
                    throw new VfxValidationException("asset_not_found",
                        $"Asset not found: {graphAssetPath}", null);

                var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
                if (graph == null)
                    throw new VfxValidationException("graph_load_failed",
                        $"Could not load VFXGraph from {graphAssetPath}", null);

                // Reuse the existing GetSettings/SetValue path on the graph itself.
                foreach (var setting in graph.GetSettings(true, VFXSettingAttribute.VisibleFlags.Default))
                {
                    if (setting.name == name || setting.name == "m_" + name)
                    {
                        if (setting.field.FieldType == typeof(SerializableType))
                        {
                            var t = System.Type.GetType((string)value) ?? typeof(Vector3);
                            setting.field.SetValue(setting.instance, (SerializableType)t);
                        }
                        else
                        {
                            // F7: dispatch through the generated coercer catalog instead of
                            // Convert.ChangeType, which silently drops Vector/Color/enum inputs.
                            var coerced = VfxCoercerDispatch.Coerce(value, setting.field.FieldType);
                            setting.field.SetValue(setting.instance, coerced);
                        }
                        graph.Invalidate(VFXModel.InvalidationCause.kSettingChanged);
                        return;
                    }
                }

                throw new VfxValidationException("setting_not_found",
                    $"VFXGraph has no setting '{name}'", null);
            }

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            if (model == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in {graphAssetPath}", null);

            // GetSettings(listHidden: true) — verified VFXModel.cs:431
            // VisibleFlags has no "All" — pass Default which covers InGraph|InInspector|InGeneratedCodeComments.
            // listHidden:true also surfaces VisibleFlags.None fields (like m_ExposedName).
            foreach (var setting in model.GetSettings(true, VFXSettingAttribute.VisibleFlags.Default))
            {
                if (setting.name == name || setting.name == "m_" + name)
                {
                    if (setting.field.FieldType == typeof(SerializableType))
                    {
                        // SerializableType wraps a System.Type via implicit cast (VFXSerializer.cs).
                        // Phase 4a F2 fix: validate strictly so a typo or wrong-typed caller can't
                        // silently end up with the wrong type written to disk (gap #15 reintroduction).
                        if (!(value is string typeName))
                            throw new VfxValidationException(
                                "invalid_type_name",
                                $"Setting '{name}' on {model.GetType().Name} is a SerializableType " +
                                $"and requires a string type-name; got {value?.GetType().Name ?? "null"}",
                                null);
                        var t = System.Type.GetType(typeName);
                        if (t == null)
                            throw new VfxValidationException(
                                "invalid_type_name",
                                $"Could not resolve type name '{typeName}' for setting '{name}' on " +
                                $"{model.GetType().Name}. Pass an assembly-qualified name (the form " +
                                $"returned by GetSetting on the round-trip).",
                                null);
                        setting.field.SetValue(setting.instance, (SerializableType)t);
                    }
                    else
                    {
                        // F7: dispatch through the generated coercer catalog instead of
                        // Convert.ChangeType, which silently drops Vector/Color/enum inputs.
                        var coerced = VfxCoercerDispatch.Coerce(value, setting.field.FieldType);
                        setting.field.SetValue(setting.instance, coerced);
                    }
                    // Notify the graph of the setting change.
                    // VFXModel.Invalidate(InvalidationCause) — single-arg, verified VFXModel.cs
                    model.Invalidate(VFXModel.InvalidationCause.kSettingChanged);
                    return;
                }
            }

            throw new VfxValidationException("setting_not_found",
                $"{model.GetType().Name} has no setting '{name}'", null);
        }

        // ─────────────────────────── GetSetting ────────────────────────────

        public object GetSetting(string graphAssetPath, string token, string name)
        {
            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            if (model == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in {graphAssetPath}", null);

            foreach (var setting in model.GetSettings(true, VFXSettingAttribute.VisibleFlags.Default))
            {
                if (setting.name == name || setting.name == "m_" + name)
                {
                    var val = setting.value;
                    // SerializableType → return the underlying System.Type FQN string
                    if (val is SerializableType st)
                    {
                        System.Type t = st;
                        return t?.AssemblyQualifiedName;
                    }
                    return val;
                }
            }

            throw new VfxValidationException("setting_not_found",
                $"{model.GetType().Name} has no setting '{name}'", null);
        }

        // ─────────────────────────── SetProperty ───────────────────────────

        public void SetProperty(string graphAssetPath, string token, string name, object value)
        {
            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            if (model == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in {graphAssetPath}", null);

            var container = model as IVFXSlotContainer;
            if (container == null)
                throw new VfxValidationException("invalid_node",
                    $"Token {token} ({model.GetType().Name}) is not an IVFXSlotContainer", null);

            var slot = FindSlotByName(container, name, input: true);
            if (slot == null)
                throw new VfxValidationException("slot_not_found",
                    $"{model.GetType().Name} ({token}) has no input slot '{name}'", null);

            // VFXSlot.value setter — verified VFXSlot.cs:62+
            slot.value = value;
        }

        // ─────────────────────────── GetProperty ───────────────────────────

        public object GetProperty(string graphAssetPath, string token, string name)
        {
            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            if (model == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in {graphAssetPath}", null);

            var container = model as IVFXSlotContainer;
            if (container == null)
                throw new VfxValidationException("invalid_node",
                    $"Token {token} ({model.GetType().Name}) is not an IVFXSlotContainer", null);

            // Try input slots first, then output slots (e.g. VFXParameter has output)
            var slot = FindSlotByName(container, name, input: true)
                    ?? FindSlotByName(container, name, input: false);
            if (slot == null)
                throw new VfxValidationException("slot_not_found",
                    $"{model.GetType().Name} ({token}) has no slot '{name}'", null);

            return slot.value;
        }

        // ─────────────────────────── DiscardChanges ────────────────────────

        public void DiscardChanges(string graphAssetPath)
        {
            // Force a re-import of the asset from disk, discarding any in-memory
            // mutations to the underlying VisualEffectAsset / VFXGraph. This is
            // safe to call before phase 4 lands the tool layer because it only
            // touches AssetDatabase, which is always available in the editor.
            AssetDatabase.ImportAsset(
                graphAssetPath,
                ImportAssetOptions.ForceUpdate);
            // VFXViewController re-fetch happens naturally via
            // GetController(..., forceUpdate: true) on the next access.
        }

        // ─────────────────────────── ListNodes ──────────────────────────────

        public IReadOnlyList<VfxNodeListEntry> ListNodes(string graphAssetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphAssetPath);
            if (asset == null)
                throw new VfxValidationException("asset_not_found",
                    $"Asset not found: {graphAssetPath}", null);

            var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
            if (graph == null)
                throw new VfxValidationException("graph_load_failed",
                    $"Could not load VFXGraph from {graphAssetPath}", null);

            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var entries = new List<VfxNodeListEntry>();

            foreach (var child in graph.children)
            {
                if (child == null) continue;

                string token = VfxKernelContainer.Identity.Mint(guid, child);
                string category = ClassifyTopLevel(child);

                entries.Add(new VfxNodeListEntry
                {
                    Token       = token,
                    TypeFqn     = child.GetType().FullName,
                    Position    = child.position,
                    ParentToken = null,        // top-level
                    Category    = category,
                    BlockIndex  = -1,
                });

                // Recurse one level into VFXContext.children for blocks.
                if (child is VFXContext ctx)
                {
                    int blockIdx = 0;
                    foreach (var block in ctx.children)
                    {
                        if (block == null) { blockIdx++; continue; }
                        string blockToken = VfxKernelContainer.Identity.Mint(guid, block);
                        entries.Add(new VfxNodeListEntry
                        {
                            Token       = blockToken,
                            TypeFqn     = block.GetType().FullName,
                            Position    = block.position,
                            ParentToken = token,
                            Category    = "block",
                            BlockIndex  = blockIdx,
                        });
                        blockIdx++;
                    }
                }
            }

            // Flush sidecar so freshly minted tokens persist for the next call.
            VfxKernelContainer.Identity.Flush();

            return entries;
        }

        private static string ClassifyTopLevel(VFXModel m)
        {
            if (m is VFXContext)              return "context";
            if (m is VFXParameter)            return "parameter";
            if (m is VFXSubgraphOperator
                || m is VFXSubgraphBlock)     return "subgraph_ref";
            return "operator";
        }

        // ─────────────────────────── MoveNode ───────────────────────────────

        public void MoveNode(string graphAssetPath, string token, Vector2 position)
        {
            string guid = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var model = VfxKernelContainer.Identity.Resolve(guid, token);
            if (model == null)
                throw new VfxIdentityException("node_lost",
                    $"Token {token} not found in {graphAssetPath}", null);

            model.position = position;
            // Position changes don't fire kSettingChanged; the editor picks them up
            // on the next save / asset re-import.
        }

        // ─────────────────────────── Private helpers ───────────────────────

        /// <summary>
        /// Find a slot by its property name on an IVFXSlotContainer.
        /// VFXSlot.property.name — verified VFXSlot.cs:19 (name getter returns m_Property.name).
        /// </summary>
        private static VFXSlot FindSlotByName(IVFXSlotContainer container, string slotName, bool input)
        {
            var slots = input ? container.inputSlots : container.outputSlots;
            foreach (var s in slots)
                if (s.property.name == slotName)
                    return s;
            return null;
        }
    }
}
