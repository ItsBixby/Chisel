using Engine.Input;
using Microsoft.Xna.Framework.Input;

namespace FPSTemplate.Utilities.Player;
public static class PlayerInput
{
    // Combat
    public static InputBinding Fire;
    public static InputBinding AltFire;
    public static InputBinding Reload;
    public static InputBinding Flashlight;

    // Movement
    public static InputBinding Forward;
    public static InputBinding Backward;
    public static InputBinding Left;
    public static InputBinding Right;
    public static InputBinding Crouch;
    public static InputBinding Sprint;
    public static InputBinding Jump;
    public static InputBinding Use;

    // Look (gamepad)
    public static InputBinding GamepadLookX;
    public static InputBinding GamepadLookY;

    // Weapon slots
    public static InputBinding Slot0;
    public static InputBinding Slot1;
    public static InputBinding Slot2;
    public static InputBinding Slot3;
    public static InputBinding Slot4;
    public static InputBinding Slot5;
    public static InputBinding Slot6;
    public static InputBinding Slot7;
    public static InputBinding Slot8;
    public static InputBinding SlotNext;
    public static InputBinding SlotPrev;

    // Misc
    public static InputBinding QuickSave;
    public static InputBinding QuickLoad;

    public static bool AnyPressed => Forward.IsPressed() ||
                                     Backward.IsPressed() ||
                                     Left.IsPressed() ||
                                     Right.IsPressed() ||
                                     Use.IsPressed() ||
                                     Jump.IsPressed();

    public static void InitializeInputs()
    {
        // Combat
        Fire = new InputBinding("fire", "Fire", "Combat",
            new BoundMouseButton { MouseButton = MouseButton.Left },
            new BoundGamepadButton(0, Buttons.RightTrigger));

        AltFire = new InputBinding("alt_fire", "Alt Fire", "Combat",
            new BoundMouseButton { MouseButton = MouseButton.Right },
            new BoundGamepadButton(0, Buttons.LeftTrigger));

        Reload = new InputBinding("reload", "Reload", "Combat",
            new BoundKey { Key = Keys.R },
            new BoundGamepadButton(0, Buttons.Y));

        Flashlight = new InputBinding("flashlight", "Flashlight", "Combat",
            new BoundKey { Key = Keys.F },
            new BoundGamepadButton(0, Buttons.B));

        // Movement
        Forward = new InputBinding("move_forward", "Move Forward", "Movement",
            new BoundKey { Key = Keys.W },
            new BoundGamepadAxis(0, GamepadJoystick.Left, GamepadJoystickAxis.Y, AxisSide.Positive));

        Backward = new InputBinding("move_backward", "Move Backward", "Movement",
            new BoundKey { Key = Keys.S },
            new BoundGamepadAxis(0, GamepadJoystick.Left, GamepadJoystickAxis.Y, AxisSide.Negative));

        Left = new InputBinding("move_left", "Move Left", "Movement",
            new BoundKey { Key = Keys.A },
            new BoundGamepadAxis(0, GamepadJoystick.Left, GamepadJoystickAxis.X, AxisSide.Negative));

        Right = new InputBinding("move_right", "Move Right", "Movement",
            new BoundKey { Key = Keys.D },
            new BoundGamepadAxis(0, GamepadJoystick.Left, GamepadJoystickAxis.X, AxisSide.Positive));

        Crouch = new InputBinding("crouch", "Crouch", "Movement",
            new BoundKey { Key = Keys.LeftControl });

        Sprint = new InputBinding("sprint", "Sprint", "Movement",
            new BoundKey { Key = Keys.LeftShift });

        Jump = new InputBinding("jump", "Jump", "Movement",
            new BoundKey { Key = Keys.Space },
            new BoundGamepadButton(0, Buttons.A));

        Use = new InputBinding("use", "Use / Interact", "Movement",
            new BoundKey { Key = Keys.E },
            new BoundGamepadButton(0, Buttons.X));

        // Look (gamepad)
        GamepadLookX = new InputBinding("gamepad_look_x", "Look Horizontal", "Gamepad Look",
            new BoundGamepadAxis(0, GamepadJoystick.Right, GamepadJoystickAxis.X, AxisSide.Both));

        GamepadLookY = new InputBinding("gamepad_look_y", "Look Vertical", "Gamepad Look",
            new BoundGamepadAxis(0, GamepadJoystick.Right, GamepadJoystickAxis.Y, AxisSide.Both));

        // Weapon slots
        Slot0 = new InputBinding("slot_0", "Weapon Slot 1", "Weapon Slots", new BoundKey { Key = Keys.D1 });
        Slot1 = new InputBinding("slot_1", "Weapon Slot 2", "Weapon Slots", new BoundKey { Key = Keys.D2 });
        Slot2 = new InputBinding("slot_2", "Weapon Slot 3", "Weapon Slots", new BoundKey { Key = Keys.D3 });
        Slot3 = new InputBinding("slot_3", "Weapon Slot 4", "Weapon Slots", new BoundKey { Key = Keys.D4 });
        Slot4 = new InputBinding("slot_4", "Weapon Slot 5", "Weapon Slots", new BoundKey { Key = Keys.D5 });
        Slot5 = new InputBinding("slot_5", "Weapon Slot 6", "Weapon Slots", new BoundKey { Key = Keys.D6 });
        Slot6 = new InputBinding("slot_6", "Weapon Slot 7", "Weapon Slots", new BoundKey { Key = Keys.D7 });
        Slot7 = new InputBinding("slot_7", "Weapon Slot 8", "Weapon Slots", new BoundKey { Key = Keys.D8 });
        Slot8 = new InputBinding("slot_8", "Weapon Slot 9", "Weapon Slots", new BoundKey { Key = Keys.D9 });
        SlotNext = new InputBinding("slot_next", "Next Slot", "Weapon Slots", new BoundGamepadButton ( 0, Buttons.RightShoulder));
        SlotPrev = new InputBinding("slot_prev", "Previous Slot", "Weapon Slots", new BoundGamepadButton ( 0, Buttons.LeftShoulder));

        // Misc
        QuickSave = new InputBinding("quick_save", "Quick Save", "Misc", new BoundKey { Key = Keys.F6 });
        QuickLoad = new InputBinding("quick_load", "Quick Load", "Misc", new BoundKey { Key = Keys.F9 });
    }
}
