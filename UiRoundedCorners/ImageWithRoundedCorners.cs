using UnityEngine;
using UnityEngine.UI;

namespace Nobi.UiRoundedCorners {
	[ExecuteInEditMode]                             //Required to check the OnEnable function
	[DisallowMultipleComponent]                     //You can only have one of these in every object.
	[RequireComponent(typeof(RectTransform))]
	public class ImageWithRoundedCorners : MonoBehaviour, IMaterialModifier {
		private static readonly int Props = Shader.PropertyToID("_WidthHeightRadius");
		private static readonly int prop_OuterUV = Shader.PropertyToID("_OuterUV");

		public float radius = 40f;
		private Material material;
		private Vector4 outerUV = new Vector4(0, 0, 1, 1);

		[HideInInspector, SerializeField] private MaskableGraphic image;

		private void OnValidate() {
			Validate();
			Refresh();
		}

		private void OnDestroy() {
			DestroyHelper.Destroy(material);
			material = null;

			if (image != null) {
				image.SetMaterialDirty();   //Force the Graphic to re-evaluate materialForRendering without us in the IMaterialModifier list
			}
			image = null;
		}

		private void OnEnable() {
			//You can only add either ImageWithRoundedCorners or ImageWithIndependentRoundedCorners
			//It will replace the other component when added into the object.
			var other = GetComponent<ImageWithIndependentRoundedCorners>();
			if (other != null) {
				radius = other.r.x;                 //When it does, transfer the radius value to this script
				DestroyHelper.Destroy(other);
			}

			Validate();
			Refresh();

			if (image != null) {
				image.SetMaterialDirty();
			}
		}

		private void OnDisable() {
			if (image != null) {
				image.SetMaterialDirty();
			}
		}

		private void OnRectTransformDimensionsChange() {
			if (enabled && material != null) {
				Refresh();
			}
		}

		public void Validate() {
			if (image == null) {
				TryGetComponent(out image);
			}

			if (image is Image uiImage && uiImage.sprite != null) {
				outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(uiImage.sprite);
			}

			if (image != null) {
				image.SetMaterialDirty();
			}
		}

		public void Refresh() {
			if (material == null) return;

			var rect = ((RectTransform)transform).rect;

			//Multiply radius value by 2 to make the radius value appear consistent with ImageWithIndependentRoundedCorners script.
			//Right now, the ImageWithIndependentRoundedCorners appears to have double the radius than this.
			material.SetVector(Props, new Vector4(rect.width, rect.height, radius * 2, 0));
			material.SetVector(prop_OuterUV, outerUV);
		}

		// IMaterialModifier — supplies the rounded-corner material at render time without
		// writing to Graphic.m_Material, so prefab instances never get a material override.
		public Material GetModifiedMaterial(Material baseMaterial) {
			if (!isActiveAndEnabled) return baseMaterial;

			if (material == null) {
				var shader = Shader.Find("UI/RoundedCorners/RoundedCorners");
				if (shader == null) return baseMaterial;
				material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			}

			Refresh();
			return material;
		}
	}
}
