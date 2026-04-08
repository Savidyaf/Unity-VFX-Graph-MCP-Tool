// Packages/com.spiralingstudio.mcp.vfxgraph/Tests/Editor/Kernel/VfxNoReflectionTests.cs
//
// Phase 5-6: No-reflection static analyzer.
//
// Asserts that no file in Editor/Kernel/ uses runtime reflection on
// UnityEditor.VFX.* or UnityEngine.VFX.* types. The soft-fork bridge
// approach (VfxMcpKernelHelpers) means zero runtime reflection is needed —
// all VFX Graph access is through compile-time InternalsVisibleTo grants.
//
// ERRATUM A-H3: no exclusions. VfxConsoleReader uses
// Application.logMessageReceived (not LogEntries reflection), so it passes.

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SpiralingStudio.VfxMcp.Kernel.Tests
{
    public class VfxNoReflectionTests
    {
        [Test]
        public void Kernel_HasNoRuntimeReflection_OnVFXTypes()
        {
            const string kernelDir = "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel";

            // Pattern matches .GetMethod/.GetField/.GetProperty etc. chained onto
            // a VFX type reference, Activator.CreateInstance of a VFX type, or
            // Type.GetType() with a VFX namespace string literal.
            // It does NOT flag typeof(VFXModel) used as a plain type argument —
            // only the follow-up reflection method call matters.
            var forbidden = new Regex(
                @"typeof\(VFX\w+\)\s*\.(GetMethod|GetField|GetProperty|InvokeMember|GetMembers|GetType|GetMethods|GetFields|GetProperties)" +
                @"|Activator\.CreateInstance\s*\(\s*typeof\(VFX\w+\)" +
                @"|Type\.GetType\s*\(\s*[""']UnityEditor\.VFX\." +
                @"|Type\.GetType\s*\(\s*[""']UnityEngine\.VFX\.",
                RegexOptions.Compiled);

            Assert.IsTrue(Directory.Exists(kernelDir),
                $"Kernel directory not found: {kernelDir}. Run tests from the project root.");

            bool anyViolation = false;
            foreach (var file in Directory.GetFiles(kernelDir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (forbidden.IsMatch(content))
                {
                    // Report all violating files before failing.
                    TestContext.WriteLine($"VIOLATION: {file} contains forbidden runtime reflection on VFX types.");
                    anyViolation = true;
                }
            }

            Assert.IsFalse(anyViolation,
                "One or more Kernel/ files contain forbidden runtime reflection on VFX types. " +
                "See TestContext output for details. Use the soft-fork bridge " +
                "(VfxMcpKernelHelpers) instead of Type.GetType / GetMethod / Activator.CreateInstance.");
        }

        [Test]
        public void Generation_HasNoRuntimeReflection_OnVFXTypes()
        {
            // Also check the generation pipeline — emitters must not use
            // runtime reflection on VFX types at generation time.
            const string genDir = "Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Generation";

            var forbidden = new Regex(
                @"typeof\(VFX\w+\)\s*\.(GetMethod|GetField|GetProperty|InvokeMember|GetMembers|GetType|GetMethods|GetFields|GetProperties)" +
                @"|Activator\.CreateInstance\s*\(\s*typeof\(VFX\w+\)" +
                @"|Type\.GetType\s*\(\s*[""']UnityEditor\.VFX\." +
                @"|Type\.GetType\s*\(\s*[""']UnityEngine\.VFX\.",
                RegexOptions.Compiled);

            if (!Directory.Exists(genDir))
            {
                Assert.Inconclusive($"Generation directory not found: {genDir}");
                return;
            }

            bool anyViolation = false;
            foreach (var file in Directory.GetFiles(genDir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (forbidden.IsMatch(content))
                {
                    TestContext.WriteLine($"VIOLATION: {file} contains forbidden runtime reflection on VFX types.");
                    anyViolation = true;
                }
            }

            Assert.IsFalse(anyViolation,
                "One or more Generation/ files contain forbidden runtime reflection on VFX types.");
        }
    }
}
