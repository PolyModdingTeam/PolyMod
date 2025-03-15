namespace PolyMod
{
    public class Mod
    {
        public record Dependency(string id, Version min, Version max, bool required = true);
        public record Manifest(string id, string? name, Version version, string[] authors, Dependency[]? dependencies, bool client = false);
        public record File(string name, byte[] bytes);
        public enum Status
        {
            Success,
            Error,
            DependenciesUnsatisfied,
        }

        public string? name;
        public Version version;
        public string[] authors;
        public Dependency[]? dependencies;
        public bool client;
        public Status status;
        public List<File> files;

        public Mod(Manifest manifest, Status status, List<File> files)
        {
            name = manifest.name ?? manifest.id;
            version = manifest.version;
            authors = manifest.authors;
            dependencies = manifest.dependencies;
            client = manifest.client;
            this.status = status;
            this.files = files;
        }
    }
}