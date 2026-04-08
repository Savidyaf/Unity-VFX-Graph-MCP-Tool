// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxYamlVerifierTests.cs
//
// Lane 3B tasks 3B-4 + 3B-5: tests for the partial UnityYAML reader that
// powers the YAML half of the three-part health gate.
//
// IMPORTANT (per erratum P-L3): sidecar tokens NEVER appear in the .vfx YAML
// by design — identity is sidecar-only. The verifier therefore matches by
// (typeFqn, structural shape), NOT by token substring. The token field on
// VfxIntentOp is propagated only into the error envelope so the caller can
// correlate the diagnostic back to the originating intent.
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SpiralingStudio.VfxMcp.Kernel;
using UnityEditor;
using UnityEditor.VFX;
using UnityEngine.VFX;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxYamlVerifierTests
    {
        // ──────────────────── Task 3B-4 ────────────────────

        [Test]
        public void Verify_DetectsMissingNode_WhenIntentTypeFqnAbsent()
        {
            // Hand-crafted YAML containing one MonoBehaviour block whose
            // m_Script GUID points to "ExistingType". The intent has an `add`
            // op for "Missing.Type", which is NOT present in the YAML, so the
            // verifier must emit `intent_diverged`.
            //
            // Per erratum P-L3, the verifier looks for `m_Script` GUID / `!u!114`
            // tag presence and matches by typeFqn declared on the intent op.
            // Tokens are not, and never will be, present in YAML.
            string yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_Script: {fileID: 11500000, guid: deadbeef, type: 3}
  m_Name: ExistingType
  m_EditorClassIdentifier:
  m_TypeFqn: ExistingType
";

            var verifier = new VfxYamlVerifier();
            var intent = new VfxIntentSnapshot
            {
                Ops = new System.Collections.Generic.List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 0,
                        Kind = "add",
                        ExpectedToken = "n_missing",
                        ExpectedTypeFqn = "Missing.Type",
                    },
                },
            };

            var result = verifier.VerifyFromYaml(yaml, intent);

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Errors,
                "Expected at least one verifier error for the missing add op.");
            Assert.AreEqual("intent_diverged", result.Errors[0].Code);
            Assert.AreEqual(0, result.Errors[0].OpIndex);
            Assert.AreEqual("Missing.Type", result.Errors[0].ExpectedTypeFqn);
        }

        [Test]
        public void Verify_NoErrors_WhenIntentTypeFqnPresent()
        {
            string yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: deadbeef, type: 3}
  m_Name: PresentType
  m_TypeFqn: UnityEditor.VFX.VFXInlineOperator
";
            var verifier = new VfxYamlVerifier();
            var intent = new VfxIntentSnapshot
            {
                Ops = new System.Collections.Generic.List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 0,
                        Kind = "add",
                        ExpectedToken = "n_xxxxx",
                        ExpectedTypeFqn = "UnityEditor.VFX.VFXInlineOperator",
                    },
                },
            };

            var result = verifier.VerifyFromYaml(yaml, intent);
            Assert.IsEmpty(result.Errors,
                "Expected no errors when the intent type FQN appears in the YAML.");
        }

        [Test]
        public void Verify_IgnoresNonAddOps()
        {
            string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";
            var verifier = new VfxYamlVerifier();
            var intent = new VfxIntentSnapshot
            {
                Ops = new System.Collections.Generic.List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 0,
                        Kind = "set_setting",
                        ExpectedToken = "n_xxxxx",
                        ExpectedTypeFqn = "Anything",
                    },
                },
            };

            var result = verifier.VerifyFromYaml(yaml, intent);
            // The initial verifier only validates `add` ops in this lane;
            // other op kinds are out-of-scope for the partial reader.
            Assert.IsEmpty(result.Errors);
        }

        // ──────────────────── Task 3B-5 ────────────────────

        [Test]
        public void Verify_DetectsDroppedConnection_BasedOnLinkedSlots()
        {
            // Synthetic YAML with two MonoBehaviour blocks but no LinkedSlots
            // entry referencing them. The intent has a `connect` op which the
            // verifier must surface as `connection_dropped`.
            string yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: aaa, type: 3}
  m_Name: NodeA
  m_TypeFqn: UnityEditor.VFX.VFXInlineOperator
  m_LinkedSlots: []
--- !u!114 &2
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: bbb, type: 3}
  m_Name: NodeB
  m_TypeFqn: UnityEditor.VFX.VFXInlineOperator
  m_LinkedSlots: []
