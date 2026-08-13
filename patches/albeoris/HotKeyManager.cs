using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Septerra.WindowsMessages;

namespace Septerra.Core.Hooks
{
    public static class HotKeyManager
    {
        public static HotKey Current { get; set; }

        public static bool TryHandle(UInt32 hotKey)
        {
            Current = 0;

            switch (SceneManager.CurrentSceneType)
            {
                case 4:
                    return ProcessBattle4(hotKey);
                default:
                    return false;

            }
        }

        private static Boolean ProcessBattle4(UInt32 hotKey)
        {
            // if (hotKey == 'F')
            // {
            //     Current = HotKey.NextTurn;
            //     return true;
            // }

            return false;
        }

        public static Boolean TryEvict(HotKey hotKey)
        {
            if (Current == hotKey)
            {
                Current = HotKey.None;
                return true;
            }

            return false;
        }

        public static Boolean TryHandle(KeyDownMessageInfo keyDown)
        {
            // WM_KEYDOWN repeat count is 1 on the first press. Ignore auto-repeat
            // via the previous-state bit so one physical F is one NextTurn.
            if (keyDown.WasKeyDown)
                return false;
            
            switch (SceneManager.CurrentSceneType)
            {
                case 4:
                    return WhenKeyDownInBattle(keyDown);
            }

            return false;
        }

        public static Boolean TryHandle(KeyUpMessageInfo keyUp)
        {
            return false;
        }
        
        private static Boolean WhenKeyDownInBattle(KeyDownMessageInfo keyDown)
        {
            if (keyDown.VirtualKeyCode == Keys.F)
            {
                Current = HotKey.NextTurn;
                return true;
            }

            return false;
        }
    }
}