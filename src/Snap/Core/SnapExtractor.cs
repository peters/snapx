﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;

namespace Snap.Core
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal interface ISnapExtractor
    {
        PackageArchiveReader ReadPackage(string nupkg);
        Task ExtractAsync(string nupkg, string destination, CancellationToken cancellationToken = default, ILogger logger = null);
        Task<bool> ExtractAsync(PackageArchiveReader packageArchiveReader, string destination, CancellationToken cancellationToken = default, ILogger logger = null);
    }

    internal sealed class SnapExtractor : ISnapExtractor
    {
        readonly ISnapFilesystem _snapFilesystem;

        public SnapExtractor(ISnapFilesystem snapFilesystem)
        {
            _snapFilesystem = snapFilesystem ?? throw new ArgumentNullException(nameof(snapFilesystem));
        }

        public PackageArchiveReader ReadPackage(string nupkg)
        {
            if (string.IsNullOrEmpty(nupkg)) throw new ArgumentException("Value cannot be null or empty.", nameof(nupkg));

            var stream = File.OpenRead(nupkg);
            var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            return new PackageArchiveReader(zipArchive);
        }

        public Task ExtractAsync(string nupkg, string destination, CancellationToken cancellationToken = default, ILogger logger = null)
        {
            if (nupkg == null) throw new ArgumentNullException(nameof(nupkg));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            using (var packageArchiveReader = ReadPackage(nupkg))
            {
                return ExtractAsync(packageArchiveReader, destination, cancellationToken, logger);
            }
        }

        public async Task<bool> ExtractAsync(PackageArchiveReader packageArchiveReader, string destination, CancellationToken cancellationToken = default, ILogger logger = null)
        {
            if (packageArchiveReader == null) throw new ArgumentNullException(nameof(packageArchiveReader));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            const string netTargetFrameworkMoniker = "net45";

            string ExtractFile(string sourcePath, string targetPath, Stream sourceStream)
            {
                var directorySeparator = _snapFilesystem.DirectorySeparator;

                var dstFilename = targetPath.Replace($"{directorySeparator}lib{directorySeparator}{netTargetFrameworkMoniker}", string.Empty);
                var dstDirectory = Path.GetDirectoryName(dstFilename);

                _snapFilesystem.CreateDirectoryIfNotExists(dstDirectory);

                using (var targetStream = new FileStream(dstFilename, FileMode.CreateNew, FileAccess.Write))
                {
                    sourceStream.CopyTo(targetStream);
                }

                return dstFilename;
            }

            var files = packageArchiveReader.GetFiles().Where(x => x.StartsWith($"lib/{netTargetFrameworkMoniker}")).ToList();
            if (!files.Any())
            {
                return false;
            }

            _snapFilesystem.CreateDirectoryIfNotExists(destination);

            await packageArchiveReader.CopyFilesAsync(destination, files, ExtractFile, logger ?? NullLogger.Instance, cancellationToken);

            return true;
        }
    }
}
