using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
	[System.Serializable]
	public class RimlightData : IPostEffectData
	{
		public bool enabled = false;
		public Color color1 = new Color(0.77f, 0.70f, 1f, 1f);
		public Color color2 = new Color(0.77f, 0.70f, 1f, 0f);

		public Vector3 rotation = new Vector3(10f, -40f, 0f);
		public float lightArea = 1f;
		public float fadeRange = 0.2f;
		public float fadeExp = 1f;

		[Header("Blend Mode")]
		[Range(0f, 1f)]
		public float useNormal = 0f;
		[Range(0f, 1f)]
		public float useAdd = 0.8f;
		[Range(0f, 1f)]
		public float useMultiply = 0f;
		[Range(0f, 1f)]
		public float useOverlay = 0f;
		[Range(0f, 1f)]
		public float useSubstruct = 0f;

		public bool isWorldSpace = false;
		// 頭部 (顔・髪・頭アクセ) にリムライトを乗せない
		public bool excludeFace = true;
		// excludeFace 時でも髪 (髪・帽子・髪アクセ) には適用する
		public bool applyHair = true;
		// 0=マスクなし / 1=キャラ除外 / 2=キャラのみ
		public int maskMode = 2;

		public void CopyFrom(IPostEffectData data)
		{
			var rimlightData = data as RimlightData;
			enabled = rimlightData.enabled;
			color1 = rimlightData.color1;
			color2 = rimlightData.color2;
			rotation = rimlightData.rotation;
			lightArea = rimlightData.lightArea;
			fadeRange = rimlightData.fadeRange;
			fadeExp = rimlightData.fadeExp;
			useNormal = rimlightData.useNormal;
			useAdd = rimlightData.useAdd;
			useMultiply = rimlightData.useMultiply;
			useOverlay = rimlightData.useOverlay;
			useSubstruct = rimlightData.useSubstruct;
			isWorldSpace = rimlightData.isWorldSpace;
			excludeFace = rimlightData.excludeFace;
			applyHair = rimlightData.applyHair;
			maskMode = rimlightData.maskMode;
		}

		public static RimlightData Lerp(
            RimlightData a,
            RimlightData b,
            float t)
        {
			return new RimlightData
			{
				enabled = a.enabled,
				color1 = Color.Lerp(a.color1, b.color1, t),
				color2 = Color.Lerp(a.color2, b.color2, t),
				rotation = Vector3.Lerp(a.rotation, b.rotation, t),
				lightArea = Mathf.Lerp(a.lightArea, b.lightArea, t),
				fadeRange = Mathf.Lerp(a.fadeRange, b.fadeRange, t),
				fadeExp = Mathf.Lerp(a.fadeExp, b.fadeExp, t),
				useNormal = Mathf.Lerp(a.useNormal, b.useNormal, t),
				useAdd = Mathf.Lerp(a.useAdd, b.useAdd, t),
				useMultiply = Mathf.Lerp(a.useMultiply, b.useMultiply, t),
				useOverlay = Mathf.Lerp(a.useOverlay, b.useOverlay, t),
				useSubstruct = Mathf.Lerp(a.useSubstruct, b.useSubstruct, t),
				isWorldSpace = a.isWorldSpace,
				excludeFace = a.excludeFace,
				applyHair = a.applyHair,
				maskMode = a.maskMode,
			};
		}
	}

    [System.Serializable]
	public class RimlightEffectSettings : PostEffectSettingsBase<RimlightData>
	{
	}
}