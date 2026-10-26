using System.Collections;

namespace WindowsUtilityPack.Services;

public interface IEnvironmentVariableStore
{
    IDictionary Read(EnvironmentVariableTarget target);
    string? Get(string name, EnvironmentVariableTarget target);
    void Set(string name, string? value, EnvironmentVariableTarget target);
}

public sealed class EnvironmentVariableStore : IEnvironmentVariableStore
{
    public IDictionary Read(EnvironmentVariableTarget target) => Environment.GetEnvironmentVariables(target);
    public string? Get(string name, EnvironmentVariableTarget target) => Environment.GetEnvironmentVariable(name, target);
    public void Set(string name, string? value, EnvironmentVariableTarget target) => Environment.SetEnvironmentVariable(name, value, target);
}

public static class EnvironmentVariableEditor
{
    public static void Save(IEnvironmentVariableStore store, EnvironmentVariableTarget target,
        string? originalName, string? originalValue, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length >= 255 || name.IndexOfAny(['=', '\0']) >= 0)
            throw new ArgumentException("Variable names must contain 1–254 characters and no '=' or null characters.");
        if (value.Length >= 32767 || value.Contains('\0')) throw new ArgumentException("The value is too long or contains a null character.");
        if (originalName is not null && store.Get(originalName, target) != originalValue)
            throw new InvalidOperationException("This variable changed outside the editor. Reload before saving.");
        var rename = originalName is not null && !string.Equals(originalName, name, StringComparison.OrdinalIgnoreCase);
        if ((originalName is null || rename) && store.Get(name, target) is not null)
            throw new InvalidOperationException("A variable with that name already exists in this scope.");
        store.Set(name, value, target);
        if (!rename) return;
        try { store.Set(originalName!, null, target); }
        catch (Exception original)
        {
            try { store.Set(name, null, target); }
            catch (Exception rollback) { throw new AggregateException("Rename failed and the new variable could not be removed. Both names may remain.", original, rollback); }
            throw;
        }
    }
}
