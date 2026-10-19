#pragma once

/**
 * @file controls/touch/gamepad.h
 *
 * Interface for gamepad.
 */


#include <functional>

#include "controls/controller_buttons.h"
#include "engine/math/circle.hpp"
#include "engine/math/point.hpp"
#include "engine/math/rectangle.hpp"

namespace devilution {

struct VirtualDirectionPad {
	Circle area;
	Point position;
	bool isUpPressed { false };
	bool isDownPressed { false };
	bool isLeftPressed { false };
	bool isRightPressed { false };

	VirtualDirectionPad()
	    : area({ { 0, 0 }, 0 })
	    , position({ 0, 0 })
	{
	}

	void UpdatePosition(Point touchCoordinates);
	void Deactivate();
};

struct VirtualButton {
	bool isHeld { false };
	bool didStateChange { false };
	std::function<bool()> isUsable;

	VirtualButton()
	    : isUsable([]() { return true; })
	{
	}

	virtual bool contains(Point point) = 0;
	void Deactivate();
};

struct VirtualMenuButton : VirtualButton {
	Rectangle area;

	VirtualMenuButton()
	    : area({ { 0, 0 }, { 0, 0 } })
	{
	}

	bool contains(Point point) override
	{
		return area.contains(point);
	}
};

struct VirtualPadButton : VirtualButton {
	Circle area;

	VirtualPadButton()
	    : area({ { 0, 0 }, 0 })
	{
	}

	bool contains(Point point) override
	{
		return area.contains(point);
	}
};

struct VirtualMenuPanel {
	VirtualMenuButton charButton;
	VirtualMenuButton questsButton;
	VirtualMenuButton inventoryButton;
	VirtualMenuButton mapButton;
	Rectangle area;

	VirtualMenuPanel()
	    : area({ { 0, 0 }, { 0, 0 } })
	{
	}

	void Deactivate();
};

struct VirtualGamepad {
	VirtualMenuPanel menuPanel;
	VirtualDirectionPad directionPad;
	VirtualPadButton standButton;

	VirtualPadButton primaryActionButton;
	VirtualPadButton secondaryActionButton;
	VirtualPadButton spellActionButton;
	VirtualPadButton cancelButton;

	VirtualPadButton healthButton;
	VirtualPadButton manaButton;

	bool isActive { false };

	VirtualGamepad() = default;

	void Deactivate();
};

void InitializeVirtualGamepad();
void ActivateVirtualGamepad();
void DeactivateVirtualGamepad();

extern VirtualGamepad VirtualGamepadState;

} // namespace devilution
