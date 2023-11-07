using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Fallout4;
using YamlDotNet.Serialization;

namespace ImmersivePickupSounds {
	public class Program {
		public static async Task<int> Main(string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetTypicalOpen(GameRelease.Fallout4, "YourPatcher.esp")
				.Run(args);
		}

		public static void RunPatch(IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			Type type = typeof(Program);
			string? namespaceString = type.Namespace;

			string configText = File.ReadAllText(state.RetrieveConfigFile($"{namespaceString}.yml"));

			var yamlDeserializer = new DeserializerBuilder().Build();
			Dictionary<string, IPSJsonItem>? items = yamlDeserializer.Deserialize<Dictionary<string, IPSJsonItem>?>(configText);

			if (items == null) {
				throw new Exception($"ERROR: Cannot read {namespaceString}.yml");
			}

			foreach (var item in items) {
				var key = item.Key;
				var value = item.Value;
				IMiscItemGetter? miscItem = null;
				IIngestibleGetter? alchItem = null;
				ISoundDescriptorGetter? PickUpSound = null;
				ISoundDescriptorGetter? PutDownSound = null;
				ISoundDescriptorGetter? ConsumeSound = null;

				if (value.PickUpSound == null && value.PutDownSound == null) continue;

				if (value.PickUpSound != null) try {
					PickUpSound = state.LinkCache.Resolve<ISoundDescriptorGetter>(value.PickUpSound);
				} catch (Exception) {
					Console.WriteLine($"Sound descriptor not found: {value.PickUpSound}");
				}

				if (value.PutDownSound != null) try {
					PutDownSound = state.LinkCache.Resolve<ISoundDescriptorGetter>(value.PutDownSound);
				} catch (Exception) {
					Console.WriteLine($"Sound descriptor not found: {value.PutDownSound}");
				}

				if (value.ConsumeSound != null) try {
					ConsumeSound = state.LinkCache.Resolve<ISoundDescriptorGetter>(value.ConsumeSound);
				} catch (Exception) {
					Console.WriteLine($"Sound descriptor not found: {value.ConsumeSound}");
				}

				if (PickUpSound == null && PutDownSound == null && ConsumeSound == null) continue;

				try {
					miscItem = state.LinkCache.Resolve<IMiscItemGetter>(key);
				} catch (Exception) {
					try {
						alchItem = state.LinkCache.Resolve<IIngestibleGetter>(key);
					} catch (Exception) {}
				}

				if (miscItem != null) {
					var fixedMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					if (PickUpSound != null) fixedMisc.PickUpSound.SetTo(PickUpSound);
					if (PutDownSound != null) fixedMisc.PutDownSound.SetTo(PutDownSound);
				} else if (alchItem != null) {
					var fixedAlch = state.PatchMod.Ingestibles.GetOrAddAsOverride(alchItem);
					if (PickUpSound != null) fixedAlch.PickUpSound.SetTo(PickUpSound);
					if (PutDownSound != null) fixedAlch.PutDownSound.SetTo(PutDownSound);
					if (ConsumeSound != null) fixedAlch.ConsumeSound.SetTo(ConsumeSound);
				}
			}

			var miscItems = state.LoadOrder.PriorityOrder.MiscItem().WinningOverrides();

			foreach (var miscItem in miscItems) {
				var PickUpSound = miscItem.PickUpSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);
				var PutDownSound = miscItem.PutDownSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);

				if (PickUpSound != null && PutDownSound == null) {
					var overrideMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					overrideMisc.PutDownSound.SetTo(PickUpSound);
				} else if (PutDownSound != null && PickUpSound == null) {
					var overrideMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					overrideMisc.PickUpSound.SetTo(PutDownSound);
				}
			}

			var alchItems = state.LoadOrder.PriorityOrder.Ingestible().WinningOverrides();

			foreach (var alchItem in alchItems) {
				var PickUpSound = alchItem.PickUpSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);
				var PutDownSound = alchItem.PutDownSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);

				if (PickUpSound != null && PutDownSound == null) {
					var overrideAlch = state.PatchMod.Ingestibles.GetOrAddAsOverride(alchItem);
					overrideAlch.PutDownSound.SetTo(PickUpSound);
				} else if (PutDownSound != null && PickUpSound == null) {
					var overrideAlch = state.PatchMod.Ingestibles.GetOrAddAsOverride(alchItem);
					overrideAlch.PickUpSound.SetTo(PutDownSound);
				}
			}
		}
	}

	class IPSJsonItem {
		public string? PickUpSound { get; set; }
		public string? PutDownSound { get; set; }
		public string? ConsumeSound { get; set; }
	}
}
