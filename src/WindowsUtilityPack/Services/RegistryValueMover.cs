using Microsoft.Win32;

namespace WindowsUtilityPack.Services;

internal static class RegistryValueMover
{
    public static void Move(RegistryKey root, string sourcePath, string destinationPath, string name, string expectedValue)
    {
        using var source = root.OpenSubKey(sourcePath, true) ?? throw new InvalidOperationException("The startup entry no longer exists. Refresh the list.");
        ValidateCurrent(source, name, expectedValue);
        var value = source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!;
        var kind = source.GetValueKind(name);
        using var destination = root.CreateSubKey(destinationPath, true);
        if (destination.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("An entry with the same name already exists in the destination. Neither entry was changed.");
        destination.SetValue(name, value, kind);
        try { ValidateCurrent(source, name, expectedValue); source.DeleteValue(name, true); }
        catch (Exception original)
        {
            try
            {
                if (!Equals(destination.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), value))
                    throw new InvalidOperationException("The destination changed concurrently and was retained.");
                destination.DeleteValue(name, true);
            }
            catch (Exception rollback) { throw new AggregateException("The startup entry could not be moved or fully rolled back.", original, rollback); }
            throw;
        }
    }

    public static void ValidateCurrent(RegistryKey key, string name, string expectedValue)
    {
        if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string current || current != expectedValue)
            throw new InvalidOperationException("The startup entry changed or was removed. Refresh the list before editing it.");
    }
}
