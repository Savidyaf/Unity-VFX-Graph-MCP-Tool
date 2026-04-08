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
using System.Text;
using NUnit.Framework;

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
    }
}
