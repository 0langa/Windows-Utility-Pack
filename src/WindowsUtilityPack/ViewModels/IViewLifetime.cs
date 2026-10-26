namespace WindowsUtilityPack.ViewModels;

/// <summary>Suspends recurring work while a retained view is outside the active navigation slot.</summary>
public interface IViewLifetime
{
    void Activate();
    void Deactivate();
}
