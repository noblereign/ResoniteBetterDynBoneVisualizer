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

	public static System.Runtime.CompilerServices.ConditionalWeakTable<DynamicBoneChain, ChainVisuals> visualCache = new();

	public class ChainVisuals {
		public WeakReference<Slot> RootSlot;
		public IAssetProvider<Material> NormalMat;
		public IAssetProvider<Material> TerminalMat;
		public IAssetProvider<Material> LineMat;
		public IAssetProvider<Material> GrabbedMat;
		
		public class PointLinePair {
			public Slot PointSlot;
			public MeshRenderer PointRenderer;
			public Slot LineSlot;
			public CylinderMesh LineMesh;
		}
		
		public System.Collections.Generic.List<PointLinePair> Pairs = new();
		
		public ChainVisuals(Slot rootSlot, IAssetProvider<Material> normal, IAssetProvider<Material> terminal, IAssetProvider<Material> line, IAssetProvider<Material> grabbed) {
			RootSlot = new WeakReference<Slot>(rootSlot);
			NormalMat = normal;
			TerminalMat = terminal;
			LineMat = line;
			GrabbedMat = grabbed;
		}
		
		public void Update(DynamicBoneChain chain) {
			if (!RootSlot.TryGetTarget(out Slot root) || root == null || root.IsDestroyed) return;
			
			var data = chain._data;
			if (data == null) return;
			
			while (Pairs.Count < data.Length) {
				Slot point = root.AddSlot("DebugBonePoint");
				point.PersistentSelf = false;
				var sMesh = point.AttachComponent<SphereMesh>();
				sMesh.Radius.Value = 1f;
				var pRenderer = point.AttachComponent<MeshRenderer>();
				pRenderer.Mesh.Target = sMesh;
				pRenderer.Materials.Add().Target = NormalMat;
				
				Slot line = root.AddSlot("DebugBoneLine");
				line.PersistentSelf = false;
				var lMesh = line.AttachComponent<CylinderMesh>();
				lMesh.Radius.Value = 0.005f;
				lMesh.Height.Value = 1f;
				var lRenderer = line.AttachComponent<MeshRenderer>();
				lRenderer.Mesh.Target = lMesh;
				lRenderer.Materials.Add().Target = LineMat;
				
				Pairs.Add(new PointLinePair {
					PointSlot = point,
					PointRenderer = pRenderer,
					LineSlot = line,
					LineMesh = lMesh
				});
			}
			while (Pairs.Count > data.Length) {
				var pair = Pairs[Pairs.Count - 1];
				if (pair.PointSlot != null) pair.PointSlot.Destroy();
				if (pair.LineSlot != null) pair.LineSlot.Destroy();
				Pairs.RemoveAt(Pairs.Count - 1);
			}
			
			Slot space = chain._space;
			if (space == null) return;
			
			for (int i = 0; i < data.Length; i++) {
				var p = Pairs[i];
				var d = data[i];
				
				float3 globalPos = space.LocalPointToGlobal(in d.pos);
				float globalRadius = space.LocalScaleToGlobal(d.radius);
				
				if (i > 0) {
					if (p.PointSlot != null && !p.PointSlot.IsDestroyed) {
						if (!p.PointSlot.ActiveSelf) p.PointSlot.ActiveSelf = true;
						p.PointSlot.GlobalPosition = globalPos;
						p.PointSlot.GlobalScale = new float3(globalRadius, globalRadius, globalRadius);
						
						var mat = d.isIK ? GrabbedMat : (d.childCount == 0 ? TerminalMat : NormalMat);
						if (p.PointRenderer != null && p.PointRenderer.Materials.Count > 0 && p.PointRenderer.Materials[0] != mat) {
							p.PointRenderer.Materials[0] = mat;
						}
					}
				} else {
					if (p.PointSlot != null && !p.PointSlot.IsDestroyed && p.PointSlot.ActiveSelf) p.PointSlot.ActiveSelf = false;
				}
				
				if (d.parentIndex >= 0 && d.parentIndex < data.Length) {
					if (p.LineSlot != null && !p.LineSlot.IsDestroyed) {
						if (!p.LineSlot.ActiveSelf) p.LineSlot.ActiveSelf = true;
						float3 parentPos = space.LocalPointToGlobal(in data[d.parentIndex].pos);
						
						float3 offset = globalPos - parentPos;
						float length = offset.Magnitude;
						
						if (length > 0) {
							float3 dir = offset / length;
							p.LineSlot.GlobalPosition = parentPos + dir * length * 0.5f;
							p.LineSlot.GlobalRotation = floatQ.LookRotation(in dir) * floatQ.FromToRotation(float3.Forward, float3.Up);
						} else {
							p.LineSlot.GlobalPosition = parentPos;
						}
						
						p.LineSlot.GlobalScale = float3.One;
						if (p.LineMesh != null && p.LineMesh.Height.Value != length) {
							p.LineMesh.Height.Value = length;
						}
					}
				} else {
					if (p.LineSlot != null && !p.LineSlot.IsDestroyed && p.LineSlot.ActiveSelf) p.LineSlot.ActiveSelf = false;
				}
			}
		}
	}


	public static void GenerateDebugVisual(DynamicBoneChain boneChain) {
		ClearDebugVisual(boneChain);

		Slot debugRoot = boneChain.Slot.AddSlot("DYNBONE_DEBUG_ROOT");
		debugRoot.PersistentSelf = false;
		debugRoot.Tag = "DYNBONE_DEBUG";

		OverlayFresnelMaterial GetMaterial(colorX color) {
			OverlayFresnelMaterial overlayFresnelMaterial = debugRoot.AttachComponent<OverlayFresnelMaterial>();
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

		var visuals = new ChainVisuals(debugRoot, normalMat, terminalMat, lineMat, grabbedMat);
		visuals.Update(boneChain);
		visualCache.AddOrUpdate(boneChain, visuals);
	}

	public static void ClearDebugVisual(DynamicBoneChain boneChain) {
		var toDestroy = new System.Collections.Generic.List<Slot>();

		void FindTags(Slot s) {
			if (s == null) return;
			foreach (var child in s.Children) {
				if (child.Tag == "DYNBONE_DEBUG") {
					toDestroy.Add(child);
				}
			}
		}

		FindTags(boneChain.Slot);
		foreach (var bone in boneChain.Bones) {
			if (bone != null && bone.IsValid && bone.BoneSlot.Target != null) {
				FindTags(bone.BoneSlot.Target);
			}
		}

		foreach (var s in toDestroy) {
			if (s != null && !s.IsDestroyed) {
				s.Destroy();
			}
		}

		if (visualCache.TryGetValue(boneChain, out var vis)) {
			if (vis.RootSlot.TryGetTarget(out Slot root) && root != null) {
				root.Destroy();
			}
			visualCache.Remove(boneChain);
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

	[HarmonyPatch(typeof(DynamicBoneChain), "FinishSimulation")]
	class DynamicBoneChain_FinishSimulation_Patch {
		public static void Postfix(DynamicBoneChain __instance) {
			if (visualCache.TryGetValue(__instance, out var vis)) {
				vis.Update(__instance);
			}
		}
	}

}
