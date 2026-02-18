using System;

namespace MCPForUnity.Editor.Tools.Vfx
{
    internal sealed class VfxToolValidationException : Exception
    {
        public VfxToolValidationException(string message) : base(message) { }
    }

    internal sealed class VfxToolReflectionException : Exception
    {
        public VfxToolReflectionException(string message) : base(message) { }
    }
}
