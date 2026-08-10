using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// キャラ除外用マスクの共有プロバイダ。サブカメラで Charactor レイヤーを白塗り描画した
    /// マスク RT を、フレーム内で複数エフェクトが使い回す (最初の要求時だけ描画する)。
    /// 要求が途絶えたら PostEffectManager.LateUpdate から呼ばれる Tick でリソースを解放する
    /// </summary>
    public static class CharacterMask
    {
        private static Camera _maskCamera;
        private static RenderTexture _maskRT;
        private static Material _compositeMaterial;
        private static int _renderedFrame = -1;
        private static int _lastRequestFrame = -1;

        // メイドは Charactor レイヤーに配置される。名前解決できない場合は既知の 10 へ落とす
        private static readonly int characterLayerMask = ResolveCharacterLayerMask();

        private static int ResolveCharacterLayerMask()
        {
            var layer = LayerMask.NameToLayer("Charactor");
            return 1 << (layer >= 0 ? layer : 10);
        }

        // マスクのような単チャンネル RT に使うフォーマット (AO の遮蔽 RT とも共用)
        public static RenderTextureFormat preferredR8Format =>
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.Default;

        // 2 チャンネルマスク RT (頭部マスクの R=顔 / G=髪 塗り分け) に使うフォーマット
        public static RenderTextureFormat preferredRGFormat =>
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RG16)
                ? RenderTextureFormat.RG16
                : RenderTextureFormat.Default;

        // このフレームで描画済みのマスク。未描画 (要求無し・シェーダー欠落) なら null
        public static RenderTexture texture =>
            _renderedFrame == Time.frameCount ? _maskRT : null;

        /// <summary>
        /// マスクを要求し、フレーム内で未描画なら描画する。
        /// OnRenderImage 中の Camera.Render は非サポートのため、各エフェクトの OnPreCull から呼ぶこと
        /// </summary>
        public static void Render(Camera targetCamera)
        {
            if (targetCamera == null)
            {
                return;
            }

            _lastRequestFrame = Time.frameCount;
            if (_renderedFrame == Time.frameCount)
            {
                return;
            }

            var maskShader = EffectShaders.GetShader(EffectShaders.PostEffects, "CharMaskWhite");
            if (maskShader == null)
            {
                return;
            }

            EnsureResources(targetCamera);
            RenderMask(targetCamera, maskShader);
            _renderedFrame = Time.frameCount;
        }

        /// <summary>
        /// エフェクト適用結果 (effected) と元画像 (original) をマスクで合成して destination へ出力する。
        /// spread はマスク境界の膨張幅 (px)。ブラー系でキャラ輪郭に背景ボケが回り込むのを防ぐ。
        /// マスクや合成シェーダーが無いときは false を返す (呼び出し側で素通しすること)
        /// </summary>
        public static bool Composite(RenderTexture effected, RenderTexture original,
            RenderTexture destination, float spread = 0f)
        {
            var mask = texture;
            if (mask == null)
            {
                return false;
            }

            if (_compositeMaterial == null)
            {
                var shader = EffectShaders.GetShader(EffectShaders.PostEffects, "CharMaskComposite");
                if (shader == null)
                {
                    return false;
                }
                _compositeMaterial = new Material(shader);
                _compositeMaterial.hideFlags = HideFlags.DontSave;
            }

            _compositeMaterial.SetTexture("_SrcTex", original);
            _compositeMaterial.SetTexture("_MaskTex", mask);
            _compositeMaterial.SetFloat("_Spread", spread);
            Graphics.Blit(effected, destination, _compositeMaterial, spread > 0f ? 1 : 0);
            _compositeMaterial.SetTexture("_SrcTex", null);
            _compositeMaterial.SetTexture("_MaskTex", null);
            return true;
        }

        // 要求が途絶えたフレームでリソースを解放する。PostEffectManager.LateUpdate から毎フレーム呼ぶ
        public static void Tick()
        {
            if (_maskRT != null && Time.frameCount - _lastRequestFrame > 1)
            {
                Release();
            }
        }

        public static void Release()
        {
            if (_compositeMaterial != null)
            {
                Object.DestroyImmediate(_compositeMaterial);
                _compositeMaterial = null;
            }
            if (_maskCamera != null)
            {
                Object.DestroyImmediate(_maskCamera.gameObject);
                _maskCamera = null;
            }
            if (_maskRT != null)
            {
                _maskRT.Release();
                Object.DestroyImmediate(_maskRT);
                _maskRT = null;
            }
            _renderedFrame = -1;
        }

        private static void EnsureResources(Camera targetCamera)
        {
            // ダウンサンプリング等と無関係なカメラの実描画解像度に合わせる
            var width = targetCamera.pixelWidth;
            var height = targetCamera.pixelHeight;
            if (_maskRT == null || _maskRT.width != width || _maskRT.height != height)
            {
                if (_maskRT != null)
                {
                    _maskRT.Release();
                    Object.DestroyImmediate(_maskRT);
                }
                // 深度 24bit はキャラ描画時の前後判定 (ZTest) 用。利用側 RT と色空間を揃えるため Linear 明示
                _maskRT = new RenderTexture(width, height, 24, preferredR8Format, RenderTextureReadWrite.Linear);
                _maskRT.name = "PostEffectsCharMask";
                _maskRT.hideFlags = HideFlags.DontSave;
            }

            if (_maskCamera == null)
            {
                var go = new GameObject("PostEffectsCharMaskCamera");
                go.hideFlags = HideFlags.HideAndDontSave;
                _maskCamera = go.AddComponent<Camera>();
                _maskCamera.enabled = false;
            }
        }

        private static void RenderMask(Camera targetCamera, Shader maskShader)
        {
            // メインカメラの視点・投影に毎フレーム追従させる
            _maskCamera.CopyFrom(targetCamera);
            _maskCamera.enabled = false;
            _maskCamera.clearFlags = CameraClearFlags.SolidColor;
            _maskCamera.backgroundColor = Color.black;
            _maskCamera.cullingMask = characterLayerMask;
            _maskCamera.depthTextureMode = DepthTextureMode.None;
            _maskCamera.renderingPath = RenderingPath.Forward;
            _maskCamera.allowMSAA = false;
            _maskCamera.allowHDR = false;
            _maskCamera.useOcclusionCulling = false;
            _maskCamera.targetTexture = _maskRT;
            // 置き換えタグを空にして全マテリアルを白塗りで描く
            _maskCamera.RenderWithShader(maskShader, "");
            _maskCamera.targetTexture = null;
        }
    }

    /// <summary>
    /// キャラ除外に対応した画面エフェクトの基底クラス。
    /// excludeCharacters が有効なとき、エフェクト適用結果と元画像をキャラマスクで合成する
    /// </summary>
    public abstract class CharacterMaskableEffect : MonoBehaviour
    {
        // キャラ (Charactor レイヤー) にエフェクトを適用しない
        public bool excludeCharacters = false;

        // マスク境界の膨張幅 (px)。ブラー系エフェクトはキャラ輪郭への背景ボケ回り込み対策で正値にする
        protected virtual float maskSpread => 0f;

        // エフェクト本体の描画を実装する
        protected abstract void RenderEffect(RenderTexture source, RenderTexture destination);

        private void OnPreCull()
        {
            if (excludeCharacters)
            {
                CharacterMask.Render(GetComponent<Camera>());
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!excludeCharacters)
            {
                RenderEffect(source, destination);
                return;
            }

            var effected = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
            RenderEffect(source, effected);
            if (!CharacterMask.Composite(effected, source, destination, maskSpread))
            {
                // マスク未描画 (シェーダー欠落等) 時はエフェクトをそのまま出す
                Graphics.Blit(effected, destination);
            }
            RenderTexture.ReleaseTemporary(effected);
        }
    }
}
