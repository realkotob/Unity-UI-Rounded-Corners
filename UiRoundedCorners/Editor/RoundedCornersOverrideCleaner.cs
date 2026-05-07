using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nobi.UiRoundedCorners.EditorTools {
	internal static class RoundedCornersOverrideCleaner {
		private const string MenuRoot = "Tools/UI Rounded Corners/";

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
						}
					} finally {
						PrefabUtility.UnloadPrefabContents(root);
					}
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}

			AssetDatabase.SaveAssets();
			Debug.Log($"[RoundedCornersOverrideCleaner] Cleared {totalCleared} material reference(s) across {prefabsTouched} prefab(s).");
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
				}
			}

			Debug.Log($"[RoundedCornersOverrideCleaner] {totalReverted} prefab override(s) reverted, {totalCleared} non-prefab material(s) cleared across {scenesTouched} scene(s). Save scenes to persist.");
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

				if (PrefabUtility.IsPartOfPrefabInstance(g) && prop.prefabOverride) {
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
	}
}
