using System;
using System.Reflection;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneCapture (COM3D2.SceneCapture.Plugin) 併用時に、
    /// SceneCapture の初期化が終わるまで本プラグインがカメラへ触るのを止めるための判定。
    ///
    /// SceneCapture の Util.GetComponentVar は「対象コンポーネントがカメラに既に居るか」で
    /// 処理を分岐し、居る場合は enabled を強制的に立てたうえで InitMemberByInstance を呼ぶ。
    /// SunShaftsDef ではこれが静的コンストラクタ実行中に呼ばれるため未初期化の static を踏んで落ち、
    /// SceneCapture 全体の初期化 (Instances の生成) が失敗して以後 NRE を吐き続ける。
    /// 本プラグインが PrepareAll でコンポーネントを先置きするとこれを必ず踏むため、
    /// SceneCapture が自分でコンポーネントを追加し終えるまで待つ。
    ///
    /// SceneCapture の Initialize は最初の Update で走るため、待機は通常 1 フレーム未満で解ける。
    /// </summary>
    public static class SceneCaptureCompat
    {
        private const string HostAssemblyName = "COM3D2.SceneCapture.Plugin";
        private const string InstancesTypeName = "CM3D2.SceneCapture.Plugin.Instances";

        // 待機を打ち切るまでの秒数。SceneCapture が別要因で初期化に失敗したときに
        // 本プラグインのエフェクトまで永久に止めないための保険
        private const float WaitTimeoutSeconds = 30f;

        // Instances.effm (EffectManager) の取得元。SceneCapture 不在なら null
        private static PropertyInfo _effmProperty;
        private static bool _resolved;

        // これ以上待つ必要がないと確定したか (不在 / 初期化完了 / タイムアウト)
        private static bool _done;

        // 待機開始時刻。未待機は -1
        private static float _waitStartTime = -1f;

        /// <summary>
        /// カメラへコンポーネントを追加してよいか。
        /// SceneCapture 未導入・互換モード無効・SceneCapture の初期化完了後は true
        /// </summary>
        public static bool CanTouchCamera()
        {
            if (_done || !ConfigManager.instance.config.sceneCaptureCompat)
            {
                return true;
            }

            Resolve();
            if (_effmProperty == null)
            {
                // SceneCapture 不在、または接続できないので待つ理由がない
                _done = true;
                return true;
            }

            bool created;
            if (!TryGetEffectManagerCreated(out created))
            {
                _done = true;
                return true;
            }

            if (created)
            {
                _done = true;
                if (_waitStartTime >= 0f)
                {
                    MTEUtils.Log("SceneCapture の初期化完了を確認しました。エフェクトの適用を開始します");
                }
                return true;
            }

            if (_waitStartTime < 0f)
            {
                _waitStartTime = Time.realtimeSinceStartup;
                // 待つと SceneCapture のコンポーネントが先に並ぶため、PrepareAll で固定している
                // 適用順が崩れて絵が変わる。原因不明の見た目の変化として悩まないよう警告で出す
                MTEUtils.LogWarning(
                    "SceneCapture を検出したため互換モードで動作します。" +
                    "本プラグインのエフェクトが SceneCapture のエフェクトより後段に回るため、" +
                    "単独使用時とは適用順 (見た目) が変わります");
                return false;
            }

            if (Time.realtimeSinceStartup - _waitStartTime > WaitTimeoutSeconds)
            {
                _done = true;
                MTEUtils.LogWarning(
                    "SceneCapture の初期化を {0} 秒待っても確認できませんでした。エフェクトの適用を開始します",
                    WaitTimeoutSeconds);
                return true;
            }

            return false;
        }

        // MTEUtils/*Client.cs は「ホスト型が見つかるまで毎回探し直す」作法だが、ここは一度で確定させる。
        // この判定はカメラへ最初に触る瞬間に効いていなければ意味がなく (先置きしてしまえば手遅れ)、
        // 後から見つけて待ちに入っても手遅れなため。SceneCapture 不在と誤判定したときの挙動は
        // 互換モード無効時と同じ (従来どおりの動作) で、ファイル名順でも
        // COM3D2.SceneCapture.Plugin.dll は本プラグインより先にロードされる
        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;

            var type = FindInstancesType();
            if (type == null)
            {
                return;
            }

            _effmProperty = type.GetProperty("effm", BindingFlags.Public | BindingFlags.Static);
            if (_effmProperty == null)
            {
                MTEUtils.LogWarning(
                    "SceneCapture の Instances.effm が見つかりませんでした。初期化の待機を行いません");
            }
        }

        // 通常は Type.GetType で足りるが、SceneCapture プラグインのロードが自分より後の場合は
        // null を返すため、AppDomain の読み込み済みアセンブリからも探す
        private static Type FindInstancesType()
        {
            var type = Type.GetType(InstancesTypeName + ", " + HostAssemblyName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != HostAssemblyName)
                {
                    continue;
                }
                return assembly.GetType(InstancesTypeName);
            }
            return null;
        }

        // Instances.effm は EffectManager の生成に成功したときだけ代入されるため、
        // 非 null であることが全 *Def の静的コンストラクタを通過した証明になる
        private static bool TryGetEffectManagerCreated(out bool created)
        {
            created = false;
            try
            {
                created = _effmProperty.GetValue(null, null) != null;
                return true;
            }
            catch (Exception e)
            {
                // 取得できない以上は待っても状態が変わらないため、待機自体を諦める
                MTEUtils.LogException(e);
                MTEUtils.LogWarning("SceneCapture の初期化状態を取得できませんでした。待機せずに続行します");
                return false;
            }
        }
    }
}
