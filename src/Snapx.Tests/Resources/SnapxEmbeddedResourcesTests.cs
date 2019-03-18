﻿using System.Diagnostics.CodeAnalysis;
using snapx.Core;
using Snap.Shared.Tests;
using Xunit;

namespace Snapx.Tests.Resources
{
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public class SnapxEmbeddedResourcesTests : IClassFixture<BaseFixture>
    {
        readonly BaseFixture _baseFixture;
        readonly ISnapxEmbeddedResources _snapxEmbeddedResources;

        public SnapxEmbeddedResourcesTests(BaseFixture baseFixture)
        {
            _baseFixture = baseFixture;
            _snapxEmbeddedResources = new SnapxEmbeddedResources();
        }

        [Fact]
        public void TestContainsResourcesForAllSupportedPlatforms()
        {
            Assert.NotNull(_snapxEmbeddedResources.SetupWindows);
            Assert.NotNull(_snapxEmbeddedResources.SetupLinux);
            Assert.NotNull(_snapxEmbeddedResources.WarpPackerWindows);
            Assert.NotNull(_snapxEmbeddedResources.WarpPackerLinux);
        }
    }
}
