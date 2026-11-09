namespace PolyMod;

/// <summary>
/// A mod that PolyMod found in the mods directory, or the base game entry with the id <c>polytopia</c>.
/// </summary>
public class Mod
{
    /// <summary>
    /// A dependency of a mod on a different mod, as the manifest gives it.
    /// </summary>
    /// <param name="id">The id of the mod that this mod needs.</param>
    /// <param name="min">The lowest version of the dependency that is permitted, or null for no lower limit.</param>
    /// <param name="max">The highest version of the dependency that is permitted, or null for no upper limit.</param>
    /// <param name="required">
    /// If true, the mod gets the status <see cref="Status.DependenciesUnsatisfied"/> when the dependency is missing or has a bad version.
    /// If false, PolyMod only writes a warning to the log.
    /// </param>
    public record Dependency(string id, Version? min, Version? max, bool required = true);

    /// <summary>
    /// The data from the <c>manifest.json</c> file of a mod.
    /// </summary>
    /// <param name="id">The id of the mod. It must contain only lowercase letters and underscores, and it must not be <c>polytopia</c>.</param>
    /// <param name="name">The name of the mod to show, or null to use the id.</param>
    /// <param name="description">The description of the mod, or null.</param>
    /// <param name="version">The version of the mod.</param>
    /// <param name="authors">The names of the authors of the mod. The array must not be empty.</param>
    /// <param name="dependencies">The dependencies of the mod, or null for no dependencies.</param>
    /// <param name="client">If true, the mod is client-side only and PolyMod does not include it in the mod checksum.</param>
    public record Manifest(string id, string? name, string? description, Version version, string[] authors, Dependency[]? dependencies, bool client = false);

    /// <summary>
    /// A file in a mod, other than <c>manifest.json</c>.
    /// </summary>
    /// <param name="name">The path of the file, relative to the root of the mod directory or archive.</param>
    /// <param name="bytes">The contents of the file.</param>
    public record File(string name, byte[] bytes);

    /// <summary>
    /// The load status of a mod.
    /// </summary>
    public enum Status
    {
        /// <summary>
        /// The mod has no errors. PolyMod loads its files.
        /// </summary>
        Success,
        /// <summary>
        /// An error occurred when PolyMod loaded the mod.
        /// </summary>
        Error,
        /// <summary>
        /// A required dependency of the mod is missing or has a version that is not permitted.
        /// </summary>
        DependenciesUnsatisfied,
    }

    /// <summary>
    /// The id of the mod.
    /// </summary>
    public string id;

    /// <summary>
    /// The name of the mod to show. If the manifest has no name, this is the id.
    /// </summary>
    public string? name;

    /// <summary>
    /// The description of the mod, or null.
    /// </summary>
    public string? description;

    /// <summary>
    /// The version of the mod.
    /// </summary>
    public Version version;

    /// <summary>
    /// The names of the authors of the mod.
    /// </summary>
    public string[] authors;

    /// <summary>
    /// The dependencies of the mod, or null.
    /// </summary>
    public Dependency[]? dependencies;

    /// <summary>
    /// If true, the mod is client-side only and PolyMod does not include it in the mod checksum.
    /// </summary>
    public bool client;

    /// <summary>
    /// The load status of the mod.
    /// </summary>
    public Status status;

    /// <summary>
    /// The files of the mod. This list does not contain <c>manifest.json</c>.
    /// </summary>
    public List<File> files;

    /// <summary>
    /// The SHA-256 checksum of the mod, as a hexadecimal string.
    /// PolyMod makes it from the id, the version, and the name and contents of each file.
    /// It stays empty for client-side mods, for the base game entry, and for mods that do not have the status <see cref="Status.Success"/>.
    /// </summary>
    public string checksum = string.Empty;

    /// <summary>
    /// Makes a mod from a manifest.
    /// </summary>
    /// <param name="manifest">The manifest that gives the id, name, description, version, authors, dependencies and client value.</param>
    /// <param name="status">The initial load status of the mod.</param>
    /// <param name="files">The files of the mod.</param>
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
}
