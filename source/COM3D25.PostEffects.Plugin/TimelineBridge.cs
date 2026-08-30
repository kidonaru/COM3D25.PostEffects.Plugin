using System;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneEditor のタイムラインからポストエフェクトを駆動するための公開 API。
    /// 再生中は毎フレーム呼ばれるため、XML やリフレクションを挟まず設定値を直接読み書きする。
    /// 対象はタイムライン対応 5 系統 (DoF / GTToneMap / パラフィン / 距離フォグ / リムライト) のみ
    /// </summary>
    public static class TimelineBridge
    {
        // 各系統の上限。実体側のシェーダーバッファ上限と同値で、SceneEditor 側 UI もこれを参照する
        // (参照元の MAX_*_COUNT が static readonly のため const にはできない)
        public static readonly int MaxParaffinCount = ColorParaffinEffectModel.MAX_PARAFFIN_COUNT;
        public static readonly int MaxDistanceFogCount = DistanceFogEffectModel.MAX_FOG_COUNT;
        public static readonly int MaxRimlightCount = RimlightEffectModel.MAX_RIMLIGHT_COUNT;

        private static EffectSettings settings => EffectSettings.instance;

        /// <summary>パラフィンのデータ数。set は上限で丸めて増減する</summary>
        public static int paraffinCount
        {
            get => settings.paraffin.GetDataCount();
            set => ResizeData(
                settings.paraffin.GetDataCount(),
                Mathf.Clamp(value, 0, MaxParaffinCount),
                () => settings.paraffin.AddData(new ColorParaffinData()),
                () => settings.paraffin.RemoveDataLast());
        }

        public static bool paraffinEnabled
        {
            get => settings.paraffin.enabled;
            set { settings.paraffin.enabled = value; settings.dirty = true; }
        }

        public static ColorParaffinData GetParaffinData(int index)
        {
            return settings.paraffin.GetData(index);
        }

        public static void ApplyParaffin(int index, ColorParaffinData data)
        {
            // 個別データが有効なら系統ごと有効化する (SceneEditor 旧実装と同じ規約)
            if (data.enabled)
            {
                settings.paraffin.enabled = true;
            }
            settings.paraffin.SetData(index, data);
            settings.dirty = true;
        }

        /// <summary>距離フォグのデータ数。set は上限で丸めて増減する</summary>
        public static int distanceFogCount
        {
            get => settings.distanceFog.GetDataCount();
            set => ResizeData(
                settings.distanceFog.GetDataCount(),
                Mathf.Clamp(value, 0, MaxDistanceFogCount),
                () => settings.distanceFog.AddData(new DistanceFogData()),
                () => settings.distanceFog.RemoveDataLast());
        }

        public static bool distanceFogEnabled
        {
            get => settings.distanceFog.enabled;
            set { settings.distanceFog.enabled = value; settings.dirty = true; }
        }

        public static DistanceFogData GetDistanceFogData(int index)
        {
            return settings.distanceFog.GetData(index);
        }

        public static void ApplyDistanceFog(int index, DistanceFogData data)
        {
            if (data.enabled)
            {
                settings.distanceFog.enabled = true;
            }
            settings.distanceFog.SetData(index, data);
            settings.dirty = true;
        }

        /// <summary>リムライトのデータ数。set は上限で丸めて増減する</summary>
        public static int rimlightCount
        {
            get => settings.rimlight.GetDataCount();
            set => ResizeData(
                settings.rimlight.GetDataCount(),
                Mathf.Clamp(value, 0, MaxRimlightCount),
                () => settings.rimlight.AddData(new RimlightData()),
                () => settings.rimlight.RemoveDataLast());
        }

        public static bool rimlightEnabled
        {
            get => settings.rimlight.enabled;
            set { settings.rimlight.enabled = value; settings.dirty = true; }
        }

        public static RimlightData GetRimlightData(int index)
        {
            return settings.rimlight.GetData(index);
        }

        public static void ApplyRimlight(int index, RimlightData data)
        {
            if (data.enabled)
            {
                settings.rimlight.enabled = true;
            }
            settings.rimlight.SetData(index, data);
            settings.dirty = true;
        }

        public static GTToneMapSetting GetGTToneMap()
        {
            return settings.gtToneMap;
        }

        public static void ApplyGTToneMap(GTToneMapSetting data)
        {
            settings.gtToneMap = data;
            settings.dirty = true;
        }

        public static DepthOfFieldSetting GetDepthOfField()
        {
            return settings.depthOfField;
        }

        public static void ApplyDepthOfField(DepthOfFieldSetting data)
        {
            settings.depthOfField = data;
            settings.dirty = true;
        }

        /// <summary>データ数を target へ寄せる。増減どちらも 1 件ずつで、書き込み後は dirty を立てる</summary>
        private static void ResizeData(
            int current, int target,
            Action addOne, Action removeLast)
        {
            if (current == target)
            {
                return;
            }
            while (current < target)
            {
                addOne();
                current++;
            }
            while (current > target)
            {
                removeLast();
                current--;
            }
            settings.dirty = true;
        }
    }
}
