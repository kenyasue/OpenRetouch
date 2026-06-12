using FluentAssertions;
using OpenRetouch.Core.Editing;
using Xunit;

namespace OpenRetouch.Core.Tests.Editing;

public sealed class EditSettingsTests
{
    [Fact]
    public void IsDefault_NewInstance_IsTrue()
    {
        new EditSettings().IsDefault.Should().BeTrue();
    }

    [Fact]
    public void IsDefault_AnyAdjustmentChanged_IsFalse()
    {
        var settings = new EditSettings();
        settings.Basic.Vibrance = 1;

        settings.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Clone_IsDeepCopy()
    {
        var original = new EditSettings();
        original.Basic.Exposure = 1.0;

        var clone = original.Clone();
        clone.Basic.Exposure = 2.0;

        original.Basic.Exposure.Should().Be(1.0, "クローンの変更が元へ波及しない");
        clone.Basic.Exposure.Should().Be(2.0);
    }
}
