namespace PhotoArchive.PersistenceSpike;

public static class ArchivePaths
{
    public const string MetadataDirectoryName = ".archive";
    public const string CatalogFileName = "catalog.sqlite";

    public static string MetadataDirectory(string archiveRoot) =>
        Path.Combine(Path.GetFullPath(archiveRoot), MetadataDirectoryName);

    public static string CatalogPath(string archiveRoot) =>
        Path.Combine(MetadataDirectory(archiveRoot), CatalogFileName);

    public static string ManifestsDirectory(string archiveRoot) =>
        Path.Combine(MetadataDirectory(archiveRoot), "manifests");

    public static string BackupsDirectory(string archiveRoot) =>
        Path.Combine(MetadataDirectory(archiveRoot), "backups");

    public static string ToArchiveRelative(string archiveRoot, string fullPath)
    {
        var root = Path.GetFullPath(archiveRoot);
        var candidate = Path.GetFullPath(fullPath);
        var relative = Path.GetRelativePath(root, candidate);
        EnsureContainedRelativePath(relative);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static string ResolveArchiveRelative(string archiveRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Archive path must be a non-empty relative path.", nameof(relativePath));
        }

        var root = Path.GetFullPath(archiveRoot);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(root, normalized));
        var relative = Path.GetRelativePath(root, resolved);
        EnsureContainedRelativePath(relative);
        return resolved;
    }

    private static void EnsureContainedRelativePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Path escapes the archive root.");
        }
    }
}
