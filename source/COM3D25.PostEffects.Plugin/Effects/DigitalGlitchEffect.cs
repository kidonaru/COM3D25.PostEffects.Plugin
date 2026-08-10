using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// デジタル映像風グリッチ (Kino/Glitch)。2.5 のゲームアセンブリに型が存在しないため、
    /// SceneCapture 同梱実装を自己完結な形で移植したもの (シェーダーは kino バンドル)
    /// </summary>
    public class DigitalGlitchEffect : MonoBehaviour
    {
        private const int NoiseTextureWidth = 64;
        private const int NoiseTextureHeight = 32;

        public Shader shader;

        [Range(0f, 1f)]
        public float intensity = 0f;

        private Material _material;
        private Texture2D _noiseTexture;
        // 過去フレームを溜めておき、グリッチ時に現フレームへ差し込む素材にする
        private RenderTexture _trashFrame1;
        private RenderTexture _trashFrame2;

        private void OnDisable()
        {
            ReleaseResources();
        }

        private void Update()
        {
            // intensity が高いほど頻繁にノイズパターンを更新する
            if (Random.value > Mathf.Lerp(0.9f, 0.5f, intensity) && SetUpResources())
            {
                UpdateNoiseTexture();
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!SetUpResources())
            {
                Graphics.Blit(source, destination);
                return;
            }

            var frameCount = Time.frameCount;
            if (frameCount % 13 == 0)
            {
                Graphics.Blit(source, _trashFrame1);
            }
            if (frameCount % 73 == 0)
            {
                Graphics.Blit(source, _trashFrame2);
            }

            _material.SetFloat("_Intensity", intensity);
            _material.SetTexture("_NoiseTex", _noiseTexture);
            _material.SetTexture("_TrashTex", Random.value > 0.5f ? _trashFrame1 : _trashFrame2);
            Graphics.Blit(source, destination, _material);
        }

        private bool SetUpResources()
        {
            if (shader == null || !shader.isSupported)
            {
                return false;
            }

            if (_material == null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            if (_noiseTexture == null)
            {
                _noiseTexture = new Texture2D(NoiseTextureWidth, NoiseTextureHeight, TextureFormat.ARGB32, false);
                _noiseTexture.hideFlags = HideFlags.DontSave;
                _noiseTexture.wrapMode = TextureWrapMode.Clamp;
                _noiseTexture.filterMode = FilterMode.Point;
                UpdateNoiseTexture();
            }

            // 解像度変更後もフルスクリーンのまま差し込めるよう、サイズが変わったら作り直す
            _trashFrame1 = GetOrCreateTrashFrame(_trashFrame1);
            _trashFrame2 = GetOrCreateTrashFrame(_trashFrame2);
            return true;
        }

        private static RenderTexture GetOrCreateTrashFrame(RenderTexture current)
        {
            if (current != null && current.width == Screen.width && current.height == Screen.height)
            {
                return current;
            }

            if (current != null)
            {
                DestroyImmediate(current);
            }

            var frame = new RenderTexture(Screen.width, Screen.height, 0);
            frame.hideFlags = HideFlags.DontSave;
            return frame;
        }

        private void UpdateNoiseTexture()
        {
            var color = RandomColor();
            for (var y = 0; y < _noiseTexture.height; y++)
            {
                for (var x = 0; x < _noiseTexture.width; x++)
                {
                    // まれに色を切り替えることで横方向に伸びたブロックノイズになる
                    if (Random.value > 0.89f)
                    {
                        color = RandomColor();
                    }
                    _noiseTexture.SetPixel(x, y, color);
                }
            }
            _noiseTexture.Apply();
        }

        private static Color RandomColor()
        {
            return new Color(Random.value, Random.value, Random.value, Random.value);
        }

        private void ReleaseResources()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
            if (_noiseTexture != null)
            {
                DestroyImmediate(_noiseTexture);
                _noiseTexture = null;
            }
            if (_trashFrame1 != null)
            {
                DestroyImmediate(_trashFrame1);
                _trashFrame1 = null;
            }
            if (_trashFrame2 != null)
            {
                DestroyImmediate(_trashFrame2);
                _trashFrame2 = null;
            }
        }
    }
}
