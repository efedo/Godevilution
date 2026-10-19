/**
 * @file engine/attributes.hpp
 *
 * Small value types for combat attributes and resources.
 */
#pragma once

namespace devilution {

struct ModifiableAttribute {
	int base = 0;
	int current = 0;
};

struct PrimaryAttributes {
	ModifiableAttribute strength;
	ModifiableAttribute magic;
	ModifiableAttribute dexterity;
	ModifiableAttribute vitality;
};

struct VitalResource {
	int base = 0;
	int current = 0;
	int maximum = 0;
	int maximumBase = 0;
	int percentage = 0;
};

struct DamageRange {
	int minimum = 0;
	int maximum = 0;
};

struct DamageBonuses {
	DamageRange physical;
	DamageRange fire;
	DamageRange lightning;
	int percent = 0;
	int flat = 0;
	int armorPiercing = 0;
};

} // namespace devilution
