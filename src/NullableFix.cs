#nullable disable

namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    internal sealed class NullableAttribute : System.Attribute
    {
        public NullableAttribute(byte b) { }
        public NullableAttribute(byte[] b) { }
    }

    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    internal sealed class NullableContextAttribute : System.Attribute
    {
        public NullableContextAttribute(byte b) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Module)]
    internal sealed class NullablePublicOnlyAttribute : System.Attribute
    {
        public NullablePublicOnlyAttribute(bool b) { }
    }
}
