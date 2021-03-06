using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NuGet.Configuration;
using Snap.Core;
using Snap.Logging;

namespace Snap.NuGet
{
    internal class NuGetMachineWideSettings : IMachineWideSettings
    {
        readonly Lazy<ISettings> _settings;

        public ISettings Settings => _settings.Value;

        public NuGetMachineWideSettings([NotNull] ISnapFilesystem filesystem, [NotNull] string workingDirectory, ILog logger = null)
        {
            if (filesystem == null) throw new ArgumentNullException(nameof(filesystem));
            if (workingDirectory == null) throw new ArgumentNullException(nameof(workingDirectory));

            logger ??= LogProvider.For<NuGetMachineWideSettings>();

            // https://github.com/NuGet/NuGet.Client/blob/8cb7886a7e9052308cfa51308f6f901c7caf5004/src/NuGet.Core/NuGet.Commands/SourcesCommands/SourceRunners.cs#L102

            _settings = new Lazy<ISettings>(() =>
            {
                ISettings settings;
                try
                {
                    settings = global::NuGet.Configuration.Settings.LoadDefaultSettings(workingDirectory,
                        configFileName: null,
                        machineWideSettings: new XPlatMachineWideSetting());
                }
                catch (NuGetConfigurationException ex) when (ex.InnerException is UnauthorizedAccessException)
                {
                    logger.ErrorException("Error loading machine wide settings", ex.InnerException ?? ex);
                    return new NullSettings();
                }

                return settings;
            });
        }
    }

    internal sealed class NugetOrgOfficialV2PackageSources : NuGetPackageSources
    {
        static readonly PackageSource PackageSourceV2 =
            new PackageSource(NuGetConstants.V2FeedUrl, "nuget.org", true, true, false)
            {
                ProtocolVersion = (int) NuGetProtocolVersion.V2,
                IsMachineWide = true
            };

        public NugetOrgOfficialV2PackageSources() : base(new NullSettings(), new List<PackageSource> {PackageSourceV2})
        {
        }
    }

    internal sealed class NugetOrgOfficialV3PackageSources : NuGetPackageSources
    {
        static readonly PackageSource PackageSourceV3 =
            new PackageSource(NuGetConstants.V3FeedUrl, "nuget.org", true, true, false)
            {
                ProtocolVersion = (int) NuGetProtocolVersion.V3,
                IsMachineWide = true
            };

        public NugetOrgOfficialV3PackageSources() : base(new NullSettings(), new List<PackageSource> {PackageSourceV3})
        {
        }
    }

    internal class NuGetMachineWidePackageSources : NuGetPackageSources
    {
        public NuGetMachineWidePackageSources([NotNull] ISnapFilesystem filesystem, [NotNull] string workingDirectory)
        {
            if (filesystem == null) throw new ArgumentNullException(nameof(filesystem));
            if (workingDirectory == null) throw new ArgumentNullException(nameof(workingDirectory));

            var nugetMachineWideSettings = new NuGetMachineWideSettings(filesystem, workingDirectory);
            
            var nugetConfigAbsolutePath = filesystem.DirectoryGetAllFiles(workingDirectory)
                .FirstOrDefault(x => x.EndsWith("nuget.config", StringComparison.OrdinalIgnoreCase));

            var packageSources = new List<PackageSource>();

            if (nugetConfigAbsolutePath != null)
            {
                var nugetConfigReader = new NuGetConfigFileReader();
                foreach (var packageSource in nugetConfigReader.ReadNugetSources(workingDirectory).Where(x => x.IsEnabled))
                {
                    if (!packageSources.Contains(packageSource))
                    {
                        packageSources.Add(packageSource);
                    }
                }
            }
            else
            {
                var packageSourceProvider = new PackageSourceProvider(nugetMachineWideSettings.Settings);
                packageSources = packageSourceProvider.LoadPackageSources().Where(x => x.IsEnabled).ToList();
            }

            Items = packageSources;
            Settings = nugetMachineWideSettings.Settings;
        }
    }

    internal class NuGetInMemoryPackageSources : NuGetPackageSources
    {
        public NuGetInMemoryPackageSources(string tempDirectory, IEnumerable<PackageSource> packageSources) : base(new NugetInMemorySettings(tempDirectory),
            packageSources)
        {
        }
    }

    internal interface INuGetPackageSources : IEnumerable<PackageSource>
    {
        ISettings Settings { get; }
        IReadOnlyCollection<PackageSource> Items { get; }
    }

    internal class NuGetPackageSources : INuGetPackageSources
    {
        public ISettings Settings { get; protected set; }
        public IReadOnlyCollection<PackageSource> Items { get; protected set; }

        public static NuGetPackageSources Empty => new NuGetPackageSources();

        protected NuGetPackageSources()
        {
            Items = new List<PackageSource>();
            Settings = new NullSettings();
        }

        [UsedImplicitly]
        public NuGetPackageSources([NotNull] ISettings settings) : this(settings,
            settings.GetConfigFilePaths().Select(x => new PackageSource(x)))
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
        }

        public NuGetPackageSources([NotNull] ISettings settings, [NotNull] IEnumerable<PackageSource> sources)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            var items = sources.ToList();

            if (!items.Any())
            {
                throw new ArgumentException(nameof(items));
            }

            Items = items;
            Settings = settings;
        }

        public IEnumerator<PackageSource> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join(",", Items.Select(s => s.SourceUri.ToString()));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
