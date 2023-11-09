using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.FormKeys.Fallout4;
using Mutagen.Bethesda.WPF.Reflection.Attributes;

namespace ConfigurableShadows {
	public class Program {

		private static Lazy<ProgramSettings> lazySettings = null!;
		private static ProgramSettings localSettings => lazySettings.Value;

		private readonly static List<IFormLinkGetter<ILightGetter>> listShadowsOn = new();
		private readonly static List<IFormLinkGetter<ILightGetter>> listShadowsOff = new();

		private static IPatcherState<IFallout4Mod, IFallout4ModGetter> localState = null!;

		public static async Task<int> Main (string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetAutogeneratedSettings(
					nickname: "Settings",
					path: "ConfigurableShadows_Settings.json",
					out lazySettings
				)
				.SetTypicalOpen(GameRelease.Fallout4, "SYN_ConfigurableShadows.esp")
				.Run(args);
		}

		private static bool IsLightTypeSpot (ILightGetter light) {
			return light.Flags.HasFlag(Light.Flag.NonShadowSpotlight) || light.Flags.HasFlag(Light.Flag.ShadowSpotlight);
		}

		private static void TurnShadowOn (ILightGetter light) {
			if (IsLightTypeSpot(light)) {
				if (light.Flags.HasFlag(Light.Flag.NonShadowSpotlight)) {
					var oLight = localState.PatchMod.Lights.GetOrAddAsOverride(light);
					oLight.Flags &= ~Light.Flag.NonShadowSpotlight;
					oLight.Flags |= Light.Flag.ShadowSpotlight;
				}
			} else if (!light.Flags.HasFlag(Light.Flag.ShadowOmnidirectional)) {
				var oLight = localState.PatchMod.Lights.GetOrAddAsOverride(light);
				oLight.Flags |= Light.Flag.ShadowOmnidirectional;
			}
		}

		private static void TurnShadowOff (ILightGetter light) {
			if (IsLightTypeSpot(light)) {
				if (light.Flags.HasFlag(Light.Flag.ShadowSpotlight)) {
					var oLight = localState.PatchMod.Lights.GetOrAddAsOverride(light);
					oLight.Flags &= ~Light.Flag.ShadowSpotlight;
					oLight.Flags |= Light.Flag.NonShadowSpotlight;
				}
			} else if (light.Flags.HasFlag(Light.Flag.ShadowOmnidirectional)) {
				var oLight = localState.PatchMod.Lights.GetOrAddAsOverride(light);
				oLight.Flags &= ~Light.Flag.ShadowOmnidirectional;
			}
		}

		private static void SetShadowOn (IFormLinkGetter<ILightGetter> light) {
			if (listShadowsOff.Contains(light)) listShadowsOff.Remove(light);
			if (!listShadowsOn.Contains(light)) listShadowsOn.Add(light);
		}

		private static void SetShadowOff (IFormLinkGetter<ILightGetter> light) {
			if (listShadowsOn.Contains(light)) listShadowsOn.Remove(light);
			if (!listShadowsOff.Contains(light)) listShadowsOff.Add(light);
		}

		public static void RunPatch (IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			localState = state;

			var lights = localState.LoadOrder.PriorityOrder.Light().WinningOverrides();
			foreach (var light in lights) {
				if (IsLightTypeSpot(light)) {
					if (localSettings.allSpotShadowsOperation == ProgramSettings.LightOps.AddShadows) SetShadowOn(light.ToLink());
					else if (localSettings.allSpotShadowsOperation == ProgramSettings.LightOps.RemoveShadows) SetShadowOff(light.ToLink());
				} else if (localSettings.removeAllOmniShadows) {
					SetShadowOff(light.ToLink());
				}
			}

			// var cobjItems = localState.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();

			// foreach (var cobjItem in cobjItems) {
			// 	var light = cobjItem.CreatedObject?.TryResolve<ILightGetter>(localState.LinkCache);
			// 	if (light == null) continue;

			// 	if (IsLightTypeSpot(light)) {
			// 		if (localSettings.workshopSpotShadowsOperation == ProgramSettings.LightOps.AddShadows) SetShadowOn(light.ToLink());
			// 		else if (localSettings.workshopSpotShadowsOperation == ProgramSettings.LightOps.RemoveShadows) SetShadowOff(light.ToLink());
			// 	} else if (localSettings.removeWorkshopOmniShadows) {
			// 		SetShadowOff(light.ToLink());
			// 	}
			// }

			foreach (var lightLink in localSettings.addShadowsList) {
				SetShadowOn(lightLink);
			}

			foreach (var lightLink in localSettings.removeShadowsList) {
				SetShadowOff(lightLink);
			}

			foreach (var lightLink in listShadowsOn) {
				TurnShadowOn(lightLink.Resolve(localState.LinkCache));
			}

			foreach (var lightLink in listShadowsOff) {
				TurnShadowOff(lightLink.Resolve(localState.LinkCache));
			}
		}
	}

	public class ProgramSettings {
		[MaintainOrder]
		[SettingName("Remove All Omnidirectional Shadows")]
		public bool removeAllOmniShadows = false;

		[MaintainOrder]
		[SettingName("All Spot Shadows")]
		public LightOps allSpotShadowsOperation = LightOps.LeaveAlone;

		// [MaintainOrder]
		// [SettingName("Weapon & Explosives Muzzle Flash Shadows")]
		// public LightOps muzzleShadowsOperation = LightOps.LeaveAlone;

		// [MaintainOrder]
		// [SettingName("Remove Workshop Omnidirectional Shadows (takes precedence over all settings above)")]
		// public bool removeWorkshopOmniShadows = false;

		// [MaintainOrder]
		// [SettingName("Workshop Spot Shadows (takes precedence over all settings above)")]
		// public LightOps workshopSpotShadowsOperation = LightOps.LeaveAlone;

		[MaintainOrder]
		[SettingName("Force Add Shadows (all types) (takes precedence over all settings above)")]
		public List<IFormLinkGetter<ILightGetter>> addShadowsList = new() {
			Fallout4.Light.HeadlampLightRinged,
			Fallout4.Light.HeadlampLightWarped,
			Fallout4.Light.HeadlampLightWarpedxx,
			Fallout4.Light.PAT45HeadlampLight,
			Fallout4.Light.PAT45HeadlampLightBlue,
			Fallout4.Light.PAT45HeadlampLightBright,
			Fallout4.Light.PAT45HeadlampLightPACrosshairs,
			Fallout4.Light.PAT45HeadlampLightPARedTactical,
			Fallout4.Light.PAT45HeadlampLightPAVaultBoy,
			Fallout4.Light.PAT45HeadlampLightPurple,
			Fallout4.Light.defaultPowerArmorHeadlampLight
		};

		[MaintainOrder]
		[SettingName("Force Remove Shadows (all types) (takes precedence over all settings above)")]
		public List<IFormLinkGetter<ILightGetter>> removeShadowsList = new();

		public enum LightOps {
			LeaveAlone,
			RemoveShadows,
			AddShadows
		}
	}
}