";
            var verifier = new VfxYamlVerifier();
            var intent = new VfxIntentSnapshot
            {
                Ops = new System.Collections.Generic.List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 0,
                        Kind = "connect",
                        ExpectedToken = "n_a",
                        ExpectedTypeFqn = "UnityEditor.VFX.VFXInlineOperator",
                        Payload = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["fromFileId"] = 1L,
                            ["toFileId"] = 2L,
                            ["fromSlot"] = "o",
                            ["toSlot"] = "a",
                        },
                    },
                },
            };

            var result = verifier.VerifyFromYaml(yaml, intent);
            Assert.IsTrue(
                result.Warnings.Exists(w => w.Code == "connection_dropped"),
                "Expected at least one connection_dropped warning when " +
                "the YAML has no LinkedSlots referencing the intent's endpoints.");
        }

        [Test]
        public void Verify_DetectsDriftedSlotBinding_WhenSlotNameMismatches()
        {
            // The YAML has a LinkedSlots entry but it points at a slot named
            // "different" instead of "expectedSlot". The verifier must emit a
            // slot_drift warning.
            string yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: aaa, type: 3}
  m_Name: NodeA
  m_TypeFqn: UnityEditor.VFX.VFXInlineOperator
  m_LinkedSlots:
  - m_LinkedSlot: {fileID: 2}
    m_Name: different
