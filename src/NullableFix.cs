#nullable disable

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// A polyfill for the Nullable attribute, which is used by the compiler for nullable reference type analysis.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class NullableAttribute : System.Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullableAttribute"/> class.
        /// </summary>
        /// <param name="b">The nullable flags.</param>
        public NullableAttribute(byte b) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NullableAttribute"/> class.
        /// </summary>
        /// <param name="b">The nullable flags.</param>
        public NullableAttribute(byte[] b) { }
    }

    /// <summary>
    /// A polyfill for the NullableContext attribute, which is used by the compiler for nullable reference type analysis.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    public sealed class NullableContextAttribute : System.Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullableContextAttribute"/> class.
        /// </summary>
        /// <param name="b">The nullable context flags.</param>
        public NullableContextAttribute(byte b) { }
    }

    /// <summary>
    /// A polyfill for the NullablePublicOnly attribute, which is used by the compiler for nullable reference type analysis.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Module)]
    public sealed class NullablePublicOnlyAttribute : System.Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NullablePublicOnlyAttribute"/> class.
        /// </summary>
        /// <param name="b">Whether to apply nullable analysis to public members only.</param>
        public NullablePublicOnlyAttribute(bool b) { }
    }
}
