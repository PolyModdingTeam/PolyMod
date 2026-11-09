#nullable disable
// verify whether it is needed at all bruv, i tested one time quickly and it easily compiled
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// A local copy of the attribute that the compiler writes to show the nullability of a type.
    /// The compiler uses this copy when the referenced assemblies do not supply the attribute.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
    public sealed class NullableAttribute : System.Attribute
    {
        /// <summary>
        /// Makes a new <see cref="NullableAttribute"/> with one nullability flag. The constructor does not keep the value.
        /// </summary>
        /// <param name="b">The nullability flag for the type.</param>
        public NullableAttribute(byte b) { }

        /// <summary>
        /// Makes a new <see cref="NullableAttribute"/> with a nullability flag for each part of a type. The constructor does not keep the values.
        /// </summary>
        /// <param name="b">The nullability flags for the type and its type arguments.</param>
        public NullableAttribute(byte[] b) { }
    }

    /// <summary>
    /// A local copy of the attribute that the compiler writes to give the default nullability for a type or method.
    /// The compiler uses this copy when the referenced assemblies do not supply the attribute.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false)]
    public sealed class NullableContextAttribute : System.Attribute
    {
        /// <summary>
        /// Makes a new <see cref="NullableContextAttribute"/>. The constructor does not keep the value.
        /// </summary>
        /// <param name="b">The default nullability flag.</param>
        public NullableContextAttribute(byte b) { }
    }

    /// <summary>
    /// A local copy of the attribute that the compiler writes to a module when it keeps nullability data only for public members.
    /// The compiler uses this copy when the referenced assemblies do not supply the attribute.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Module)]
    public sealed class NullablePublicOnlyAttribute : System.Attribute
    {
        /// <summary>
        /// Makes a new <see cref="NullablePublicOnlyAttribute"/>. The constructor does not keep the value.
        /// </summary>
        /// <param name="b">True if the module also has nullability data for internal members.</param>
        public NullablePublicOnlyAttribute(bool b) { }
    }
}
