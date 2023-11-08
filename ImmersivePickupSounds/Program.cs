using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Fallout4;
using YamlDotNet.Serialization;

namespace ImmersivePickupSounds {
	public class Program {
		public static async Task<int> Main (string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetTypicalOpen(GameRelease.Fallout4, "SYN_ImmersivePickupSounds.esp")
				.Run(args);
		}

		public static void RunPatch (IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			Type type = typeof(Program);
			string? namespaceString = type.Namespace;

			string configText = File.ReadAllText(state.RetrieveConfigFile($"{namespaceString}.yml"));

			var yamlDeserializer = new DeserializerBuilder().Build();
			Dictionary<string, IPSJsonItem>? items = yamlDeserializer.Deserialize<Dictionary<string, IPSJsonItem>?>(configText);

			if (items == null) throw new Exception($"ERROR: Cannot read {namespaceString}.yml");

			foreach (var item in items) {
				var key = item.Key;
				var value = item.Value;
				IMiscItemGetter? miscItem = null;
				IIngestibleGetter? alchItem = null;
				ISoundDescriptorGetter? pickUpSound = null;
				ISoundDescriptorGetter? putDownSound = null;
				ISoundDescriptorGetter? consumeSound = null;

				if (value.PickUpSound == null && value.PutDownSound == null) continue;

				if (value.PickUpSound != null) try {
						pickUpSound = state.LinkCache.Resolve<ISoundDescriptorGetter>(value.PickUpSound);
					} catch (Exception) {
						Console.WriteLine($"Sound descriptor not found: {value.PickUpSound}");
					}

				if (value.PutDownSound != null) try {
						putDownSound = state.LinkCache.Resolve<ISoundDescriptorGetter>(value.PutDownSound);
					} catch (Exception) {
						Console.WriteLine($"Sound descriptor not found: {value.PutDownSound}");
					}

				if (value.ConsumeSound != null) try {
						consumeSound = state.LinkCache.Resolve<ISoundDescriptorGetter>(value.ConsumeSound);
					} catch (Exception) {
						Console.WriteLine($"Sound descriptor not found: {value.ConsumeSound}");
					}

				if (pickUpSound == null && putDownSound == null && consumeSound == null) continue;

				try {
					miscItem = state.LinkCache.Resolve<IMiscItemGetter>(key);
				} catch (Exception) {
					try {
						alchItem = state.LinkCache.Resolve<IIngestibleGetter>(key);
					} catch (Exception) { }
				}

				if (miscItem != null) {
					var fixedMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					if (pickUpSound != null) fixedMisc.PickUpSound.SetTo(pickUpSound);
					if (putDownSound != null) fixedMisc.PutDownSound.SetTo(putDownSound);
				} else if (alchItem != null) {
					var fixedAlch = state.PatchMod.Ingestibles.GetOrAddAsOverride(alchItem);
					if (pickUpSound != null) fixedAlch.PickUpSound.SetTo(pickUpSound);
					if (putDownSound != null) fixedAlch.PutDownSound.SetTo(putDownSound);
					if (consumeSound != null) fixedAlch.ConsumeSound.SetTo(consumeSound);
				}
			}

			var miscItems = state.LoadOrder.PriorityOrder.MiscItem().WinningOverrides();

			foreach (var miscItem in miscItems) {
				var pickUpSound = miscItem.PickUpSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);
				var putDownSound = miscItem.PutDownSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);

				if (pickUpSound != null && putDownSound == null) {
					var overrideMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					overrideMisc.PutDownSound.SetTo(pickUpSound);
				} else if (putDownSound != null && pickUpSound == null) {
					var overrideMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					overrideMisc.PickUpSound.SetTo(putDownSound);
				}
			}

			var alchItems = state.LoadOrder.PriorityOrder.Ingestible().WinningOverrides();

			foreach (var alchItem in alchItems) {
				var pickUpSound = alchItem.PickUpSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);
				var putDownSound = alchItem.PutDownSound?.TryResolve<ISoundDescriptorGetter>(state.LinkCache);

				if (pickUpSound != null && putDownSound == null) {
					var overrideAlch = state.PatchMod.Ingestibles.GetOrAddAsOverride(alchItem);
					overrideAlch.PutDownSound.SetTo(pickUpSound);
				} else if (putDownSound != null && pickUpSound == null) {
					var overrideAlch = state.PatchMod.Ingestibles.GetOrAddAsOverride(alchItem);
					overrideAlch.PickUpSound.SetTo(putDownSound);
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
