using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteModLoader;

#if DEBUG
using ResoniteHotReloadLib;
#endif

namespace LessFlashyDebugVisuals;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class LessFlashyDebugVisuals : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
	public override string Name => "LessFlashyDebugVisuals";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/ResoniteLessFlashyDebugVisuals/";

	const string harmonyId = "dog.glacier.LessFlashyDebugVisuals";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	public static ModConfigurationKey<bool> Enabled = new("Enabled", "Enables the mod, pretty self-explanatory.", () => true);

	public override void OnEngineInit() {
#if DEBUG
		HotReloader.RegisterForHotReload(this);
#endif

		Config = GetConfiguration()!;
		Config!.Save(true);

		Setup();
	}

	static void Setup() {
		// Patch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.PatchAll();
	}

#if DEBUG
	// This is the method that should be used to unload your mod
	// This means removing patches, clearing memory that may be in use etc.
	static void BeforeHotReload() {
		// Unpatch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.UnpatchAll(harmonyId);
	}

	// This is called in the newly loaded assembly
	// Load your mod here like you normally would in OnEngineInit

	static void OnHotReload(ResoniteMod modInstance) {
		// Get the config if needed
		Config = modInstance.GetConfiguration()!;
		Config!.Save(true);

		// Call setup method
		Setup();
	}
