#nullable disable

namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class NullableAttribute : System.Attribute
    {
        public NullableAttribute(byte b) { }
        public NullableAttribute(byte[] b) { }
    }

    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    public sealed class NullableContextAttribute : System.Attribute
    {
        public NullableContextAttribute(byte b) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Module)]
    public sealed class NullablePublicOnlyAttribute : System.Attribute
    {
        public NullablePublicOnlyAttribute(bool b) { }
    }
}
