using System.Collections;
using Microsoft.Win32;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class WindowsEditorSafetyTests
{
    [Fact]
    public void EnvironmentRename_RemovesOldName_AndRejectsCollision()
    {
        var store = new Variables(); store.Values["OLD"] = "original"; store.Values["TAKEN"] = "keep";
        Assert.Throws<InvalidOperationException>(() => EnvironmentVariableEditor.Save(store, EnvironmentVariableTarget.User, "OLD", "original", "TAKEN", "new"));
        Assert.Equal("keep", store.Values["TAKEN"]);
        EnvironmentVariableEditor.Save(store, EnvironmentVariableTarget.User, "OLD", "original", "NEW", "new");
        Assert.False(store.Values.ContainsKey("OLD")); Assert.Equal("new", store.Values["NEW"]);
    }

    [Fact]
    public void EnvironmentRename_FailedSourceRemovalRollsBackDestination()
    {
        var store = new Variables { RejectRemoval = "OLD" }; store.Values["OLD"] = "original";
        Assert.Throws<UnauthorizedAccessException>(() => EnvironmentVariableEditor.Save(store, EnvironmentVariableTarget.User, "OLD", "original", "NEW", "new"));
        Assert.Single(store.Values); Assert.Equal("original", store.Values["OLD"]);
    }

    [Fact]
    public void StartupMove_PreservesRawValueKind_AndDoesNotOverwriteCollision()
    {
        var path = @"Software\WindowsUtilityPackTests\" + Guid.NewGuid().ToString("N");
        try
        {
            using var source = Registry.CurrentUser.CreateSubKey(path + @"\source");
            using var destination = Registry.CurrentUser.CreateSubKey(path + @"\destination");
            source.SetValue("fixture", "%TEMP%\\fixture.exe", RegistryValueKind.ExpandString);
            destination.SetValue("fixture", "keep");
            Assert.Throws<InvalidOperationException>(() => RegistryValueMover.Move(Registry.CurrentUser, path + @"\source", path + @"\destination", "fixture", "%TEMP%\\fixture.exe"));
            Assert.Equal("keep", destination.GetValue("fixture"));
            destination.DeleteValue("fixture");
            RegistryValueMover.Move(Registry.CurrentUser, path + @"\source", path + @"\destination", "fixture", "%TEMP%\\fixture.exe");
            Assert.Null(source.GetValue("fixture"));
            Assert.Equal(RegistryValueKind.ExpandString, destination.GetValueKind("fixture"));
            Assert.Equal("%TEMP%\\fixture.exe", destination.GetValue("fixture", null, RegistryValueOptions.DoNotExpandEnvironmentNames));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }

    private sealed class Variables : IEnvironmentVariableStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? RejectRemoval { get; init; }
        public IDictionary Read(EnvironmentVariableTarget target) => new Hashtable(Values);
        public string? Get(string name, EnvironmentVariableTarget target) => Values.GetValueOrDefault(name);
        public void Set(string name, string? value, EnvironmentVariableTarget target)
        {
            if (value is null && name == RejectRemoval) throw new UnauthorizedAccessException("fixture removal failure");
            if (value is null) Values.Remove(name); else Values[name] = value;
        }
    }
}