#endif

	private static void GenerateDebugVisualIntern(DynamicBoneChain boneChain, IAssetProvider<Material> normalMat, IAssetProvider<Material> terminalMat, IAssetProvider<Material> lineMat, IAssetProvider<Material> grabbedMat) {
		var validBones = new System.Collections.Generic.List<Slot>();
		var radiusModifiers = new System.Collections.Generic.Dictionary<Slot, float>();
		foreach (var bone in boneChain.Bones) {
			if (bone.IsValid && bone.BoneSlot.Target != null) {
				validBones.Add(bone.BoneSlot.Target);
				radiusModifiers[bone.BoneSlot.Target] = bone.RadiusModifier.Value;
			}
		}

		if (validBones.Count == 0) return;

		validBones.Sort((a, b) => a.HierachyDepth.CompareTo(b.HierachyDepth));

		int[] childCounts = new int[validBones.Count];
		int[] parentIndices = new int[validBones.Count];
		parentIndices[0] = -1;
		for (int i = 1; i < validBones.Count; i++) {
			parentIndices[i] = 0;
			for (int n = i - 1; n >= 0; n--) {
				if (validBones[i].IsChildOf(validBones[n])) {
					childCounts[n]++;
					parentIndices[i] = n;
					break;
				}
			}
		}

		var simNodes = new System.Collections.Generic.List<(Slot slot, bool isVirtual, int parentIndex)>();
		for (int i = 0; i < validBones.Count; i++) {
			simNodes.Add((validBones[i], false, parentIndices[i]));
		}

		bool simulateTerm = boneChain.SimulateTerminalBones.Value;
		if (simulateTerm) {
			int count = simNodes.Count;
			for (int i = 0; i < count; i++) {
				if (childCounts[i] == 0) {
					simNodes.Add((validBones[i], true, i));
				}
			}
		}

		bool[] isIK = new bool[simNodes.Count];
		int effectorIndex = boneChain.EffectorBoneIndex.Value;
		if (effectorIndex >= 0 && effectorIndex < simNodes.Count) {
			int curr = effectorIndex;
			while (curr >= 0) {
				isIK[curr] = true;
				curr = simNodes[curr].parentIndex;
			}
		}

		System.Collections.Generic.Dictionary<Slot, bool> boneGrabbed = new();
		System.Collections.Generic.Dictionary<Slot, bool> virtualBoneGrabbed = new();
		for (int i = 0; i < simNodes.Count; i++) {
			if (simNodes[i].isVirtual) {
				virtualBoneGrabbed[simNodes[i].slot] = isIK[i];
			} else {
				boneGrabbed[simNodes[i].slot] = isIK[i];
			}
		}

		float rootScale = MathX.AvgComponent(validBones[0].GlobalScale);
		float baseGlobalRadius = boneChain.BaseBoneRadius.Value * rootScale;

		for (int i = 0; i < validBones.Count; i++) {
			Slot boneSlot = validBones[i];
			float globalRadius = baseGlobalRadius * radiusModifiers[boneSlot];
			bool isTerminal = (childCounts[i] == 0);
			bool isGrabbed = boneGrabbed.TryGetValue(boneSlot, out bool grabbed) && grabbed;

			if (i > 0) {
				Slot pointSlot = boneSlot.AddSlot("DebugBonePoint");
				pointSlot.PersistentSelf = false;
				pointSlot.Tag = "DYNBONE_DEBUG";
				float localRadius = boneSlot.GlobalScaleToLocal(globalRadius);
				pointSlot.AttachSphere(localRadius, isGrabbed ? grabbedMat : ((isTerminal && !simulateTerm) ? terminalMat : normalMat), collider: false);
			}

			int pIndex = parentIndices[i];
			Slot parentSlot = pIndex >= 0 ? validBones[pIndex] : null;

			if (parentSlot != null) {
				float length;
				float3 dir = parentSlot.GlobalPointToLocal(boneSlot.GlobalPosition).GetNormalized(out length);
				Slot lineSlot = parentSlot.AddSlot("DebugBoneLine");
				lineSlot.PersistentSelf = false;
				lineSlot.Tag = "DYNBONE_DEBUG";
				lineSlot.LocalRotation = floatQ.LookRotation(in dir) * floatQ.FromToRotation(float3.Forward, float3.Up);
				lineSlot.LocalPosition = dir * length * 0.5f;
				
				// Match FrooxEngine's native Debug.Line thickness (1mm)
				lineSlot.AttachCylinder(parentSlot.GlobalScaleToLocal(0.005f), length, lineMat, collider: false);
			}

			if (isTerminal && simulateTerm) {
				float3 offset = float3.Zero;
				if (parentSlot != null) {
					offset = boneSlot.GlobalPosition - parentSlot.GlobalPosition;
				} else {
					Slot walk = boneSlot;
					while (MathX.Approximately(walk.LocalPosition.Magnitude, 0f) && !walk.IsRootSlot) {
						walk = walk.Parent;
					}
					if (!walk.IsRootSlot) {
						offset = walk.Parent.LocalVectorToGlobal(walk.LocalPosition);
					} else {
						offset = boneSlot.Forward;
					}
				}

				float3 localOffset = boneSlot.GlobalVectorToLocal(offset);

				Slot simPointSlot = boneSlot.AddSlot("DebugSimulatedBonePoint");
				simPointSlot.PersistentSelf = false;
				simPointSlot.Tag = "DYNBONE_DEBUG";
				simPointSlot.LocalPosition = localOffset;
				float simLocalRadius = boneSlot.GlobalScaleToLocal(globalRadius);
				
				bool isSimGrabbed = virtualBoneGrabbed.TryGetValue(boneSlot, out bool vGrabbed) && vGrabbed;
				simPointSlot.AttachSphere(simLocalRadius, isSimGrabbed ? grabbedMat : terminalMat, collider: false);

				float3 dir = localOffset.GetNormalized(out float length);
				if (length > 0) {
					Slot simLineSlot = boneSlot.AddSlot("DebugSimulatedBoneLine");
					simLineSlot.PersistentSelf = false;
					simLineSlot.Tag = "DYNBONE_DEBUG";
					simLineSlot.LocalRotation = floatQ.LookRotation(in dir) * floatQ.FromToRotation(float3.Forward, float3.Up);
					simLineSlot.LocalPosition = dir * length * 0.5f;
					simLineSlot.AttachCylinder(boneSlot.GlobalScaleToLocal(0.005f), length, lineMat, collider: false);
				}
			}
		}
	}


	public static void GenerateDebugVisual(DynamicBoneChain boneChain) {
		OverlayFresnelMaterial GetMaterial(colorX color) {
			OverlayFresnelMaterial overlayFresnelMaterial = boneChain.World.AssetsSlot.AddSlot("RigDebugMaterial_" + color.ToString()).AttachComponent<OverlayFresnelMaterial>();
			overlayFresnelMaterial.Slot.PersistentSelf = false;
			overlayFresnelMaterial.BlendMode.Value = BlendMode.Alpha;
			overlayFresnelMaterial.FrontNearColor.Value = color * 0.5f;
			overlayFresnelMaterial.FrontFarColor.Value = color;
			overlayFresnelMaterial.SetBehindColorFromFront((colorX c) => c.SetA(0.73f));
			return overlayFresnelMaterial;
		}

		IAssetProvider<Material> normalMat = GetMaterial(colorX.Cyan);
		IAssetProvider<Material> terminalMat = GetMaterial(colorX.Blue);
		IAssetProvider<Material> lineMat = GetMaterial(colorX.Yellow);
		IAssetProvider<Material> grabbedMat = GetMaterial(colorX.Magenta);

		GenerateDebugVisualIntern(boneChain, normalMat, terminalMat, lineMat, grabbedMat);
	}

	public static void ClearDebugVisual(DynamicBoneChain boneChain) {
		foreach (var bone in boneChain.Bones) {
			if (bone.BoneSlot.Target != null) {
				foreach (Slot item in bone.BoneSlot.Target.GetChildrenWithTag("DYNBONE_DEBUG")) {
					item.Destroy();
				}
			}
		}
		foreach (Slot item in boneChain.Slot.GetChildrenWithTag("DYNBONE_DEBUG")) {
			item.Destroy();
		}
	}

	[HarmonyPatch(typeof(DynamicBoneChain), "BuildInspectorUI")]
	class DynamicBoneChain_BuildInspectorUI_Patch {
		public static void Postfix(DynamicBoneChain __instance, UIBuilder ui) {
			if (!Config!.GetValue(Enabled)) return;

			ui.Style.MinHeight = 24f;
			ui.Text("Better DynBone Debug Visuals (Mod)").Color.Value = RadiantUI_Constants.Hero.CYAN;
			ui.Style.MinHeight = 2f;
			ui.Image(RadiantUI_Constants.Hero.CYAN);
			ui.Style.MinHeight = 24f;

			Button generateButton = ui.Button("Generate debug visuals");

			generateButton.LocalPressed += (btn, data) => {
				__instance.World.RunSynchronously(() => {
					GenerateDebugVisual(__instance);
				});
			};

			Button clearButton = ui.Button("Clear debug visuals");

			clearButton.LocalPressed += (btn, data) => {
				__instance.World.RunSynchronously(() => {
					ClearDebugVisual(__instance);
				});
			};
		}
	}

}
