using UnityEngine;
using UnityEngine.Rendering;

namespace COM3D25.PostEffects.Plugin
{
    public abstract class PostEffectModelBase
	{
		public PostEffectContext context { get; private set; }

		public Camera camera
		{
			get
			{
				return context.camera;
			}
		}

		public abstract bool active { get; }

		public abstract CameraEvent cameraEvent { get; }

		public abstract bool isDebugView { get; }

		public abstract bool isExtraBlend { get; }

		// CharacterMask の描画 (OnPreCull 契機) が必要なモデルは true を返す。
		// active は前フレームの集計に依存するため、設定値から直接判定する実装にすること
		public virtual bool needsCharacterMask
		{
			get
			{
				return false;
			}
		}

		// 頭部マスク RT (Hub が CommandBuffer で白塗り) が必要なモデルは true を返す。
		// こちらも設定値から直接判定する実装にすること
		public virtual bool needsHeadMask
		{
			get
			{
				return false;
			}
		}

		public void Init(PostEffectContext context)
		{
			this.context = context;
		}

		public abstract void Dispose();

		public abstract void OnPreRender();

		public abstract void Prepare(Material material);

		public static void SetKeyword(Material material, string keyword, bool enable)
		{
			if (enable)
			{
				material.EnableKeyword(keyword);
			}
			else
			{
				material.DisableKeyword(keyword);
			}
		}
	}
}