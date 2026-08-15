using COM3D2.MotionTimelineEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 他の Manager と違い ManagerBase ではなく MTEUtils の WindowManagerBase を継承する。
    /// ウィンドウ管理の実装を EditorWindow プラグインと共有するための例外
    /// </summary>
    public class WindowManager : WindowManagerBase
    {
        public MainWindow mainWindow = null;

        private bool _isCameraControlDisabled = false;
        private bool _isUIInputDisabled = false;
        private bool _isGizmoLocked = false;
        private bool _isCameraPressInProgress = false;
        private bool _isCameraDragFromOutside = false;

        private static WindowManager _instance = null;
        public static WindowManager instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new WindowManager();
                }
                return _instance;
            }
        }

        private WindowManager()
        {
        }

        public override void Init()
        {
            base.Init();

            mainWindow = new MainWindow();
            AddWindow(mainWindow);

            // カラー編集・カーブ編集・画像選択ウィンドウはメインウィンドウの描画結果を参照するため後に登録する
            AddWindow(ColorPickerWindow.instance);
            AddWindow(CurveEditorWindow.instance);
            AddWindow(TexturePickerWindow.instance);

            // ComboBoxPopupWindow はホストの描画中に開閉が確定するため、
            // コンボボックスを持つウィンドウより後に登録すること (同フレームで描画させる)
            AddWindow(ComboBoxPopupWindow.instance);
        }

        protected override void OnAfterUpdate()
        {
            UpdateInputBlock();
        }

        /// <summary>
        /// ウィンドウ上にカーソルがある間はゲーム側のマウス入力を止める。
        /// 止めないと右クリックや左ドラッグでカメラが動き、
        /// またウィンドウ裏に隠れたゲーム UI のボタンまで押されてしまう
        /// </summary>
        private void UpdateInputBlock()
        {
            var isMouseOverWindow = false;
            foreach (var window in windows)
            {
                if (window.isShowWnd && MTEUtils.IsMouseOverWindowRect(window.windowRect))
                {
                    isMouseOverWindow = true;
                    break;
                }
            }

            UpdateCameraDragFromOutside(isMouseOverWindow);

            // ウィンドウ外から始まったドラッグを逃がすのはカメラ操作だけ。
            // UI 入力とギズモは誤操作防止を優先し、カーソルがウィンドウに乗った時点で従来どおり塞ぐ
            UpdateCameraControl(isMouseOverWindow && !_isCameraDragFromOutside);
            UpdateUIInput(isMouseOverWindow);
            UpdateGizmoLock(GUIUtility.hotControl != 0
                || (isMouseOverWindow && Input.GetMouseButton(0)));
        }

        /// <summary>
        /// ウィンドウ外で押し始めたドラッグは、途中でカーソルがウィンドウ内へ入ってもカメラ操作を続けさせる。
        /// 判定は押下フレームのカーソル位置だけで行い、以降はボタンをすべて離すまで維持する
        /// </summary>
        private void UpdateCameraDragFromOutside(bool isMouseOverWindow)
        {
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
            {
                _isCameraPressInProgress = false;
                _isCameraDragFromOutside = false;
                return;
            }

            if (_isCameraPressInProgress)
            {
                return;
            }
            _isCameraPressInProgress = true;

            _isCameraDragFromOutside = !isMouseOverWindow;
        }

        private void UpdateCameraControl(bool shouldBlock)
        {
            var mainCamera = GameMain.Instance.MainCamera;
            if (mainCamera == null)
            {
                return;
            }

            if (shouldBlock)
            {
                // 自分が無効化する前から無効なら他プラグイン等の管理下なので触らない（復帰時に誤って有効化しないため）。
                // 無効化後に外部から有効へ戻された場合は毎フレーム無効化し直す
                if (_isCameraControlDisabled || mainCamera.GetControl())
                {
                    mainCamera.SetControl(false);
                    _isCameraControlDisabled = true;
                }
            }
            else if (_isCameraControlDisabled)
            {
                mainCamera.SetControl(true);
                _isCameraControlDisabled = false;
            }
        }

        /// <summary>
        /// ゲーム UI（NGUI）のイベント処理を止める。
        /// UICamera.InputEnable はゲーム本体もフェード中の入力遮断に使う共有フラグなので、
        /// カメラ操作と同様に「自分が無効化したときだけ戻す」ガードを入れている
        /// </summary>
        private void UpdateUIInput(bool shouldBlock)
        {
            if (shouldBlock)
            {
                if (_isUIInputDisabled || UICamera.InputEnable)
                {
                    UICamera.InputEnable = false;
                    _isUIInputDisabled = true;
                }
            }
            else if (_isUIInputDisabled)
            {
                UICamera.InputEnable = true;
                _isUIInputDisabled = false;
            }
        }

        /// <summary>
        /// IMGUI が何らかのコントロールでマウスを掴んでいる間はギズモのハンドル選択を止める。
        /// global_control_lock はゲーム本体も使う共有フラグなので、
        /// 他と同様に「自分が立てたときだけ倒す」ガードを入れている
        /// </summary>
        private void UpdateGizmoLock(bool shouldLock)
        {
            if (shouldLock)
            {
                if (_isGizmoLocked || !GizmoRender.global_control_lock)
                {
                    GizmoRender.global_control_lock = true;
                    _isGizmoLocked = true;
                }
            }
            else if (_isGizmoLocked)
            {
                GizmoRender.global_control_lock = false;
                _isGizmoLocked = false;
            }
        }

        private void RestoreInputBlock()
        {
            if (_isCameraControlDisabled)
            {
                _isCameraControlDisabled = false;

                var mainCamera = GameMain.Instance.MainCamera;
                if (mainCamera != null)
                {
                    mainCamera.SetControl(true);
                }
            }

            if (_isUIInputDisabled)
            {
                _isUIInputDisabled = false;
                UICamera.InputEnable = true;
            }

            if (_isGizmoLocked)
            {
                _isGizmoLocked = false;
                GizmoRender.global_control_lock = false;
            }

            _isCameraPressInProgress = false;
            _isCameraDragFromOutside = false;
        }

        protected override void OnBeforeCloseWindows()
        {
            RestoreInputBlock();
        }

        public override void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
            RestoreInputBlock();

            base.OnChangedSceneLevel(scene, sceneMode);
        }
    }
}
