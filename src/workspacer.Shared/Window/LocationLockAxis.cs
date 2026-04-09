using System;

namespace workspacer;

[Flags]
public enum LocationLockAxis
{
    None = 0b0000,
    Right = 0b0001,
    Left = 0b0010,
    Top = 0b0100,
    Bottom = 0b1000,
    
    All = Right | Left | Top | Bottom,
    AllExceptLeft = Right | Top | Bottom,
    AllExceptRight = Left | Top | Bottom,
    AllExceptTop = Left | Right | Bottom,
    AllExceptBottom = Left | Right | Top,
    
    Horizontal = Left | Right,
    Vertical = Top | Bottom,
}