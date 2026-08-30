using System;
using UnityEngine;
using COM3D2.MotionTimelineEditor;
using PEData = COM3D2.MotionTimelineEditor.PostEffects;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneEditor のタイムラインからポストエフェクトを駆動するための公開 API。
    /// SceneEditor はこのクラスをリフレクションで解決するため、
    /// **メソッド名・引数の個数と型を変えると連携が黙って切れる**。
    /// 値の受け渡しは MTEUtils の共有 DTO で行い、実体型との相互コピーはここで閉じる。
    /// 再生中は毎フレーム呼ばれるため、XML を挟まず設定値を直接読み書きする。
    /// 対象はタイムライン対応 5 系統 (DoF / GTToneMap / パラフィン / 距離フォグ / リムライト) のみ
    /// </summary>
    public static class TimelineBridge
    {
        private static EffectSettings settings => EffectSettings.instance;

        // 各系統の上限。実体側のシェーダーバッファ上限と同値
        public static int GetMaxParaffinCount()
        {
            return ColorParaffinEffectModel.MAX_PARAFFIN_COUNT;
        }

        public static int GetMaxDistanceFogCount()
        {
            return DistanceFogEffectModel.MAX_FOG_COUNT;
        }

        public static int GetMaxRimlightCount()
        {
            return RimlightEffectModel.MAX_RIMLIGHT_COUNT;
        }

        public static int GetParaffinCount()
        {
            return settings.paraffin.GetDataCount();
        }

        /// <summary>パラフィンのデータ数。上限で丸めて 1 件ずつ増減する</summary>
        public static void SetParaffinCount(int value)
        {
            ResizeData(
                settings.paraffin.GetDataCount(),
                Mathf.Clamp(value, 0, GetMaxParaffinCount()),
                () => settings.paraffin.AddData(new ColorParaffinData()),
                () => settings.paraffin.RemoveDataLast());
        }

        public static bool GetParaffinEnabled()
        {
            return settings.paraffin.enabled;
        }

        public static void SetParaffinEnabled(bool value)
        {
            settings.paraffin.enabled = value;
            settings.dirty = true;
        }

        public static PEData.ParaffinData GetParaffinData(int index)
        {
            var dto = new PEData.ParaffinData();
            ReflectionFieldCopier.Copy(settings.paraffin.GetData(index), dto);
            return dto;
        }

        public static void ApplyParaffin(int index, PEData.ParaffinData data)
        {
            // 個別データが有効なら系統ごと有効化する (SceneEditor 旧実装と同じ規約)
            if (data.enabled)
            {
                settings.paraffin.enabled = true;
            }
            var native = new ColorParaffinData();
            ReflectionFieldCopier.Copy(data, native);
            settings.paraffin.SetData(index, native);
            settings.dirty = true;
        }

        public static int GetDistanceFogCount()
        {
            return settings.distanceFog.GetDataCount();
        }

        /// <summary>距離フォグのデータ数。上限で丸めて 1 件ずつ増減する</summary>
        public static void SetDistanceFogCount(int value)
        {
            ResizeData(
                settings.distanceFog.GetDataCount(),
                Mathf.Clamp(value, 0, GetMaxDistanceFogCount()),
                () => settings.distanceFog.AddData(new DistanceFogData()),
                () => settings.distanceFog.RemoveDataLast());
        }

        public static bool GetDistanceFogEnabled()
        {
            return settings.distanceFog.enabled;
        }

        public static void SetDistanceFogEnabled(bool value)
        {
            settings.distanceFog.enabled = value;
            settings.dirty = true;
        }

        public static PEData.DistanceFogData GetDistanceFogData(int index)
        {
            var dto = new PEData.DistanceFogData();
            ReflectionFieldCopier.Copy(settings.distanceFog.GetData(index), dto);
            return dto;
        }

        public static void ApplyDistanceFog(int index, PEData.DistanceFogData data)
        {
            if (data.enabled)
            {
                settings.distanceFog.enabled = true;
            }
            var native = new DistanceFogData();
            ReflectionFieldCopier.Copy(data, native);
            settings.distanceFog.SetData(index, native);
            settings.dirty = true;
        }

        public static int GetRimlightCount()
        {
            return settings.rimlight.GetDataCount();
        }

        /// <summary>リムライトのデータ数。上限で丸めて 1 件ずつ増減する</summary>
        public static void SetRimlightCount(int value)
        {
            ResizeData(
                settings.rimlight.GetDataCount(),
                Mathf.Clamp(value, 0, GetMaxRimlightCount()),
                () => settings.rimlight.AddData(new RimlightData()),
                () => settings.rimlight.RemoveDataLast());
        }

        public static bool GetRimlightEnabled()
        {
            return settings.rimlight.enabled;
        }

        public static void SetRimlightEnabled(bool value)
        {
            settings.rimlight.enabled = value;
            settings.dirty = true;
        }

        public static PEData.RimlightData GetRimlightData(int index)
        {
            var dto = new PEData.RimlightData();
            ReflectionFieldCopier.Copy(settings.rimlight.GetData(index), dto);
            return dto;
        }

        public static void ApplyRimlight(int index, PEData.RimlightData data)
        {
            if (data.enabled)
            {
                settings.rimlight.enabled = true;
            }
            var native = new RimlightData();
            ReflectionFieldCopier.Copy(data, native);
            settings.rimlight.SetData(index, native);
            settings.dirty = true;
        }

        public static PEData.GTToneMapData GetGTToneMap()
        {
            var dto = new PEData.GTToneMapData();
            ReflectionFieldCopier.Copy(settings.gtToneMap, dto);
            return dto;
        }

        public static void ApplyGTToneMap(PEData.GTToneMapData data)
        {
            // 実体は差し替えず、既存インスタンスへ写す
            // (他のコントローラが同じ参照を握っているため)
            ReflectionFieldCopier.Copy(data, settings.gtToneMap);
            settings.dirty = true;
        }

        public static PEData.DepthOfFieldData GetDepthOfField()
        {
            var dto = new PEData.DepthOfFieldData();
            ReflectionFieldCopier.Copy(settings.depthOfField, dto);
            return dto;
        }

        public static void ApplyDepthOfField(PEData.DepthOfFieldData data)
        {
            // DTO は DX11 ボケ等の項目を持たないため、写らない項目は実体側の値が残る
            ReflectionFieldCopier.Copy(data, settings.depthOfField);
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
