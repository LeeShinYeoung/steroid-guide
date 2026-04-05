using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class CraftableUISystem : ModSystem
    {
        internal UserInterface CraftableInterface;
        internal CraftableUIState CraftableState;
        private bool _isVisible;
        private bool _escWasDown;
        private bool _enterWasDown;
        private int _talkingNpcIndex = -1;
        private bool _pendingChatClose;

        public bool IsVisible => _isVisible;
        public int TalkingNpcIndex => _talkingNpcIndex;
        private const float MaxNpcDistance = 300f;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                CraftableInterface = new UserInterface();
                CraftableState = new CraftableUIState();
                CraftableState.Activate();
            }
        }

        public override void Unload()
        {
            CraftableState = null;
            CraftableInterface = null;
        }

        public void ShowUI(int npcIndex = -1)
        {
            _isVisible = true;
            _escWasDown = false;
            _enterWasDown = false;
            _talkingNpcIndex = npcIndex;
            _pendingChatClose = true;
            CraftableInterface?.SetState(CraftableState);
            CraftableState?.OnShow();
        }

        public void HideUI()
        {
            _isVisible = false;
            _escWasDown = false;
            _enterWasDown = false;
            _talkingNpcIndex = -1;
            CraftableInterface?.SetState(null);
        }

        public override void PostUpdateInput()
        {
            if (!_isVisible || CraftableState == null)
            {
                return;
            }

            if (CraftableState.IsSearchFocused)
            {
                CraftableState.ApplySearchTextInputCapture();
            }

            if (CraftableState.IsMouseOverMainPanel && PlayerInput.ScrollWheelDeltaForUI != 0)
            {
                PlayerInput.LockVanillaMouseScroll("SteroidGuide.Craftable.Panel");
            }
        }

        public void ToggleUI()
        {
            if (_isVisible) HideUI();
            else ShowUI();
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (_pendingChatClose)
            {
                _pendingChatClose = false;
                Main.player[Main.myPlayer].SetTalkNPC(-1);
                Main.npcChatText = "";
            }

            if (!_isVisible)
                return;

            CraftableInterface?.Update(gameTime);

            bool enterDown = Main.keyState.IsKeyDown(Keys.Enter);
            if (enterDown && !_enterWasDown && CraftableState?.HandleSearchEnterKey() == true)
            {
                _enterWasDown = true;
                return;
            }
            _enterWasDown = enterDown;

            CraftableState?.UpdateSearchTextInput();

            // ESC to close
            bool escDown = Main.keyState.IsKeyDown(Keys.Escape);
            if (escDown && !_escWasDown)
            {
                if (CraftableState?.HandleEscapeKey() == true)
                {
                    _escWasDown = escDown;
                    return;
                }

                HideUI();
                // Prevent inventory from opening on the same ESC press
                Main.playerInventory = false;
                _escWasDown = escDown;
                return;
            }
            _escWasDown = escDown;

            // Auto-close if player is too far from the NPC
            if (_talkingNpcIndex >= 0 && _talkingNpcIndex < Main.maxNPCs && Main.LocalPlayer != null)
            {
                NPC npc = Main.npc[_talkingNpcIndex];
                if (!npc.active || npc.type == NPCID.None)
                {
                    HideUI();
                }
                else
                {
                    float dx = Main.LocalPlayer.Center.X - npc.Center.X;
                    float dy = Main.LocalPlayer.Center.Y - npc.Center.Y;
                    float distSq = dx * dx + dy * dy;
                    if (distSq > MaxNpcDistance * MaxNpcDistance)
                    {
                        HideUI();
                    }
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "SteroidGuide: Craftable",
                    delegate
                    {
                        if (_isVisible)
                        {
                            CraftableInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
    }
}
