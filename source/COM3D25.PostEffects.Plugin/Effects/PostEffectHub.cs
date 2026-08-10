using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D25.PostEffects.Plugin
{
	// MTE 由来の CommandBuffer 方式 3 エフェクト (パラフィン/距離フォグ/リムライト) の共有ホスト。
	// OnPreRender で毎フレーム CommandBuffer を組み直し、1 枚の PostEffect マテリアルへ
	// キーワード切替でエフェクトを積む
	[RequireComponent(typeof(Camera))]
	public class PostEffectHub : MonoBehaviour
	{
		public PostEffectContext context = new PostEffectContext();

		private List<PostEffectModelBase> _models = new List<PostEffectModelBase>();

		private Dictionary<CameraEvent, Material> _materials = new Dictionary<CameraEvent, Material>();
		private Dictionary<CameraEvent, CommandBuffer> _commandBuffers = new Dictionary<CameraEvent, CommandBuffer>();

		// 取得時の depthTextureMode。モデルが |= で立てたフラグを全モデル非アクティブ時に戻すために保持する
		private DepthTextureMode _capturedDepthMode;
		private bool _depthModeCaptured;

		// 頭部マスク RT の塗り分けに使うマテリアル (CharMaskChannel、ColorMask で R=顔 / G=髪)
		private Material _faceMaskMaterial;
		private Material _hairMaskMaterial;

		void Awake()
		{
			_models.Add(new ColorParaffinEffectModel());
			_models.Add(new DistanceFogEffectModel());
			_models.Add(new RimlightEffectModel());
		}

		void OnEnable()
		{
			context.camera = GetComponent<Camera>();

			InitMaterial();
			InitCommandBuffer();
		}

		void OnDisable()
		{
			if (_faceMaskMaterial != null)
			{
				DestroyImmediate(_faceMaskMaterial);
				_faceMaskMaterial = null;
			}
			if (_hairMaskMaterial != null)
			{
				DestroyImmediate(_hairMaskMaterial);
				_hairMaskMaterial = null;
			}

			DeleteMaterial();
			DeleteCommandBuffer();

			foreach (var model in _models)
			{
				model.Dispose();
			}
		}

		public static PostEffectHub GetOrAdd(GameObject go)
		{
			if (go == null)
			{
				return null;
			}
			var hub = go.GetComponent<PostEffectHub>();
			return hub != null ? hub : go.AddComponent<PostEffectHub>();
		}

		public static Material LoadMaterial(string shaderName)
		{
			var shader = EffectShaders.GetShader(EffectShaders.PostEffects, shaderName);
			if (shader == null)
			{
				return null;
			}
			var material = new Material(shader);
			material.hideFlags = HideFlags.HideAndDontSave;
			return material;
		}

		private void InitMaterial()
		{
			DeleteMaterial();

			foreach (var model in _models)
			{
				var cameraEvent = model.cameraEvent;
				if (!_materials.ContainsKey(cameraEvent))
				{
					_materials[cameraEvent] = LoadMaterial("PostEffect");
				}
			}
		}

		private void DeleteMaterial()
		{
			if (_materials != null)
			{
				foreach (var material in _materials.Values)
				{
					if (material != null)
					{
						DestroyImmediate(material);
					}
				}
				_materials.Clear();
			}
		}

		private void InitCommandBuffer()
		{
			DeleteCommandBuffer();

			foreach (var model in _models)
			{
				var cameraEvent = model.cameraEvent;
				if (!_commandBuffers.ContainsKey(cameraEvent))
				{
					var buffer = new CommandBuffer();
					buffer.name = "PostEffect_" + cameraEvent;
					context.camera.AddCommandBuffer(cameraEvent, buffer);
					_commandBuffers.Add(cameraEvent, buffer);
				}
			}
		}

		private void DeleteCommandBuffer()
		{
			if (_commandBuffers != null)
			{
				foreach (var pair in _commandBuffers)
				{
					var cameraEvent = pair.Key;
					var buffer = pair.Value;
					if (context.camera != null)
					{
						context.camera.RemoveCommandBuffer(cameraEvent, buffer);
					}
					buffer.Release();
				}
				_commandBuffers.Clear();
			}
		}

		public int GetActiveModelCount(CameraEvent cameraEvent)
		{
			var count = 0;

			foreach (var model in _models)
			{
				if (model.cameraEvent == cameraEvent && model.active)
				{
					++count;
				}
			}

			return count;
		}

		void OnPreCull()
		{
			// CharacterMask は Camera.Render を伴うため OnPreCull でしか描画できない。
			// needsCharacterMask は設定値ベースの判定なのでフレーム遅れなし
			foreach (var model in _models)
			{
				model.Init(context);
				if (model.needsCharacterMask)
				{
					CharacterMask.Render(context.camera);
					break;
				}
			}
		}

		void OnPreRender()
		{
			foreach (var buffer in _commandBuffers.Values)
			{
				buffer.Clear();
			}

			if (!_depthModeCaptured)
			{
				_capturedDepthMode = context.camera.depthTextureMode;
				_depthModeCaptured = true;
			}

			bool anyActive = false;
			foreach (var model in _models)
			{
				model.Init(context);
				model.OnPreRender();
				anyActive |= model.active;
			}

			// モデルの OnPreRender は非アクティブでも depthTextureMode を |= で立てるため、
			// 全モデル非アクティブなら取得時のモードへ戻して余分な深度パスを残さない
			if (!anyActive)
			{
				context.camera.depthTextureMode = _capturedDepthMode;
			}

			foreach (var pair in _commandBuffers)
			{
				var cameraEvent = pair.Key;
				var buffer = pair.Value;
				var activeModelCount = GetActiveModelCount(cameraEvent);
				Material material;

				if (activeModelCount > 0 && _materials.TryGetValue(cameraEvent, out material))
				{
					if (material == null)
					{
						continue; // シェーダーバンドル読込失敗時は素通し
					}

					bool isDebugView = false;
					bool isExtraBlend = false;
					bool needHeadMask = false;

					foreach (var model in _models)
					{
						if (model.cameraEvent == cameraEvent)
						{
							model.Prepare(material);
							isDebugView |= model.isDebugView;
							isExtraBlend |= model.isExtraBlend;
							needHeadMask |= model.active && model.needsHeadMask;
						}
					}

					PostEffectModelBase.SetKeyword(material, "DEBUG_VIEW", isDebugView);
					PostEffectModelBase.SetKeyword(material, "EXTRA_BLEND", isExtraBlend);

					if (needHeadMask)
					{
						BuildHeadMask(buffer);
					}

					buffer.GetTemporaryRT(Uniforms._TempRT, -1, -1, 24, FilterMode.Bilinear);
					buffer.Blit(BuiltinRenderTextureType.CameraTarget, Uniforms._TempRT);
					buffer.Blit(Uniforms._TempRT, BuiltinRenderTextureType.CameraTarget, material);
					buffer.ReleaseTemporaryRT(Uniforms._TempRT);

					if (needHeadMask)
					{
						// 一時 RT の再利用事故を防ぐため、参照する Blit より後で解放する
						buffer.ReleaseTemporaryRT(Uniforms._HeadMaskRT);
					}
				}
			}
		}

		// 頭部 Renderer を CharMaskChannel で塗り分けた画面サイズのマスク RT を組む (R=顔, G=髪)。
		// ColorMask は他チャンネルの既存値を消さないため、必ず髪→顔の順で描くこと。
		// 髪を先に描いておけば、髪が手前のピクセルでは後続の顔パスが深度テストに落ちて R が立たない。
		// レイヤー・サブカメラを使わない CommandBuffer 完結の方式。
		// 既知の制約: 遮蔽判定がないため、壁の裏に顔がある場合も手前のリム光がわずかに消える
		private void BuildHeadMask(CommandBuffer buffer)
		{
			if (_faceMaskMaterial == null)
			{
				_faceMaskMaterial = LoadMaterial("CharMaskChannel");
				if (_faceMaskMaterial != null)
				{
					// ColorMask: R=8, G=4 (書き込み先チャンネルの選択)
					_faceMaskMaterial.SetFloat(Uniforms._ColorMask, 8f);
					_hairMaskMaterial = new Material(_faceMaskMaterial);
					_hairMaskMaterial.hideFlags = HideFlags.HideAndDontSave;
					_hairMaskMaterial.SetFloat(Uniforms._ColorMask, 4f);
				}
			}

			buffer.GetTemporaryRT(Uniforms._HeadMaskRT, -1, -1, 24, FilterMode.Bilinear,
				CharacterMask.preferredRGFormat);
			buffer.SetRenderTarget(Uniforms._HeadMaskRT);
			buffer.ClearRenderTarget(true, true, Color.black);

			if (_faceMaskMaterial != null)
			{
				List<Renderer> faceRenderers, hairRenderers;
				HeadMask.CollectRenderers(out faceRenderers, out hairRenderers);
				DrawMaskRenderers(buffer, hairRenderers, _hairMaskMaterial);
				DrawMaskRenderers(buffer, faceRenderers, _faceMaskMaterial);
			}

			buffer.SetGlobalTexture(Uniforms._HeadMaskTex, Uniforms._HeadMaskRT);
		}

		private static void DrawMaskRenderers(CommandBuffer buffer, List<Renderer> renderers, Material material)
		{
			for (int i = 0; i < renderers.Count; i++)
			{
				var renderer = renderers[i];
				if (renderer == null || !renderer.enabled)
				{
					continue;
				}

				var submeshCount = renderer.sharedMaterials.Length;
				for (int sub = 0; sub < submeshCount; sub++)
				{
					buffer.DrawRenderer(renderer, material, sub, 0);
				}
			}
		}

		private static class Uniforms
		{
			internal static readonly int _TempRT = Shader.PropertyToID("_TempRT");
			internal static readonly int _HeadMaskRT = Shader.PropertyToID("_HeadMaskRT");
			internal static readonly int _HeadMaskTex = Shader.PropertyToID("_HeadMaskTex");
			internal static readonly int _ColorMask = Shader.PropertyToID("_ColorMask");
		}
	}
}
