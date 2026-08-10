using System.Xml.Serialization;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 環境遮蔽 (SSAO) (Kino/Obscurance)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)。
    /// 移植元の ambientOnly (G-Buffer 合成) は移植元でも機能しておらず、未移植
    /// </summary>
    public class ObscuranceEffect : MonoBehaviour
    {
        // XmlSerializer は入れ子型も外側の型名を含まない XML 型名で扱うため、一意な名前を明示する
        [XmlType("ObscuranceSampleCount")]
        public enum SampleCount
        {
            Lowest,
            Low,
            Medium,
            High,
            Variable,
        }

        [XmlType("ObscuranceOcclusionSource")]
        public enum OcclusionSource
        {
            DepthTexture,
            DepthNormalsTexture,
            GBuffer,
        }

        public Shader shader;

        public float intensity = 0.5f;
        public Color tint = Color.gray;
        public float radius = 0.1f;
        public SampleCount sampleCount = SampleCount.Medium;
        // sampleCount が Variable のときだけ使うサンプル数
        public int variableSampleCount = 24;
        public bool downsampling = false;
        public OcclusionSource occlusionSource = OcclusionSource.DepthTexture;
        // 遮蔽を単色ではなく 2 色のグラデーションで塗る
        public bool colorMode = false;
        public Color subTint = Color.gray;
        public Color subTint2 = Color.gray;
        public float blend = 0f;
        // キャラ (Charactor レイヤー) に AO を適用しない。
        // キャラマスクをサブカメラで描き、ぼかし後の遮蔽 RT から消し込む方式のため、
        // キャラが背景へ AO を落とす挙動は維持される (docs/scenecapture-ui-diff.md §5.1)
        public bool excludeCharacters = true;

        private Material _material;
        private Material _maskEraseMaterial;

        private Camera targetCamera => GetComponent<Camera>();

        private RenderTextureFormat aoTextureFormat => CharacterMask.preferredR8Format;

        // G-Buffer はディファードレンダリング時しか使えないので、その場合は深度＋法線へ落とす
        private OcclusionSource effectiveOcclusionSource =>
            occlusionSource == OcclusionSource.GBuffer &&
            targetCamera.actualRenderingPath != RenderingPath.DeferredShading
                ? OcclusionSource.DepthNormalsTexture
                : occlusionSource;

        private int effectiveSampleCount
        {
            get
            {
                switch (sampleCount)
                {
                    case SampleCount.Lowest: return 3;
                    case SampleCount.Low: return 6;
                    case SampleCount.Medium: return 12;
                    case SampleCount.High: return 20;
                    default: return Mathf.Clamp(variableSampleCount, 1, 256);
                }
            }
        }

        private void OnEnable()
        {
            UpdateDepthTextureMode();
        }

        // occlusionSource に応じて必要な深度テクスチャを要求する。
        // 設定は実行中に切り替わるため描画時にも呼び直す
        private void UpdateDepthTextureMode()
        {
            var source = effectiveOcclusionSource;
            if (source == OcclusionSource.DepthTexture)
            {
                targetCamera.depthTextureMode |= DepthTextureMode.Depth;
            }
            if (source != OcclusionSource.GBuffer)
            {
                targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
            }
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
            if (_maskEraseMaterial != null)
            {
                DestroyImmediate(_maskEraseMaterial);
                _maskEraseMaterial = null;
            }
        }

        // マスクは CharacterMask (共有プロバイダ) が描く。
        // OnRenderImage の最中に Camera.Render を呼ぶのは非サポートのためここで要求する
        private void OnPreCull()
        {
            if (excludeCharacters)
            {
                CharacterMask.Render(targetCamera);
            }
        }

        // キャラマスクの立っている画素の遮蔽量を 0 にした RT を返す。失敗時は入力をそのまま返す
        private RenderTexture ApplyCharacterMask(RenderTexture occlusionRT)
        {
            var maskRT = CharacterMask.texture;
            if (maskRT == null)
            {
                return occlusionRT;
            }

            if (_maskEraseMaterial == null)
            {
                var eraseShader = EffectShaders.GetShader(EffectShaders.PostEffects, "ObscuranceMask");
                if (eraseShader == null)
                {
                    return occlusionRT;
                }
                _maskEraseMaterial = new Material(eraseShader);
                _maskEraseMaterial.hideFlags = HideFlags.DontSave;
            }

            var maskedRT = RenderTexture.GetTemporary(
                occlusionRT.width, occlusionRT.height, 0, aoTextureFormat, RenderTextureReadWrite.Linear);
            _maskEraseMaterial.SetTexture("_MaskTex", maskRT);
            Graphics.Blit(occlusionRT, maskedRT, _maskEraseMaterial);
            RenderTexture.ReleaseTemporary(occlusionRT);
            return maskedRT;
        }

        private void UpdateMaterialProperties()
        {
            _material.SetFloat("_Intensity", intensity);
            _material.SetColor("_Color", tint);
            _material.SetColor("_Color2", subTint);
            _material.SetColor("_Color3", subTint2);
            _material.SetFloat("_Blend", blend);
            _material.SetFloat("_Radius", radius);
            _material.SetFloat("_Downsample", downsampling ? 0.5f : 1f);
            _material.SetInt("_SampleCount", effectiveSampleCount);
        }

        // 不透明部だけを対象にするため ImageEffectOpaque を付ける (移植元と同じ)
        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (shader == null || !shader.isSupported)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            UpdateDepthTextureMode();
            UpdateMaterialProperties();

            var occlusion = effectiveOcclusionSource;
            var format = aoTextureFormat;
            var readWrite = RenderTextureReadWrite.Linear;
            var scale = downsampling ? 2 : 1;

            // 遮蔽項を生成 → 横ぼかし → 縦ぼかし → 元画像へ合成
            var occlusionRT = RenderTexture.GetTemporary(
                source.width / scale, source.height / scale, 0, format, readWrite);
            Graphics.Blit(source, occlusionRT, _material, (int)occlusion);

            var blurRT = RenderTexture.GetTemporary(source.width, source.height, 0, format, readWrite);
            Graphics.Blit(occlusionRT, blurRT, _material, occlusion == OcclusionSource.GBuffer ? 4 : 3);
            RenderTexture.ReleaseTemporary(occlusionRT);

            occlusionRT = RenderTexture.GetTemporary(source.width, source.height, 0, format, readWrite);
            Graphics.Blit(blurRT, occlusionRT, _material, 5);
            RenderTexture.ReleaseTemporary(blurRT);

            if (excludeCharacters)
            {
                occlusionRT = ApplyCharacterMask(occlusionRT);
            }

            _material.SetTexture("_OcclusionTexture", occlusionRT);
            Graphics.Blit(source, destination, _material, colorMode ? 8 : 6);
            RenderTexture.ReleaseTemporary(occlusionRT);
            _material.SetTexture("_OcclusionTexture", null);
        }
    }
}
