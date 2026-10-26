using System.IO;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace RPGFramework.GameKit.Editor
{
    /// <summary>
    /// Where the framework package and the project live on disk (the package may be embedded, local or in the
    /// package cache).
    /// </summary>
    public static class PackagePaths
    {
        public const string PackageName = "com.aizekhan.rpg-framework";

        public static string Root
        {
            get
            {
                var info = PackageInfo.FindForAssembly(typeof(PackagePaths).Assembly);
                return info != null ? info.resolvedPath : Path.GetFullPath(Path.Combine("Packages", PackageName));
            }
        }

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    }
}
