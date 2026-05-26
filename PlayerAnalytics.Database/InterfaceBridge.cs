using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Managers;

namespace PlayerAnalytics.Database;

internal sealed class InterfaceBridge
{
    public static InterfaceBridge Instance { get; private set; } = null!;

    public string               SharpPath          { get; }
    public string               ConfigPath         { get; }
    public string               DataPath           { get; }
    public ILoggerFactory       LoggerFactory      { get; }
    public ISharpModuleManager  SharpModuleManager { get; }
    public PlayerAnalyticsDatabasePlugin Module   { get; }

    public InterfaceBridge(
        PlayerAnalyticsDatabasePlugin module,
        ISharedSystem                 sharedSystem,
        string                        sharpPath)
    {
        Instance = this;

        Module             = module;
        SharpPath          = sharpPath;
        ConfigPath         = Path.Combine(sharpPath, "configs");
        DataPath           = Path.Combine(sharpPath, "data");
        LoggerFactory      = sharedSystem.GetLoggerFactory();
        SharpModuleManager = sharedSystem.GetSharpModuleManager();

        Directory.CreateDirectory(ConfigPath);
        Directory.CreateDirectory(DataPath);
    }
}
