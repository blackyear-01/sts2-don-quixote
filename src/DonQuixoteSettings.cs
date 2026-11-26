using System;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace DonQuixote;

/// <summary>
/// 模组持久化设置（全局，跨存档共享）。
/// </summary>
public sealed class DonQuixoteSettings
{
    /// <summary>
    /// 是否让「折射轨道线」作为第四层出现，默认开启。
    /// </summary>
    public bool EnableRefractionRail { get; set; } = true;
}

/// <summary>
/// 在 RitsuLib 的模组配置界面注册设置页。
/// </summary>
public static class DonQuixoteSettingsPage
{
    private const string DonQuixote_DataKey = "settings";

    private static readonly ModSettingsValueBinding<DonQuixoteSettings, bool> DonQuixote_RailBinding = new(
        DonQuixoteModEntry.DonQuixote_ModId,
        DonQuixote_DataKey,
        SaveScope.Global,
        static s => s.EnableRefractionRail,
        static (s, v) => s.EnableRefractionRail = v);

    /// <summary>
    /// 当前是否启用第四层；读取失败时按默认（开启）处理。
    /// </summary>
    public static bool DonQuixote_RailEnabled
    {
        get
        {
            try
            {
                return DonQuixote_RailBinding.Read();
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// 注册数据存储与设置页，需在模组初始化时调用一次。
    /// </summary>
    public static void DonQuixote_Register()
    {
        try
        {
            ModDataStore.For(DonQuixoteModEntry.DonQuixote_ModId).Register<DonQuixoteSettings>(
                key: DonQuixote_DataKey,
                fileName: "settings.json",
                scope: SaveScope.Global,
                defaultFactory: static () => new DonQuixoteSettings(),
                autoCreateIfMissing: true);

            RitsuLibFramework.RegisterModSettings(DonQuixoteModEntry.DonQuixote_ModId, page => page
                .WithTitle(ModSettingsText.Literal("堂吉诃德"))
                .WithModDisplayName(ModSettingsText.Literal("堂吉诃德"))
                .WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
                .AddSection("general", section => section
                    .WithTitle(ModSettingsText.Literal("关卡"))
                    .AddToggle(
                        "refraction_rail",
                        ModSettingsText.Literal("启用第四层折射轨道线"),
                        DonQuixote_RailBinding)));

            DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] settings page registered", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] settings page registration failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}
