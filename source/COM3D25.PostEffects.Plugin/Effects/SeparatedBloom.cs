using System;
using COM3D2.MotionTimelineEditor;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    // 全画面用の既存設定を背景に使い、キャラの発光量・抽出しきい値・広がりを独立させる。
    public class BloomSeparationSetting
    {
        public bool enabled = false;
        public bool charactersEnabled = true;
        public bool backgroundEnabled = true;
        public float characterIntensity = 2f;
        public float characterThreshold = 1.1f;
        public float characterRadius = 1f;
    }

    public abstract class SeparatedBloomController<TComponent, TSetting> : EffectControllerBase<TComponent, TSetting>
        where TComponent : Behaviour
        where TSetting : class, new()
    {
        protected abstract BloomSeparationSetting separation { get; }
        protected abstract void ApplyCharacterSetting(TComponent component);
        protected abstract void RenderBloom(TComponent component, RenderTexture source, RenderTexture destination);
        private SeparatedBloomEffect _separatedEffect;

        public override void Apply()
        {
            if (!separation.enabled)
            {
                StopSeparation();
                base.Apply();
                return;
            }

            var component = GetOrAddComponent();
            if (component == null) return;
            component.enabled = false;
            if (_separatedEffect == null)
            {
                _separatedEffect = component.gameObject.AddComponent<SeparatedBloomEffect>();
                _separatedEffect.renderRegion = RenderRegion;
            }
            _separatedEffect.enabled = true;
        }

        private void RenderRegion(RenderTexture source, RenderTexture destination, bool character)
        {
            if (!(character ? separation.charactersEnabled : separation.backgroundEnabled))
            {
                Graphics.Blit(source, destination);
                return;
            }
            try
            {
                ApplySetting(_component);
                if (character) ApplyCharacterSetting(_component);
                RenderBloom(_component, source, destination);
            }
            finally
            {
                // ゲーム内蔵エフェクトのリソース検査が有効状態を書き換えても二重描画させない。
                _component.enabled = false;
            }
        }

        private void StopSeparation()
        {
            if (_separatedEffect == null || !_separatedEffect.enabled) return;
            _separatedEffect.enabled = false;
            if (_component != null)
            {
                // 手動描画中に作られたマテリアルも通常の無効化処理で解放する。
                _component.enabled = true;
                _component.enabled = false;
            }
        }

        public override void Restore()
        {
            StopSeparation();
            base.Restore();
        }

        protected void DrawSeparation(GUIView view, float defaultIntensity, float defaultThreshold,
            float defaultRadius, float maxRadius, string radiusLabel)
        {
            var s = separation;
            view.DrawToggle("キャラと背景を別々に設定", s.enabled, 300, 20, value =>
            {
                s.enabled = value;
                SetDirty();
            });
            if (!s.enabled) return;

            view.DrawLabel("キャラ", -1, 20);
            view.DrawToggle("キャラのブルームを有効化", s.charactersEnabled, 300, 20,
                value => { s.charactersEnabled = value; SetDirty(); });
            DrawSlider(view, "キャラ強度", 0f, 10f, defaultIntensity, s.characterIntensity,
                value => s.characterIntensity = value);
            DrawSlider(view, "キャラしきい値", 0f, 3f, defaultThreshold, s.characterThreshold,
                value => s.characterThreshold = value);
            DrawSlider(view, "キャラ" + radiusLabel, 0f, maxRadius, defaultRadius, s.characterRadius,
                value => s.characterRadius = value);
            view.DrawLabel("品質・色・フレアなどは共通設定です", -1, 20);
            view.DrawHorizontalLine(Color.gray);
            view.DrawLabel("背景", -1, 20);
            view.DrawToggle("背景のブルームを有効化", s.backgroundEnabled, 300, 20,
                value => { s.backgroundEnabled = value; SetDirty(); });
        }
    }

    // 各ブルームの描画をマスク済み入力で実行する。合成後にはマスクを掛けず、輪郭外への光を残す。
    public class SeparatedBloomEffect : MonoBehaviour
    {
        public Action<RenderTexture, RenderTexture, bool> renderRegion;
        private Material _material;
        private bool _warned;

        private void OnPreCull()
        {
            CharacterMask.Render(GetComponent<Camera>(), true);
        }

        private void OnDisable()
        {
            if (_material != null) DestroyImmediate(_material);
            _material = null;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var shader = EffectShaders.GetShader(EffectShaders.PostEffects, "SeparatedBloom");
            if (renderRegion == null || CharacterMask.texture == null || shader == null || !shader.isSupported)
            {
                if (!_warned)
                {
                    MTEUtils.LogError("キャラ・背景ブルームのマスクまたはシェーダーが利用できません。シェーダーバンドルを更新してください");
                    _warned = true;
                }
                Graphics.Blit(source, destination);
                return;
            }
            if (_material == null)
                _material = new Material(shader) { hideFlags = HideFlags.DontSave };

            RenderTexture input = null, characters = null, background = null;
            try
            {
                input = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                characters = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                background = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                _material.SetTexture("_MaskTex", CharacterMask.texture);
                _material.SetFloat("_Characters", 1f);
                Graphics.Blit(source, input, _material, 0);
                renderRegion(input, characters, true);

                _material.SetFloat("_Characters", 0f);
                Graphics.Blit(source, input, _material, 0);
                renderRegion(input, background, false);

                // 入力を相補的に分けているため、各結果の和に元画像はちょうど一回だけ含まれる。
                _material.SetTexture("_CharacterTex", characters);
                _material.SetTexture("_BackgroundTex", background);
                Graphics.Blit(source, destination, _material, 1);
                _warned = false;
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    MTEUtils.LogError("キャラ・背景ブルームの描画に失敗しました");
                    MTEUtils.LogException(e);
                    _warned = true;
                }
                Graphics.Blit(source, destination);
            }
            finally
            {
                _material.SetTexture("_MaskTex", null);
                _material.SetTexture("_CharacterTex", null);
                _material.SetTexture("_BackgroundTex", null);
                if (input != null) RenderTexture.ReleaseTemporary(input);
                if (characters != null) RenderTexture.ReleaseTemporary(characters);
                if (background != null) RenderTexture.ReleaseTemporary(background);
            }
        }
    }
}
