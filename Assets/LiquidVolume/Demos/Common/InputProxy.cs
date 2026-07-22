using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace LiquidVolumeFX {

    public static class InputProxy {

        public static bool GetKey(KeyCode keyCode) {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(keyCode);
#elif ENABLE_INPUT_SYSTEM
            return GetKeyInternal(keyCode, false);
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode keyCode) {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#elif ENABLE_INPUT_SYSTEM
            return GetKeyInternal(keyCode, true);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static bool GetKeyInternal(KeyCode keyCode, bool down) {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            var control = GetKeyControl(keyboard, keyCode);
            if (control == null) return false;
            return down ? control.wasPressedThisFrame : control.isPressed;
        }

        static KeyControl GetKeyControl(Keyboard keyboard, KeyCode keyCode) {
            switch (keyCode) {
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.F: return keyboard.fKey;
                default: return null;
            }
        }
#endif
    }
}
