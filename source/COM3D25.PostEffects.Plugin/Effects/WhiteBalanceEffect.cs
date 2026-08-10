using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// ホワイトバランス (色温度 / ティント)。LMS 色空間での 1 パス補正 (PPSv2 の White Balance 相当)。
    /// 本プラグイン自前の実装 (シェーダーは posteffects バンドル)
    /// </summary>
    public class WhiteBalanceEffect : MonoBehaviour
    {
        public Shader shader;

        [Range(-100f, 100f)]
        public float temperature = 0f;
        [Range(-100f, 100f)]
        public float tint = 0f;

        private Material _material;

        private void OnDisable()
        {
            if (_material != null)
            {
                DestroyImmediate(_material);
                _material = null;
            }
        }

        // CIE 1960 UCS 上で標準イルミナント軌跡に沿った y を返す (PPSv2 と同じ近似)
        private static float StandardIlluminantY(float x)
        {
            return 2.87f * x - 3f * x * x - 0.27509507f;
        }

        // CIE xy 色度から CAT02 の LMS 錐体応答へ変換する
        private static Vector3 CIExyToLMS(float x, float y)
        {
            var Y = 1f;
            var X = Y * x / y;
            var Z = Y * (1f - x - y) / y;

            var L = 0.7328f * X + 0.4296f * Y - 0.1624f * Z;
            var M = -0.7036f * X + 1.6975f * Y + 0.0061f * Z;
            var S = 0.0030f * X + 0.0136f * Y + 0.9834f * Z;
            return new Vector3(L, M, S);
        }

        // 温度/ティントから D65 基準のホワイトポイント比を求める (PPSv2 ColorUtilities.ComputeColorBalance)
        private static Vector3 ComputeBalance(float temperature, float tint)
        {
            var t1 = temperature / 65f;
            var t2 = tint / 65f;

            var x = 0.31271f - t1 * (t1 < 0f ? 0.1f : 0.05f);
            var y = StandardIlluminantY(x) + t2 * 0.05f;

            var w1 = new Vector3(0.949237f, 1.03542f, 1.08728f); // D65 の LMS
            var w2 = CIExyToLMS(x, y);
            return new Vector3(w1.x / w2.x, w1.y / w2.y, w1.z / w2.z);
        }

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

            _material.SetVector("_Balance", ComputeBalance(temperature, tint));
            Graphics.Blit(source, destination, _material, 0);
        }
    }
}
