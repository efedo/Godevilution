/**
 * @file controls/control_mode.cpp
 *
 * Implementation of control mode management.
 */


#include "controls/control_mode.hpp"

namespace devilution {

ControlTypes ControlMode = ControlTypes::None;
ControlTypes ControlDevice = ControlTypes::None;

GamepadLayout GamepadType =
#if defined(DEVILUTIONX_GAMEPAD_TYPE)
    GamepadLayout::
        DEVILUTIONX_GAMEPAD_TYPE;
#else
    GamepadLayout::Generic;
#endif

} // namespace devilution
