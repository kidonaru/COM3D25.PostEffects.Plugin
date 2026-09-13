using System;
using UnityEngine;
using COM3D2.MotionTimelineEditor;
using PEData = COM3D2.MotionTimelineEditor.PostEffects;
// ブルームの enum を実体型へ戻すために BloomController と同じエイリアスを張る
#if COM3D25
using BloomEffect = PostEffects_Dummy.Bloom;
#else
using BloomEffect = global::Bloom;
#endif

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// SceneEditor のタイムラインからポストエフェクトを駆動するための公開 API。
    /// SceneEditor はこのクラスをリフレクションで解決するため、
    /// **メソッド名・引数の個数と型を変えると連携が黙って切れる**。
    /// 値の受け渡しは MTEUtils の共有 DTO で行い、実体型との相互コピーはここで閉じる。
    /// 再生中は毎フレーム呼ばれるため、XML を挟まず設定値を直接読み書きする。
    /// 対象はタイムライン対応 6 系統
    /// (DoF / GTToneMap / パラフィン / 距離フォグ / リムライト / ブルーム) のみ
    /// </summary>
    public static class TimelineBridge
    {
        private static EffectSettings settings => EffectSettings.instance;

        /// <summary>
        /// メインウィンドウをタイムラインタブへ切り替える。
        /// SceneEditor がタイムラインを読込・新規作成したタイミングで呼ばれる
        /// </summary>
        public static void ShowTimelineMode()
        {
            var window = WindowManager.instance.mainWindow;
            if (window == null)
            {
                return;
            }
            window.ShowTimelineMode();
        }

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

        public static PEData.BloomData GetBloom()
        {
            var setting = settings.bloom;
            var dto = new PEData.BloomData();
            ReflectionFieldCopier.Copy(setting, dto);
            CopyBloomSpecialFieldsToDto(setting, dto);
            return dto;
        }

        public static void ApplyBloom(PEData.BloomData data)
        {
            // 実体は差し替えず、既存インスタンスへ写す
            // (他のコントローラが同じ参照を握っているため)
            var setting = settings.bloom;
            ReflectionFieldCopier.Copy(data, setting);
            CopyBloomSpecialFieldsToNative(data, setting);
            // レンズフレアのブラー反復回数はループ回数に直結する。
            // 内蔵ブルームは自前で丸めてくれないため、GUI と同じ範囲へ寄せる
            // (bloomBlurIterations は内蔵側が 1〜10 に自己クランプするので触らない)
            setting.hollywoodFlareBlurIterations = Mathf.Clamp(
                setting.hollywoodFlareBlurIterations,
                BloomSetting.MIN_HOLLYWOOD_FLARE_BLUR_ITERATIONS,
                BloomSetting.MAX_HOLLYWOOD_FLARE_BLUR_ITERATIONS);
            settings.dirty = true;
        }

        /// <summary>
        /// ブルームのうち ReflectionFieldCopier では写らない項目を実体 → DTO へ写す。
        /// enum は int と代入互換が無く、分離設定はネストクラスで型が食い違うため、
        /// どちらも黙って既定値のまま素通りしてしまう
        /// </summary>
        private static void CopyBloomSpecialFieldsToDto(BloomSetting src, PEData.BloomData dst)
        {
            dst.hdr = (int)src.hdr;
            dst.screenBlendMode = (int)src.screenBlendMode;
            dst.lensFlareMode = (int)src.lensFlareMode;

            var nativeSeparation = src.separation;
            dst.separationEnabled = nativeSeparation.enabled;
            dst.separationCharactersEnabled = nativeSeparation.charactersEnabled;
            dst.separationBackgroundEnabled = nativeSeparation.backgroundEnabled;
            dst.separationCharacterIntensity = nativeSeparation.characterIntensity;
            dst.separationCharacterThreshold = nativeSeparation.characterThreshold;
            dst.separationCharacterRadius = nativeSeparation.characterRadius;
        }

        /// <summary><see cref="CopyBloomSpecialFieldsToDto"/> の逆方向</summary>
        private static void CopyBloomSpecialFieldsToNative(PEData.BloomData src, BloomSetting dst)
        {
            // タイムライン XML の手編集やバージョン差で定義域外の値が来ても
            // そのままキャストすると未定義の enum 値になるため、定義域へ丸める
            dst.hdr = (BloomEffect.HDRBloomMode)ClampEnumValue(src.hdr, _maxHdrMode);
            dst.screenBlendMode = (BloomEffect.BloomScreenBlendMode)ClampEnumValue(
                src.screenBlendMode, _maxScreenBlendMode);
            dst.lensFlareMode = (BloomEffect.LensFlareStyle)ClampEnumValue(
                src.lensFlareMode, _maxLensFlareMode);

            var nativeSeparation = dst.separation;
            nativeSeparation.enabled = src.separationEnabled;
            nativeSeparation.charactersEnabled = src.separationCharactersEnabled;
            nativeSeparation.backgroundEnabled = src.separationBackgroundEnabled;
            nativeSeparation.characterIntensity = src.separationCharacterIntensity;
            nativeSeparation.characterThreshold = src.separationCharacterThreshold;
            nativeSeparation.characterRadius = src.separationCharacterRadius;
        }

        // ブルームの enum の上限値。毎フレーム通る経路なので、
        // その都度アロケートする Enum.IsDefined ではなく初回に求めた値で丸める
        private static readonly int _maxHdrMode = GetMaxEnumValue(typeof(BloomEffect.HDRBloomMode));
        private static readonly int _maxScreenBlendMode = GetMaxEnumValue(typeof(BloomEffect.BloomScreenBlendMode));
        private static readonly int _maxLensFlareMode = GetMaxEnumValue(typeof(BloomEffect.LensFlareStyle));

        /// <summary>0 始まりの連番 enum の最大値。ブルームの enum は 3 種ともこの形</summary>
        private static int GetMaxEnumValue(Type enumType)
        {
            return Enum.GetValues(enumType).Length - 1;
        }

        private static int ClampEnumValue(int value, int max)
        {
            return Mathf.Clamp(value, 0, max);
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
