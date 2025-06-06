using Il2CppMonomiPark.SlimeRancher.UI;

namespace NewSR2MP.Networking.Patches;

[HarmonyPatch(typeof(MarketUI), nameof(MarketUI.Awake))]
public class MarketUIAwake
{
    static void Prefix(MarketUI __instance)
    {
        marketUI = __instance;
    }
}