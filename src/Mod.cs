namespace PolyMod;

/// <summary>
/// Represents a mod, containing its manifest information, status, and files.
/// </summary>
public class Mod
{
    /// <summary>
    /// Represents a dependency for a mod.
    /// </summary>
    /// <param name="id">The unique identifier of the dependency.</param>
    /// <param name="min">The minimum compatible version of the dependency.</param>
    /// <param name="max">The maximum compatible version of the dependency.</param>
    /// <param name="required">Whether the dependency is required for the mod to function.</param>
    public record Dependency(string id, Version min, Version max, bool required = true);

    /// <summary>
    /// Represents the manifest of a mod, containing metadata.
    /// </summary>
    /// <param name="id">The unique identifier of the mod.</param>
    /// <param name="name">The display name of the mod.</param>
    /// <param name="description">A description of the mod.</param>
    /// <param name="version">The version of the mod.</param>
    /// <param name="authors">The authors of the mod.</param>
    /// <param name="dependencies">The dependencies of the mod.</param>
    /// <param name="client">Whether the mod is client-side only.</param>
    public record Manifest(string id, string? name, string? description, Version version, string[] authors, Dependency[]? dependencies, bool client = false);

    /// <summary>
    /// Represents a file included in a mod.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="bytes">The raw byte content of the file.</param>
    public record File(string name, byte[] bytes);

    /// <summary>
    /// Represents the loading status of a mod.
    /// </summary>
    public enum Status
    {
        /// <summary>The mod was loaded successfully.</summary>
        Success,
        /// <summary>An error occurred while loading the mod.</summary>
        Error,
        /// <summary>The mod's dependencies were not satisfied.</summary>
        DependenciesUnsatisfied,
    }

    /// <summary>The unique identifier of the mod.</summary>
    public string id;
    /// <summary>The display name of the mod.</summary>
    public string? name;
    /// <summary>A description of the mod.</summary>
    public string? description;
    /// <summary>The version of the mod.</summary>
    public Version version;
    /// <summary>The authors of the mod.</summary>
    public string[] authors;
    /// <summary>The dependencies of the mod.</summary>
    public Dependency[]? dependencies;
    /// <summary>Whether the mod is client-side only.</summary>
    public bool client;
    /// <summary>The loading status of the mod.</summary>
    public Status status;
    /// <summary>The files included in the mod.</summary>
    public List<File> files;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mod"/> class.
    /// </summary>
    /// <param name="manifest">The mod's manifest data.</param>
    /// <param name="status">The initial loading status of the mod.</param>
    /// <param name="files">The list of files included in the mod.</param>
    public Mod(Manifest manifest, Status status, List<File> files)
    {
        id = manifest.id;
        name = manifest.name ?? manifest.id;
        description = manifest.description;
        version = manifest.version;
        authors = manifest.authors;
        dependencies = manifest.dependencies;
        client = manifest.client;
        this.status = status;
        this.files = files;
    }

    /// <summary>
    /// Returns a string that represents the current mod.
    /// </summary>
    /// <returns>A string representation of the mod.</returns>
    public override string ToString()
    {
        return $"mod: id={id} dependencies={dependencies} status={status}";
    }
}
