using System;
using System.IO;

namespace GoonNet;

public static class ProjectPaths
{
    private const string ProjectFileName = "GoonNet.csproj";
    private static string? _projectRoot;

    public static string ProjectRoot => _projectRoot ??= FindProjectRoot();
    public static string MusicFolder => Path.Combine(ProjectRoot, "music");

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var projectFile = Path.Combine(dir.FullName, ProjectFileName);
            if (File.Exists(projectFile))
                return dir.FullName;

            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