";
            var verifier = new VfxYamlVerifier();
            var intent = new VfxIntentSnapshot
            {
                Ops = new System.Collections.Generic.List<VfxIntentOp>
                {
                    new VfxIntentOp
                    {
                        OpIndex = 0,
                        Kind = "connect",
                        ExpectedToken = "n_a",
                        ExpectedTypeFqn = "UnityEditor.VFX.VFXInlineOperator",
                        Payload = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["fromFileId"] = 1L,
                            ["toFileId"] = 2L,
                            ["fromSlot"] = "expectedSlot",
                            ["toSlot"] = "in",
                        },
                    },
                },
            };

            var result = verifier.VerifyFromYaml(yaml, intent);
            Assert.IsTrue(
                result.Warnings.Exists(w => w.Code == "slot_drift"),
                "Expected slot_drift warning when LinkedSlots m_Name does not match.");
        }

        [Test]
        public void Verify_FromFile_ReadsYamlFromDisk()
        {
            // VfxYamlVerifier.Verify(string graphAssetPath, ...) reads the file.
            // The on-disk path is the canonical interface; VerifyFromYaml is the
            // injected-string overload used by hand-crafted tests.
            string tempPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "VfxYamlVerifierTest_" + System.Guid.NewGuid().ToString("n") + ".vfx");
            try
            {
                System.IO.File.WriteAllText(tempPath,
@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: aaa, type: 3}
  m_Name: PresentType
  m_TypeFqn: UnityEditor.VFX.VFXInlineOperator
");
                var verifier = new VfxYamlVerifier();
                var intent = new VfxIntentSnapshot
                {
                    Ops = new System.Collections.Generic.List<VfxIntentOp>
                    {
                        new VfxIntentOp
                        {
                            OpIndex = 0,
                            Kind = "add",
                            ExpectedToken = "n_xxx",
                            ExpectedTypeFqn = "UnityEditor.VFX.VFXInlineOperator",
                        },
                    },
                };
                var result = verifier.Verify(tempPath, intent);
                Assert.IsEmpty(result.Errors);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        // ──────────────────── Task 5-7: MonoScript GUID resolver ────────────

        // Phase 5-7: tests that Verify(path, intent) performs GUID->FQN
        // resolution on real .vfx YAML (which lacks m_TypeFqn lines) so the
        // strict-match path runs without emitting yaml_verify_skipped warnings.
        //
        // Strategy: create a real .vfx asset, add an operator via VfxNodeOps,
        // save, then assert Verify() returns zero errors AND zero warnings.
        // Uses the Assets/VfxKernelTestFixtures/ pattern from VfxCompileGateTests.

        private const string GuidResolverFixtureDir  = "Assets/VfxKernelTestFixtures";
        private const string GuidResolverFixturePath =
            GuidResolverFixtureDir + "/VfxYamlVerifier_GuidResolver.vfx";

        [Test]
        public void Verify_OnRealVfxAsset_ResolvesGuidToFqn_NoWarningsOrErrors()
        {
            // ── set up ──────────────────────────────────────────────────────
            if (!AssetDatabase.IsValidFolder(GuidResolverFixtureDir))
                AssetDatabase.CreateFolder("Assets", "VfxKernelTestFixtures");

            // Remove stale fixture.
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GuidResolverFixturePath) != null)
                AssetDatabase.DeleteAsset(GuidResolverFixturePath);

            VisualEffectAssetEditorUtility.CreateNew<VisualEffectAsset>(GuidResolverFixturePath);
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GuidResolverFixturePath);
            Assert.IsNotNull(asset, "Could not create VfxYamlVerifier GUID-resolver fixture.");

            string addedTypeFqn = null;

            try
            {
                // Add one operator via VfxNodeOps (the soft-fork bridge path).
                var graph = VfxMcpKernelHelpers.LoadGraphFromAsset(asset);
                Assert.IsNotNull(graph, "LoadGraphFromAsset returned null.");

                // Pick the first available operator FQN.
                foreach (var desc in VFXLibrary.GetOperators())
                {
                    if (desc.modelType?.FullName != null)
                    {
                        addedTypeFqn = desc.modelType.FullName;
                        var op = (VFXOperator)desc.CreateInstance();
                        graph.AddChild(op);
                        break;
                    }
                }
                Assume.That(addedTypeFqn, Is.Not.Null,
                    "VFXLibrary.GetOperators() returned no usable entries.");

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // ── verify ─────────────────────────────────────────────────
                var verifier = new VfxYamlVerifier();
                var intent = new VfxIntentSnapshot
                {
                    Ops = new List<VfxIntentOp>
                    {
                        new VfxIntentOp
                        {
                            OpIndex = 0,
                            Kind = "add",
                            ExpectedToken = "n_guid_test",
                            ExpectedTypeFqn = addedTypeFqn,
                        },
                    },
                };

                var result = verifier.Verify(GuidResolverFixturePath, intent);

                // Zero errors: GUID resolution injected the FQN so the strict match
                // found it in typeFqns. The file-based Verify() path never emits
                // intent_diverged — misses produce yaml_verify_skipped. So an error
                // here means the strict match succeeded but something is wrong upstream.
                Assert.IsEmpty(result.Errors,
                    $"Expected no errors after GUID resolution for {addedTypeFqn}. " +
                    "Check that AssetDatabase.GUIDToAssetPath / MonoScript.GetClass() " +
                    "resolved the m_Script GUID correctly.");

                // Zero yaml_verify_skipped warnings: the FQN was present in the YAML
                // (GUID resolved successfully, asset saved before Verify). If this
                // fires, the save/refresh didn't flush the operator block to disk.
                bool hasSkipWarning = result.Warnings.Exists(
                    w => w.Code == "yaml_verify_skipped");
                Assert.IsFalse(hasSkipWarning,
                    $"yaml_verify_skipped must not appear when the resolved FQN '{addedTypeFqn}' " +
                    "is present in the YAML. Check that AssetDatabase.SaveAssets()+Refresh() " +
                    "flushed the operator block before Verify() was called.");
            }
            finally
            {
                // ── tear down ──────────────────────────────────────────────
                if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GuidResolverFixturePath) != null)
                    AssetDatabase.DeleteAsset(GuidResolverFixturePath);

                if (AssetDatabase.IsValidFolder(GuidResolverFixtureDir))
                {
                    var remaining = AssetDatabase.FindAssets(
                        string.Empty, new[] { GuidResolverFixtureDir });
                    if (remaining == null || remaining.Length == 0)
                        AssetDatabase.DeleteAsset(GuidResolverFixtureDir);
                }
            }
        }

        [Test]
        public void ScanMonoBehaviours_CapturesScriptGuid_FromMScriptLine()
        {
            // Unit test: ScanMonoBehaviours must populate YamlBlock.ScriptGuid
            // from the m_Script line, even when m_TypeFqn is absent (production YAML).
            string yaml = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: deadbeef1234567890abcdef12345678, type: 3}
  m_Name: SomeOperator
";
            var blocks = VfxYamlVerifier.ScanMonoBehaviours(yaml);

            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual("deadbeef1234567890abcdef12345678", blocks[0].ScriptGuid,
                "ScanMonoBehaviours must capture the GUID from the m_Script line.");
            Assert.IsNull(blocks[0].TypeFqn,
                "TypeFqn must be null when m_TypeFqn is absent (production YAML).");
        }
    }
}
