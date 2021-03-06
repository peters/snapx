using System;
using System.Runtime.InteropServices;

namespace Snap.AnyOS
{
    internal interface ISnapOsSpecialFolders
    {
        string ApplicationData { get; }
        string LocalApplicationData { get; }
        string DesktopDirectory { get; }
        string StartupDirectory { get; }
        string StartMenu { get; }
        string InstallerCacheDirectory { get; }
        string NugetCacheDirectory { get; }
    }

    internal abstract class SnapOsSpecialFolders : ISnapOsSpecialFolders
    {
        public virtual string ApplicationData => throw new PlatformNotSupportedException();
        public virtual string LocalApplicationData => throw new PlatformNotSupportedException();
        public virtual string DesktopDirectory => throw new PlatformNotSupportedException();
        public virtual string StartupDirectory => throw new PlatformNotSupportedException();
        public virtual string StartMenu => throw new PlatformNotSupportedException();
        public virtual string InstallerCacheDirectory => throw new PlatformNotSupportedException();
        public virtual string NugetCacheDirectory => throw new PlatformNotSupportedException();

        public static ISnapOsSpecialFolders AnyOs
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return new SnapOsSpecialFoldersWindows();
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return new SnapOsSpecialFoldersUnix();
                }

                throw new PlatformNotSupportedException();
            }
        }
    }
    
    internal sealed class SnapOsSpecialFoldersWindows : SnapOsSpecialFolders
    {
        public override string ApplicationData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public override string LocalApplicationData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public override string DesktopDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public override string StartupDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        public override string StartMenu { get; } = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        public override string InstallerCacheDirectory => $"{ApplicationData}\\snapx";
        public override string NugetCacheDirectory => $"{InstallerCacheDirectory}\\temp\\nuget";
    }

    internal sealed class SnapOsSpecialFoldersUnix : SnapOsSpecialFolders
    {
        public override string ApplicationData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public override string LocalApplicationData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public override string DesktopDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public override string StartupDirectory => DesktopDirectory;
        public override string StartMenu => DesktopDirectory;
        public override string InstallerCacheDirectory => $"{ApplicationData}/snapx";
        public override string NugetCacheDirectory => $"{InstallerCacheDirectory}/temp/nuget";
    }
}
