using System.Security.AccessControl;
using System.Security.Principal;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWriteMetadataTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingOwnerGroupDaclProtectionCreationTimeAndAttributesArePreserved(bool protect)
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var security = new FileSecurity();
        security.SetAccessRuleProtection(protect, !protect);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
            FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(fixture.Path).SetAccessControl(security);
        var sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
        var expected = new FileInfo(fixture.Path).GetAccessControl(sections);
        var created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetCreationTimeUtc(fixture.Path, created);
        File.SetAttributes(fixture.Path, FileAttributes.Hidden | FileAttributes.NotContentIndexed);

        await fixture.WriteAsync(await fixture.SnapshotAsync());

        var actual = new FileInfo(fixture.Path).GetAccessControl(sections);
        Assert.Equal(expected.GetOwner(typeof(SecurityIdentifier)), actual.GetOwner(typeof(SecurityIdentifier)));
        Assert.Equal(expected.GetGroup(typeof(SecurityIdentifier)), actual.GetGroup(typeof(SecurityIdentifier)));
        Assert.Equal(expected.AreAccessRulesProtected, actual.AreAccessRulesProtected);
        Assert.Equal(DaclBytes(expected), DaclBytes(actual));
        Assert.Equal(created, File.GetCreationTimeUtc(fixture.Path));
        Assert.True(File.GetAttributes(fixture.Path).HasFlag(FileAttributes.Hidden | FileAttributes.NotContentIndexed));
        Assert.Equal("updated", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task NamedStreamIsPreservedByRefusingReplacement()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        await File.WriteAllTextAsync(fixture.Path + ":metadata", "retained stream", fixture.Token);
        var original = await fixture.SnapshotAsync();
        var staged = false;
        await Assert.ThrowsAsync<FileWriteSessionException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, fixture.Context, (_, _) =>
            {
                staged = true;
                return Task.CompletedTask;
            }, fixture.Token));
        Assert.False(staged);
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        Assert.Equal("retained stream", await File.ReadAllTextAsync(fixture.Path + ":metadata", fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task ReadOnlyFileIsNotReplacedOrMadeWritable()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var original = await fixture.SnapshotAsync();
        File.SetAttributes(fixture.Path, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<FileWriteSessionException>(() => fixture.WriteAsync(original));
            Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
            Assert.True(File.GetAttributes(fixture.Path).HasFlag(FileAttributes.ReadOnly));
            fixture.AssertNoTemporaryFiles();
        }
        finally { File.SetAttributes(fixture.Path, FileAttributes.Normal); }
    }

    private static byte[] DaclBytes(FileSecurity security)
    {
        var dacl = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0).DiscretionaryAcl!;
        var bytes = new byte[dacl.BinaryLength];
        dacl.GetBinaryForm(bytes, 0);
        return bytes;
    }

    [Fact]
    public async Task ExtendedAttributesAreNotSilentlyDiscarded()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        AtomicWriteExtendedAttributeFixture.Add(fixture.Path);
        var size = AtomicWriteExtendedAttributeFixture.Size(fixture.Path);
        Assert.True(size > 0);
        var original = await fixture.SnapshotAsync();
        await Assert.ThrowsAsync<FileWriteSessionException>(() => fixture.WriteAsync(original));
        Assert.Equal(size, AtomicWriteExtendedAttributeFixture.Size(fixture.Path));
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task NamedStreamAddedAfterAcquisitionIsStillRefusedAtCommit()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var original = await fixture.SnapshotAsync();
        using (var session = fixture.Sessions.Create(fixture.Path, [fixture.Root], fixture.Token))
        {
            await session.Stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("updated"), fixture.Token);
            await File.WriteAllTextAsync(fixture.Path + ":late", "retained stream", fixture.Token);
            await Assert.ThrowsAsync<FileWriteSessionException>(() => session.TryCommitAsync(original.Sha256, fixture.Token));
        }
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        Assert.Equal("retained stream", await File.ReadAllTextAsync(fixture.Path + ":late", fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
