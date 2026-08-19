using System.Collections.Generic;
using UnityEngine;

namespace COM3D25.PostEffects.Plugin
{
    /// <summary>
    /// 全メイドの頭部スロットの Renderer を顔系/髪系に分けて収集する共有ヘルパ。
    /// リムライトの顔除外マスク (PostEffectHub が CommandBuffer.DrawRenderer で R=顔 / G=髪 に塗り分け) に使う。
    /// レイヤーには一切触らない (ゲーム側 VR モードの Face レイヤー運用と干渉させないため)
    /// </summary>
    public static class HeadMask
    {
        // 親ボーンが "Bip01 Head" のスロット群を顔系と髪系に分けた固定リスト
        // (TBody.m_strDefSlotName の親ボーン定義に基づく)。
        // 「顔にかけない + 髪には適用」でマスクを分けるため 2 グループで収集する

        // 顔面に付くスロット (顔・目・歯・鼻・めがね・顔アクセ)
        private static readonly TBody.SlotID[] FaceSlots =
        {
            TBody.SlotID.head, TBody.SlotID.eye,
            TBody.SlotID.accHa, TBody.SlotID.accHana, TBody.SlotID.megane,
#if COM3D25
            // COM3D2 (2.0) の TBody.SlotID には存在しないスロット
            TBody.SlotID.accFace,
#endif
        };

        // 髪と頭部装飾のスロット (髪・帽子・カチューシャ・耳/髪アクセ等)
        private static readonly TBody.SlotID[] HairSlots =
        {
            TBody.SlotID.hairF, TBody.SlotID.hairR, TBody.SlotID.hairS,
            TBody.SlotID.hairT, TBody.SlotID.hairAho,
            TBody.SlotID.headset, TBody.SlotID.accHead,
            TBody.SlotID.accHat,
            TBody.SlotID.accMiMiR, TBody.SlotID.accMiMiL,
            TBody.SlotID.accKami_1_, TBody.SlotID.accKami_2_, TBody.SlotID.accKami_3_,
            TBody.SlotID.accKamiSubR, TBody.SlotID.accKamiSubL,
#if COM3D25
            // COM3D2 (2.0) の TBody.SlotID には存在しないスロット
            TBody.SlotID.hairS_2, TBody.SlotID.hairT_2,
            TBody.SlotID.accHead_2, TBody.SlotID.accHat_2,
#endif
        };

        private static readonly List<Renderer> _faceRenderers = new List<Renderer>();
        private static readonly List<Renderer> _hairRenderers = new List<Renderer>();
        private static readonly List<Renderer> _collectTemp = new List<Renderer>();
        private static int _lastCollectFrame = -1;
        // 想定外の例外まで無言で握らないよう、初回だけ警告を出す
        private static bool _slotErrorLogged;

        // 毎フレームの列挙は避け、この間隔でだけ再収集する (装備・メイド数変化の追従は最大この遅延)
        private const int CollectIntervalFrames = 30;

        // キャッシュヒット時はアロケーションなしで同一 List を返す。
        // 破棄済み Renderer (null) が混ざりうるため、呼び出し側で null チェックすること
        public static void CollectRenderers(out List<Renderer> faceRenderers, out List<Renderer> hairRenderers)
        {
            faceRenderers = _faceRenderers;
            hairRenderers = _hairRenderers;

            if (_lastCollectFrame >= 0 && Time.frameCount - _lastCollectFrame < CollectIntervalFrames)
            {
                return;
            }
            _lastCollectFrame = Time.frameCount;
            _faceRenderers.Clear();
            _hairRenderers.Clear();

            var characterMgr = GameMain.Instance != null ? GameMain.Instance.CharacterMgr : null;
            if (characterMgr == null)
            {
                return;
            }

            int maidCount = characterMgr.GetMaidCount();
            for (int i = 0; i < maidCount; i++)
            {
                var maid = characterMgr.GetMaid(i);
                if (maid == null || !maid.Visible || maid.body0 == null)
                {
                    continue;
                }

                CollectSlotRenderers(maid, FaceSlots, _faceRenderers);
                CollectSlotRenderers(maid, HairSlots, _hairRenderers);
            }

            _collectTemp.Clear();
        }

        private static void CollectSlotRenderers(Maid maid, TBody.SlotID[] slots, List<Renderer> results)
        {
            foreach (var slotId in slots)
            {
                TBodySkin slot;
                try
                {
                    slot = maid.body0.GetSlot((int)slotId);
                }
                catch (System.Exception e)
                {
                    // スロット未初期化のボディは無視 (それ以外の異常も継続するが、初回だけ記録を残す)
                    if (!_slotErrorLogged)
                    {
                        _slotErrorLogged = true;
                        COM3D2.MotionTimelineEditor.MTEUtils.LogWarning(
                            "頭部スロットの取得に失敗しました: {0}", e.Message);
                    }
                    continue;
                }

                if (slot == null || !slot.boVisible || slot.obj == null)
                {
                    continue;
                }

                _collectTemp.Clear();
                slot.obj.GetComponentsInChildren(false, _collectTemp);
                results.AddRange(_collectTemp);
            }
        }
    }
}
