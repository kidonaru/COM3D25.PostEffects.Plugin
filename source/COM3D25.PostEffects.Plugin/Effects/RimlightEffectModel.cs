using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D25.PostEffects.Plugin
{
    public class RimlightEffectModel : PostEffectModelBase
	{
		public static readonly int MAX_RIMLIGHT_COUNT = 4;

		// シェーダー側 StructuredBuffer と 1:1 のレイアウトを保証する (既定の Auto は順序保証がない)
		[System.Serializable]
		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct RimlightBuffer
		{
			public Color color1;
			public Color color2;
			public Vector3 direction;
			public float lightArea;
			public float fadeRange;
			public float fadeExp;
			public float useNormal;
			public float useAdd;
			public float useMultiply;
			public float useOverlay;
			public float useSubstruct;
			public float excludeFace;
			public float applyHair;
			public float maskMode;
		}

		private RimlightBuffer[] _rimlightBuffers = new RimlightBuffer[MAX_RIMLIGHT_COUNT];
		private int _enabledCount = 0;
		private bool _enableExtraBlend = false;
		private ComputeBuffer _computeBuffer = null;

		public override bool active
		{
			get
			{
				return settings.enabled && _enabledCount > 0;
			}
		}

		public override CameraEvent cameraEvent
		{
			get
			{
				return CameraEvent.BeforeImageEffectsOpaque;
			}
		}

		public override bool isDebugView
		{
			get
			{
				return settings.isDebugView;
			}
		}

		public override bool isExtraBlend
		{
			get
			{
				return _enableExtraBlend;
			}
		}

		// 有効データに顔除外があるときだけ Hub が頭部マスク RT を組む
		public override bool needsHeadMask
		{
			get
			{
				if (!settings.enabled)
				{
					return false;
				}
				for (int i = 0; i < settings.dataList.Count; i++)
				{
					var data = settings.dataList[i];
					if (data.enabled && data.excludeFace)
					{
						return true;
					}
				}
				return false;
			}
		}

		// 有効データにマスク指定があるときだけ CharacterMask の描画を要求する
		public override bool needsCharacterMask
		{
			get
			{
				if (!settings.enabled)
				{
					return false;
				}
				for (int i = 0; i < settings.dataList.Count; i++)
				{
					var data = settings.dataList[i];
					if (data.enabled && data.maskMode != 0)
					{
						return true;
					}
				}
				return false;
			}
		}

		public RimlightEffectSettings settings
		{
			get
			{
				return context.rimlightSettings;
			}
		}

		public override void Dispose()
		{
			if (_computeBuffer != null)
			{
				_computeBuffer.Release();
				_computeBuffer = null;
			}
		}

		public override void OnPreRender()
		{
			if (_computeBuffer == null)
			{
				// ストライドは構造体から導出する (MTE 原典の固定値指定は実サイズと不一致で、
				// Unity 2022.3 は SetData がストライド検証で例外になるため修正)
				_computeBuffer = new ComputeBuffer(MAX_RIMLIGHT_COUNT,
					System.Runtime.InteropServices.Marshal.SizeOf(typeof(RimlightBuffer)));
			}

			camera.depthTextureMode |= DepthTextureMode.DepthNormals;

			BuildRimlightBuffers();
		}

		public override void Prepare(Material material)
		{
			if (!active)
			{
				material.DisableKeyword("RIMLIGHT");
				return;
			}

			_computeBuffer.SetData(_rimlightBuffers);

			material.SetBuffer(Uniforms._RimlightBuffer, _computeBuffer);
			material.SetInt(Uniforms._RimlightCount, _enabledCount);
			// マスク未描画フレームは黒 (=マスクなし相当) にフォールバック
			var maskTexture = CharacterMask.texture;
			material.SetTexture(Uniforms._CharMaskTex, maskTexture != null ? (Texture)maskTexture : Texture2D.blackTexture);

			material.EnableKeyword("RIMLIGHT");
		}

		private static class Uniforms
		{
			internal static readonly int _RimlightBuffer = Shader.PropertyToID("_RimlightBuffer");
			internal static readonly int _RimlightCount = Shader.PropertyToID("_RimlightCount");
			internal static readonly int _CharMaskTex = Shader.PropertyToID("_CharMaskTex");
		}

		private void BuildRimlightBuffers()
		{
			_enabledCount = 0;
			_enableExtraBlend = false;

			if (!settings.enabled)
			{
				return;
			}

			for (int i = 0; i < settings.dataList.Count; i++)
			{
				var data = settings.dataList[i];
				if (!data.enabled)
				{
					continue;
				}

				if (_enabledCount >= MAX_RIMLIGHT_COUNT)
				{
					Debug.LogError("Too many rimlight effects. Max count is " + MAX_RIMLIGHT_COUNT);
					break;
				}

				if (data.useNormal > 0f || data.useMultiply > 0f || data.useOverlay > 0f || data.useSubstruct > 0f)
				{
					_enableExtraBlend = true;
				}

				_rimlightBuffers[_enabledCount] = ConvertToBuffer(data);
				++_enabledCount;
			}
		}

		private RimlightBuffer ConvertToBuffer(RimlightData data)
		{
			var rotation = Quaternion.Euler(data.rotation);
    		Vector3 direction = rotation * Vector3.forward;

			if (data.isWorldSpace)
			{
				direction = camera.worldToCameraMatrix.MultiplyVector(direction);
    			direction = direction.normalized;
			}

			float fadeRange = data.fadeRange * 0.5f;

			return new RimlightBuffer
			{
				color1 = data.color1,
				color2 = data.color2,
				direction = direction,
				lightArea = data.lightArea,
				fadeRange = fadeRange,
				fadeExp = data.fadeExp,
				useNormal = data.useNormal,
				useAdd = data.useAdd,
				useMultiply = data.useMultiply,
				useOverlay = data.useOverlay,
				useSubstruct = data.useSubstruct,
				excludeFace = data.excludeFace ? 1f : 0f,
				applyHair = data.applyHair ? 1f : 0f,
				maskMode = data.maskMode,
			};
		}
	}
}