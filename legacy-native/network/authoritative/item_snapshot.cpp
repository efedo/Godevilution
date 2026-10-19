#include "network/authoritative/item_snapshot.hpp"

#include <climits>
#include <limits>
#include <type_traits>

#include "devilution.pb.h"
#include "network/authoritative/protocol_limits.hpp"
#include "tables/itemdat.h"
#include "utils/string/utf8.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

template <typename Target, typename Source>
bool AssignIfRepresentable(Target &target, Source source)
{
	if constexpr (std::is_enum_v<Target>) {
		using Underlying = std::underlying_type_t<Target>;
		if (!std::in_range<Underlying>(source))
			return false;
	} else if constexpr (std::is_integral_v<Target> && std::is_integral_v<Source>) {
		if (!std::in_range<Target>(source))
			return false;
	}
	target = static_cast<Target>(source);
	return true;
}

} // namespace

tl::expected<Item, std::string> ProjectNativeItem(const protocol::ItemStateSnapshot &state, uint32_t itemSeed, uint32_t price)
{
	if (price > static_cast<uint32_t>(INT_MAX))
		return tl::make_unexpected("Server-backed item price is outside the native range.");
	if (state.value() < 0
	    || state.identified_value() < 0
	    || state.min_damage() < 0
	    || state.max_damage() < state.min_damage()
	    || state.armor_class() < 0
	    || state.charges() < 0
	    || state.max_charges() < state.charges()
	    || state.durability() < 0
	    || state.max_durability() < state.durability()
	    || state.fire_min_damage() < 0
	    || state.fire_max_damage() < state.fire_min_damage()
	    || state.lightning_min_damage() < 0
	    || state.lightning_max_damage() < state.lightning_min_damage()
	    || static_cast<uint32_t>(state.presentation_kind()) > static_cast<uint32_t>(MaximumItemPresentationKind)
	    || static_cast<uint32_t>(state.spell_source_kind()) > static_cast<uint32_t>(MaximumItemSpellSourceKind)
	    || static_cast<uint32_t>(state.item_action_kind()) > static_cast<uint32_t>(MaximumItemActionKind)
	    || static_cast<uint32_t>(state.spell_targeting_kind()) > static_cast<uint32_t>(MaximumSpellTargetingKind)
	    || state.spell_display_name().find('\0') != std::string::npos) {
		return tl::make_unexpected("Server-backed item contains invalid semantic state.");
	}
	if (state.base_name().size() >= ItemNameLength
	    || state.identified_name().size() >= ItemNameLength
	    || state.base_name().find('\0') != std::string::npos
	    || state.identified_name().find('\0') != std::string::npos)
		return tl::make_unexpected("Server-backed item contains a dynamic name outside the native representation.");
	Item item;
	item._iSeed = itemSeed;
	if (!AssignIfRepresentable(item._iCreateInfo, state.create_info())
	    || !AssignIfRepresentable(item._itype, state.item_type())
	    || !AssignIfRepresentable(item._iMagical, state.magical())
	    || !AssignIfRepresentable(item._iLoc, state.equip_location())
	    || !AssignIfRepresentable(item._iClass, state.item_class())
	    || !AssignIfRepresentable(item._iMinDam, state.min_damage())
	    || !AssignIfRepresentable(item._iMaxDam, state.max_damage())
	    || !AssignIfRepresentable(item._iAC, state.armor_class())
	    || !AssignIfRepresentable(item._iFlags, state.flags())
	    || !AssignIfRepresentable(item._iMiscId, state.misc_id())
	    || !AssignIfRepresentable(item._iSpell, state.spell_id())
	    || !AssignIfRepresentable(item.IDidx, state.item_index())
	    || !AssignIfRepresentable(item._iPLDam, state.plus_damage())
	    || !AssignIfRepresentable(item._iPLToHit, state.plus_to_hit())
	    || !AssignIfRepresentable(item._iPLAC, state.plus_armor_class())
	    || !AssignIfRepresentable(item._iPLStr, state.plus_strength())
	    || !AssignIfRepresentable(item._iPLMag, state.plus_magic())
	    || !AssignIfRepresentable(item._iPLDex, state.plus_dexterity())
	    || !AssignIfRepresentable(item._iPLVit, state.plus_vitality())
	    || !AssignIfRepresentable(item._iPLFR, state.plus_fire_resistance())
	    || !AssignIfRepresentable(item._iPLLR, state.plus_lightning_resistance())
	    || !AssignIfRepresentable(item._iPLMR, state.plus_magic_resistance())
	    || !AssignIfRepresentable(item._iPLMana, state.plus_mana())
	    || !AssignIfRepresentable(item._iPLHP, state.plus_hit_points())
	    || !AssignIfRepresentable(item._iPLDamMod, state.plus_damage_modifier())
	    || !AssignIfRepresentable(item._iPLGetHit, state.plus_get_hit())
	    || !AssignIfRepresentable(item._iPLLight, state.plus_light())
	    || !AssignIfRepresentable(item._iSplLvlAdd, state.spell_level_add())
	    || !AssignIfRepresentable(item._iFMinDam, state.fire_min_damage())
	    || !AssignIfRepresentable(item._iFMaxDam, state.fire_max_damage())
	    || !AssignIfRepresentable(item._iLMinDam, state.lightning_min_damage())
	    || !AssignIfRepresentable(item._iLMaxDam, state.lightning_max_damage())
	    || !AssignIfRepresentable(item._iPLEnAc, state.plus_enemy_armor_class())
	    || !AssignIfRepresentable(item._iPrePower, state.prefix_power())
	    || !AssignIfRepresentable(item._iSufPower, state.suffix_power())
	    || !AssignIfRepresentable(item._iMinStr, state.minimum_strength())
	    || !AssignIfRepresentable(item._iMinMag, state.minimum_magic())
	    || !AssignIfRepresentable(item._iMinDex, state.minimum_dexterity())
	    || !AssignIfRepresentable(item._iDamAcFlags, state.hellfire_damage_armor_flags())) {
		return tl::make_unexpected("Server-backed item state contains a value outside the native representation.");
	}
	item.position = { state.position_x(), state.position_y() };
	item._iDelFlag = state.deleted();
	item._iIdentified = state.identified();
	item._ivalue = state.value();
	item._iIvalue = static_cast<int>(price);
	item._iCharges = state.charges();
	item._iMaxCharges = state.max_charges();
	item._iDurability = state.durability();
	item._iMaxDur = state.max_durability();
	item._iUid = state.unique_id();
	item._iVAdd1 = state.value_add_1();
	item._iVMult1 = state.value_multiply_1();
	item._iVAdd2 = state.value_add_2();
	item._iVMult2 = state.value_multiply_2();
	item._iStatFlag = state.stat_flag();
	item.dwBuff = state.buff();
	if (state.item_index() >= 0 && static_cast<size_t>(state.item_index()) < AllItemsList.size()) {
		const auto &baseItem = AllItemsList[static_cast<size_t>(state.item_index())];
		CopyUtf8(item._iName, baseItem.iName, ItemNameLength);
		CopyUtf8(item._iIName, baseItem.iName, ItemNameLength);
		item._iCurs = baseItem.iCurs;
		item.setNewAnimation(false);
	}
	if (!state.base_name().empty())
		CopyUtf8(item._iName, state.base_name(), ItemNameLength);
	if (!state.identified_name().empty())
		CopyUtf8(item._iIName, state.identified_name(), ItemNameLength);
	if (state.has_native_cursor_graphic()) {
		if (!AssignIfRepresentable(item._iCurs, state.native_cursor_graphic()))
			return tl::make_unexpected("Server-backed item cursor graphic is outside the native representation.");
		item.setNewAnimation(false);
	}
	// Presentation metadata is server-owned. Keep the native compatibility
	// identity aligned even when an older or custom content row carries
	// conflicting legacy item fields.
	switch (state.presentation_kind()) {
	case protocol::ITEM_PRESENTATION_KIND_GOLD:
		item._itype = ItemType::Gold;
		item._iClass = ICLASS_GOLD;
		item._iLoc = ILOC_UNEQUIPABLE;
		item._iMiscId = IMISC_NONE;
		item.IDidx = IDI_GOLD;
		break;
	case protocol::ITEM_PRESENTATION_KIND_EAR:
		item._itype = ItemType::Misc;
		item._iClass = ICLASS_MISC;
		item._iLoc = ILOC_UNEQUIPABLE;
		item._iMiscId = IMISC_EAR;
		item.IDidx = IDI_EAR;
		break;
	default:
		break;
	}
	return item;
}

} // namespace devilution::authoritative
