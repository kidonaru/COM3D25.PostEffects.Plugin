using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// メイド (Charactor / Face レイヤー) をカメラの描画対象から外す。
    /// 後処理ではなくカリングマスクの操作なので Blit は行わない。
    /// 移植元は無効化時にマスクを -1 (全レイヤー) へ戻していたが、2.5 のメインカメラは
    /// 元から一部レイヤーを外しているため、掴んだ時点のマスクを覚えて戻す
    /// </summary>
    public class MaidHideEffect : MonoBehaviour
    {
        // 名前解決できない場合は既知のレイヤー番号へ落とす (Obscurance のキャラマスクと同じ扱い)
        private static readonly int HiddenLayerMask = ToLayerMask("Charactor", 10) | ToLayerMask("Face", 11);

        private static int ToLayerMask(string layerName, int fallbackLayer)
        {
            var layer = LayerMask.NameToLayer(layerName);
            return 1 << (layer >= 0 ? layer : fallbackLayer);
        }

        private Camera _camera;
        private int _originalCullingMask;
        private bool _captured;

        private Camera targetCamera
        {
            get
            {
                if (_camera == null)
                {
                    _camera = GetComponent<Camera>();
                }
                return _camera;
            }
        }

        private void OnDisable()
        {
            if (_captured && targetCamera != null)
            {
                targetCamera.cullingMask = _originalCullingMask;
            }
            _captured = false;
        }

        // ゲーム側がカリングマスクを書き換えても追従できるよう、毎フレーム該当ビットだけを落とす。
        // 復元用の値は有効化直後のスナップショットで固定する (有効中のゲーム側の変更までは追わない)
        private void OnPreCull()
        {
            var camera = targetCamera;
            if (camera == null)
            {
                return;
            }

            if (!_captured)
            {
                _originalCullingMask = camera.cullingMask;
                _captured = true;
            }
            camera.cullingMask &= ~HiddenLayerMask;
        }
    }
}
