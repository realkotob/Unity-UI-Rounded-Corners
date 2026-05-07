using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nobi.UiRoundedCorners.EditorTools {
	internal static class RoundedCornersOverrideCleaner {
		private const string MenuRoot = "Tools/UI Rounded Corners/";

		[MenuItem(MenuRoot + "Diagnose Material Overrides (Open Scenes + Selection)")]
		private static void Diagnose() {
			var sb = new StringBuilder();
			sb.AppendLine("[RoundedCornersOverrideCleaner] Diagnosis:");

			for (var s = 0; s < SceneManager.sceneCount; s++) {
				var scene = SceneManager.GetSceneAt(s);
				if (!scene.isLoaded) continue;
				sb.AppendLine($"-- Scene: {scene.name} --");
				foreach (var go in scene.GetRootGameObjects()) {
					DiagnoseRoot(go, sb);
				}
			}

			foreach (var sel in Selection.gameObjects) {
				sb.AppendLine($"-- Selection: {sel.name} --");
				DiagnoseRoot(sel, sb);
			}

			Debug.Log(sb.ToString());
		}

		private static void DiagnoseRoot(GameObject root, StringBuilder sb) {
			var graphics = new List<Graphic>();
			root.GetComponentsInChildren(includeInactive: true, results: graphics);

			foreach (var g in graphics) {
				if (!HasRoundedCornersComponent(g.gameObject)) continue;

				var path = GetHierarchyPath(g.transform);
				sb.Append("  ").Append(path).AppendLine();

				DumpAllOverrides(g.gameObject, sb);
			}
		}

		private static void DumpAllOverrides(GameObject go, StringBuilder sb) {
			if (!PrefabUtility.IsPartOfPrefabInstance(go)) {
				sb.AppendLine("    (not a prefab instance)");
				return;
			}

			var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
			var mods = PrefabUtility.GetPropertyModifications(instanceRoot);
			if (mods == null || mods.Length == 0) {
				sb.AppendLine("    (no PropertyModifications on instance root)");
				return;
			}

			var anyForThisGo = false;
			foreach (var mod in mods) {
				if (mod == null || mod.target == null) continue;

				// Only print mods whose target component is on this GameObject.
				var comp = mod.target as Component;
				if (comp == null || comp.gameObject != go) continue;

				anyForThisGo = true;
				sb.Append("    [")
				  .Append(comp.GetType().Name)
				  .Append("] ")
				  .Append(mod.propertyPath)
				  .Append(" = ")
				  .Append(string.IsNullOrEmpty(mod.value) ? "(empty)" : mod.value);

				if (mod.objectReference != null) {
					sb.Append(" (ref: ").Append(mod.objectReference.GetType().Name)
					  .Append(" '").Append(mod.objectReference.name).Append("')");
				}
				sb.AppendLine();
			}

			if (!anyForThisGo) {
				sb.AppendLine("    (no PropertyModifications targeting components on this GO)");
			}
		}

		[MenuItem(MenuRoot + "Clear Leaked Material Overrides (Prefab Assets)")]
		private static void CleanPrefabAssets() {
			if (!EditorUtility.DisplayDialog(
				"Clear Leaked Material Overrides",
				"Scan every prefab under Assets/ and clear Graphic.m_Material on objects that have a rounded-corners component. This will modify and save prefab assets. Make sure your changes are committed first.",
				"Run", "Cancel")) return;

			var guids = AssetDatabase.FindAssets("t:Prefab");
			var totalCleared = 0;
			var prefabsTouched = 0;

			try {
				for (var i = 0; i < guids.Length; i++) {
					var path = AssetDatabase.GUIDToAssetPath(guids[i]);
					EditorUtility.DisplayProgressBar("Cleaning prefabs", path, (float)i / guids.Length);

					var root = PrefabUtility.LoadPrefabContents(path);
					try {
						var cleared = ClearOnPrefabAssetRoot(root);
						if (cleared > 0) {
							PrefabUtility.SaveAsPrefabAsset(root, path);
							totalCleared += cleared;
							prefabsTouched++;
							Debug.Log($"[RoundedCornersOverrideCleaner] Cleared {cleared} on prefab: {path}");
						}
					} finally {
						PrefabUtility.UnloadPrefabContents(root);
					}
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}

			AssetDatabase.SaveAssets();
			Debug.Log($"[RoundedCornersOverrideCleaner] Done. Cleared {totalCleared} material reference(s) across {prefabsTouched} prefab(s).");
		}

		[MenuItem(MenuRoot + "Clear Leaked Material Overrides (Open Scenes)")]
		private static void CleanOpenScenes() {
			if (!EditorUtility.DisplayDialog(
				"Clear Leaked Material Overrides",
				"Scan every loaded scene and revert instance overrides on Graphic.m_Material for objects that have a rounded-corners component. Scenes will be marked dirty so you can save them.",
				"Run", "Cancel")) return;

			var totalReverted = 0;
			var totalCleared = 0;
			var scenesTouched = 0;

			for (var s = 0; s < SceneManager.sceneCount; s++) {
				var scene = SceneManager.GetSceneAt(s);
				if (!scene.isLoaded) continue;

				var reverted = 0;
				var cleared = 0;
				foreach (var go in scene.GetRootGameObjects()) {
					ClearOnSceneRoot(go, ref reverted, ref cleared);
				}

				if (reverted + cleared > 0) {
					EditorSceneManager.MarkSceneDirty(scene);
					totalReverted += reverted;
					totalCleared += cleared;
					scenesTouched++;
					Debug.Log($"[RoundedCornersOverrideCleaner] Scene '{scene.name}': reverted {reverted} prefab override(s), cleared {cleared} non-prefab material(s).");
				}
			}

			Debug.Log($"[RoundedCornersOverrideCleaner] Done. {totalReverted} prefab override(s) reverted, {totalCleared} non-prefab material(s) cleared across {scenesTouched} scene(s). Save scenes to persist.");
		}

		private static int ClearOnPrefabAssetRoot(GameObject root) {
			var cleared = 0;
			var graphics = new List<Graphic>();
			root.GetComponentsInChildren(includeInactive: true, results: graphics);

			foreach (var g in graphics) {
				if (!HasRoundedCornersComponent(g.gameObject)) continue;

				var so = new SerializedObject(g);
				var prop = so.FindProperty("m_Material");
				if (prop == null) continue;
				if (prop.objectReferenceValue == null) continue;

				prop.objectReferenceValue = null;
				so.ApplyModifiedPropertiesWithoutUndo();
				cleared++;
			}

			return cleared;
		}

		private static void ClearOnSceneRoot(GameObject root, ref int reverted, ref int cleared) {
			var graphics = new List<Graphic>();
			root.GetComponentsInChildren(includeInactive: true, results: graphics);

			foreach (var g in graphics) {
				if (!HasRoundedCornersComponent(g.gameObject)) continue;

				var so = new SerializedObject(g);
				var prop = so.FindProperty("m_Material");
				if (prop == null) continue;

				var isInstance = PrefabUtility.IsPartOfPrefabInstance(g);

				if (isInstance && prop.prefabOverride) {
					// Revert the override entirely so it can never re-apply itself.
					PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
					reverted++;
					continue;
				}

				if (prop.objectReferenceValue != null) {
					prop.objectReferenceValue = null;
					so.ApplyModifiedPropertiesWithoutUndo();
					cleared++;
				}
			}
		}

		private static bool HasRoundedCornersComponent(GameObject go) {
			return go.GetComponent<ImageWithRoundedCorners>() != null
				|| go.GetComponent<ImageWithIndependentRoundedCorners>() != null;
		}

		private static string GetHierarchyPath(Transform t) {
			var sb = new StringBuilder(t.name);
			var p = t.parent;
			while (p != null) {
				sb.Insert(0, "/");
				sb.Insert(0, p.name);
				p = p.parent;
			}
			return sb.ToString();
		}
	}
}
