// Packages/com.spiralingstudio.mcp.vfxgraph/Editor/Kernel/VfxKernelContainer.cs
//
// Phase 4 task 4-SETUP — kernel DI container.
//
// The single chokepoint that wires kernel services to the Phase 4 tool layer.
// Every tool class reaches the kernel exclusively through VfxKernelContainer.*.
//
// ERRATUM P-H5: identity tests mint and resolve tokens against
// Library/VfxMcpIdentity.json, which is shared process-wide. Without an
// override hook, the tests would leak state across runs. The Override()
// method swaps in a test-local VfxKernelServices instance and returns an
// IDisposable that restores the previous container on Dispose — enabling
// `using (VfxKernelContainer.Override(services)) { ... }` in [SetUp]/[TearDown].

using System;

namespace SpiralingStudio.VfxMcp.Kernel
{
    /// <summary>
    /// Aggregates every kernel service the tool layer depends on. Created once
    /// at startup via <see cref="CreateDefault"/>; identity tests may construct
    /// their own instances and install them via <see cref="VfxKernelContainer.Override"/>.
    /// </summary>
    internal sealed class VfxKernelServices
    {
        public IVfxIdentity Identity;
        public IVfxYamlVerifier Verifier;
        public IVfxCompileGate CompileGate;
        public IVfxConsoleCorrelator Correlator;
        public IVfxBusyGate BusyGate;
        public IVfxNodeOps NodeOps;
        public IVfxResponseShaper Shaper;
        public IVfxTransaction Transaction;

        public static VfxKernelServices CreateDefault()
        {
            var identity = new VfxIdentity(new VfxIdentitySidecar());
            var verifier = new VfxYamlVerifier();
            var compile  = new VfxCompileGate();
            var console  = new VfxConsoleCorrelator();
            var busy     = new VfxBusyGate();
            var nodeOps  = new VfxNodeOps();
            var shaper   = new VfxResponseShaper();
            var txn      = new VfxTransaction(identity, verifier, compile, console, busy);
            return new VfxKernelServices
            {
                Identity = identity,
                Verifier = verifier,
                CompileGate = compile,
                Correlator = console,
                BusyGate = busy,
                NodeOps = nodeOps,
                Shaper = shaper,
                Transaction = txn,
            };
        }
    }

    /// <summary>
    /// Static singleton container providing kernel services to tool classes.
    /// Tests may call <see cref="Override"/> to swap the container with a
    /// test-local services instance; the returned <see cref="IDisposable"/>
    /// restores the previous services on <see cref="IDisposable.Dispose"/>.
    /// </summary>
    internal static class VfxKernelContainer
    {
        private static VfxKernelServices s_Services = VfxKernelServices.CreateDefault();

        public static IVfxIdentity Identity => s_Services.Identity;
        public static IVfxYamlVerifier Verifier => s_Services.Verifier;
        public static IVfxCompileGate CompileGate => s_Services.CompileGate;
        public static IVfxConsoleCorrelator Correlator => s_Services.Correlator;
        public static IVfxBusyGate BusyGate => s_Services.BusyGate;
        public static IVfxNodeOps NodeOps => s_Services.NodeOps;
        public static IVfxResponseShaper Shaper => s_Services.Shaper;
        public static IVfxTransaction Transaction => s_Services.Transaction;

        internal static IDisposable Override(VfxKernelServices replacement)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));
            var prev = s_Services;
            s_Services = replacement;
            return new Disposer(() => s_Services = prev);
        }

        private sealed class Disposer : IDisposable
        {
            private Action _onDispose;
            public Disposer(Action onDispose) { _onDispose = onDispose; }
            public void Dispose()
            {
                var cb = _onDispose;
                _onDispose = null;
                cb?.Invoke();
            }
        }
    }
}
