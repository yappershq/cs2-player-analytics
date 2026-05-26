using System.IO;
using Microsoft.Extensions.Logging;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.CommandCenter.Shared;
using Sharp.Shared;
using Sharp.Shared.Managers;

namespace PlayerAnalytics.Core;

internal sealed class InterfaceBridge
{
    internal static InterfaceBridge Instance { get; private set; } = null!;

    // === Paths ===
    internal string SharpPath  { get; }
    internal string ConfigPath { get; }
    internal string DataPath   { get; }

    // === Managers ===
    internal IEntityManager      EntityManager      { get; }
    internal IClientManager      ClientManager      { get; }
    internal IConVarManager      ConVarManager      { get; }
    internal IEventManager       EventManager       { get; }
    internal IModSharp           ModSharp           { get; }
    internal ILoggerFactory      LoggerFactory      { get; }
    internal ISharpModuleManager SharpModuleManager { get; }

    // === Optional — populated in OnAllModulesLoaded ===
    internal IAdminManager? AdminManager  { get; private set; }
    internal ICommandCenter? CommandCenter { get; private set; }

    public InterfaceBridge(
        string        dllPath,
        string        sharpPath,
        ISharedSystem sharedSystem,
        ILoggerFactory loggerFactory)
    {
        Instance = this;

        SharpPath  = sharpPath;
        ConfigPath = Path.Combine(sharpPath, "configs");
        DataPath   = Path.Combine(sharpPath, "data");

        EntityManager      = sharedSystem.GetEntityManager();
        ClientManager      = sharedSystem.GetClientManager();
        ConVarManager      = sharedSystem.GetConVarManager();
        EventManager       = sharedSystem.GetEventManager();
        ModSharp           = sharedSystem.GetModSharp();
        LoggerFactory      = loggerFactory;
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
    }

    internal void InitOptionalModules()
    {
        AdminManager = SharpModuleManager
            .GetOptionalSharpModuleInterface<IAdminManager>(IAdminManager.Identity)?.Instance;

        CommandCenter = SharpModuleManager
            .GetOptionalSharpModuleInterface<ICommandCenter>(ICommandCenter.Identity)?.Instance;
    }
}
