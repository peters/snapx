using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using Snap.Core.Models;
using Snap.Core.Resources;
using Snap.Extensions;

namespace Snap.Core
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal interface ISnapExtractor
    {
        Task<List<string>> ExtractAsync(string nupkgAbsolutePath, string destinationDirectoryAbsolutePath, SnapRelease snapRelease, CancellationToken cancellationToken = default);
        Task<List<string>> ExtractAsync(string destinationDirectoryAbsolutePath, SnapRelease snapRelease, IAsyncPackageCoreReader asyncPackageCoreReader, CancellationToken cancellationToken = default);
        Task<SnapAppsReleases> GetSnapAppsReleasesAsync(IAsyncPackageCoreReader asyncPackageCoreReader, [NotNull] ISnapAppReader snapAppReader, CancellationToken cancellationToken = default);
    }

    internal sealed class SnapExtractor : ISnapExtractor
    {   
        readonly ISnapFilesystem _snapFilesystem;
        readonly ISnapPack _snapPack;
        readonly ISnapEmbeddedResources _snapEmbeddedResources;

        public SnapExtractor(ISnapFilesystem snapFilesystem, [NotNull] ISnapPack snapPack, [NotNull] ISnapEmbeddedResources snapEmbeddedResources)
        {
            _snapFilesystem = snapFilesystem ?? throw new ArgumentNullException(nameof(snapFilesystem));
            _snapPack = snapPack ?? throw new ArgumentNullException(nameof(snapPack));
            _snapEmbeddedResources = snapEmbeddedResources ?? throw new ArgumentNullException(nameof(snapEmbeddedResources));
        }

        public async Task<List<string>> ExtractAsync(string nupkgAbsolutePath, string destinationDirectoryAbsolutePath, SnapRelease snapRelease, CancellationToken cancellationToken = default)
        {
            if (nupkgAbsolutePath == null) throw new ArgumentNullException(nameof(nupkgAbsolutePath));
            if (destinationDirectoryAbsolutePath == null) throw new ArgumentNullException(nameof(destinationDirectoryAbsolutePath));

            using (var packageArchiveReader = new PackageArchiveReader(nupkgAbsolutePath))
            {
                return await ExtractAsync(destinationDirectoryAbsolutePath, snapRelease, packageArchiveReader, cancellationToken);
            }
        }

        public async Task<List<string>> ExtractAsync(string destinationDirectoryAbsolutePath, [NotNull] SnapRelease snapRelease, 
            IAsyncPackageCoreReader asyncPackageCoreReader, CancellationToken cancellationToken = default)
        {
            if (destinationDirectoryAbsolutePath == null) throw new ArgumentNullException(nameof(destinationDirectoryAbsolutePath));
            if (snapRelease == null) throw new ArgumentNullException(nameof(snapRelease));
            if (asyncPackageCoreReader == null) throw new ArgumentNullException(nameof(asyncPackageCoreReader));

            var snapApp = await _snapPack.GetSnapAppAsync(asyncPackageCoreReader, cancellationToken);                        
            var coreRunExeFilename = _snapEmbeddedResources.GetCoreRunExeFilenameForSnapApp(snapApp);
            var extractedFiles = new List<string>();
            
            _snapFilesystem.DirectoryCreateIfNotExists(destinationDirectoryAbsolutePath);

            var files = !snapRelease.IsFull ? 
                snapRelease
                    .New
                    .Concat(snapRelease.Modified)
                    .OrderBy(x => x.NuspecTargetPath).ToList() : 
                    snapRelease.Files;

            foreach (var checksum in files)
            {
                var isSnapRootTargetItem = checksum.NuspecTargetPath.StartsWith(SnapConstants.NuspecAssetsTargetPath);

                string dstFilename;
                if (isSnapRootTargetItem)
                {
                    dstFilename = _snapFilesystem.PathCombine(destinationDirectoryAbsolutePath, checksum.Filename);

                    if (checksum.Filename == coreRunExeFilename)
                    {
                        dstFilename = _snapFilesystem.PathCombine(
                            _snapFilesystem.DirectoryGetParent(destinationDirectoryAbsolutePath), checksum.Filename);          
                    }
                }
                else
                {
                    var targetPath = checksum.NuspecTargetPath.Substring(SnapConstants.NuspecRootTargetPath.Length + 1);
                    dstFilename = _snapFilesystem.PathCombine(destinationDirectoryAbsolutePath,  
                        _snapFilesystem.PathEnsureThisOsDirectoryPathSeperator(targetPath));
                }

                var thisDestinationDir = _snapFilesystem.PathGetDirectoryName(dstFilename);
                _snapFilesystem.DirectoryCreateIfNotExists(thisDestinationDir);

                var srcStream = await asyncPackageCoreReader.GetStreamAsync(checksum.NuspecTargetPath, cancellationToken);

                await _snapFilesystem.FileWriteAsync(srcStream, dstFilename, cancellationToken);

                extractedFiles.Add(dstFilename);
            }

            return extractedFiles;
        }

        public async Task<SnapAppsReleases> GetSnapAppsReleasesAsync([NotNull] IAsyncPackageCoreReader asyncPackageCoreReader, ISnapAppReader snapAppReader, CancellationToken cancellationToken = default)
        {
            if (asyncPackageCoreReader == null) throw new ArgumentNullException(nameof(asyncPackageCoreReader));
            if (snapAppReader == null) throw new ArgumentNullException(nameof(snapAppReader));

            var snapReleasesFilename = _snapFilesystem.PathCombine(SnapConstants.NuspecRootTargetPath, SnapConstants.ReleasesFilename);
            using (var snapReleasesStream =
                await asyncPackageCoreReader
                    .GetStreamAsync(snapReleasesFilename, cancellationToken)
                    .ReadToEndAsync(cancellationToken))
            {                
                return snapAppReader.BuildSnapAppsReleasesFromStream(snapReleasesStream);
            }
        }
    }
}
