/**
 * @file game/replay/replay.cpp
 *
 * Deterministic replay primitives.
 */


#include "game/replay/replay.hpp"

#include <algorithm>
#include <span>

#include <picosha2.h>

#include "game/items/items.hpp"
#include "game/players/players.hpp"
#include "game/stores/stores.hpp"

namespace devilution {

namespace {

void AppendItemRange(ReplayStateHasher &hasher, std::span<const Item> items)
{
	hasher.AppendUint64(items.size());
	for (const Item &item : items)
		AppendReplayItemState(hasher, item);
}

void AppendVitalResource(ReplayStateHasher &hasher, const VitalResource &resource)
{
	hasher.AppendInt32(resource.base);
	hasher.AppendInt32(resource.current);
	hasher.AppendInt32(resource.maximum);
	hasher.AppendInt32(resource.maximumBase);
}

void AppendModifiableAttribute(ReplayStateHasher &hasher, const ModifiableAttribute &attribute)
{
	hasher.AppendInt32(attribute.base);
	hasher.AppendInt32(attribute.current);
}

} // namespace

bool IsReplayCommandOrderBefore(const ReplayCommandOrder &left, const ReplayCommandOrder &right) noexcept
{
	if (left.targetTick != right.targetTick)
		return left.targetTick < right.targetTick;
	return left.serverReceiptSequence < right.serverReceiptSequence;
}

std::vector<ReplayCommand> SortReplayCommands(std::vector<ReplayCommand> commands)
{
	std::stable_sort(commands.begin(), commands.end(), [](const ReplayCommand &left, const ReplayCommand &right) {
		return IsReplayCommandOrderBefore(left.order, right.order);
	});
	return commands;
}

void ReplayStateHasher::AppendBool(bool value)
{
	AppendUint8(value ? 1 : 0);
}

void ReplayStateHasher::AppendUint8(uint8_t value)
{
	bytes_.push_back(value);
}

void ReplayStateHasher::AppendInt32(int32_t value)
{
	AppendUint32(static_cast<uint32_t>(value));
}

void ReplayStateHasher::AppendUint32(uint32_t value)
{
	for (size_t byte = 0; byte < sizeof(value); ++byte)
		bytes_.push_back(static_cast<uint8_t>(value >> (byte * 8)));
}

void ReplayStateHasher::AppendUint64(uint64_t value)
{
	for (size_t byte = 0; byte < sizeof(value); ++byte)
		bytes_.push_back(static_cast<uint8_t>(value >> (byte * 8)));
}

void ReplayStateHasher::AppendString(std::string_view value)
{
	AppendUint64(value.size());
	bytes_.insert(bytes_.end(), value.begin(), value.end());
}

std::array<uint8_t, 32> ReplayStateHasher::Digest() const
{
	std::array<uint8_t, 32> digest;
	picosha2::hash256(bytes_.begin(), bytes_.end(), digest.begin(), digest.end());
	return digest;
}

std::string ReplayStateHasher::HexDigest() const
{
	const std::array<uint8_t, 32> digest = Digest();
	return picosha2::bytes_to_hex_string(digest.begin(), digest.end());
}

void AppendReplayItemState(ReplayStateHasher &hasher, const Item &item)
{
	hasher.AppendUint32(item._iSeed);
	hasher.AppendUint32(item._iCreateInfo);
	hasher.AppendInt32(static_cast<int32_t>(item._itype));
	hasher.AppendInt32(item.position.x);
	hasher.AppendInt32(item.position.y);
	hasher.AppendBool(item._iDelFlag);
	hasher.AppendBool(item._iIdentified);
	hasher.AppendInt32(static_cast<int32_t>(item._iMagical));
	hasher.AppendInt32(static_cast<int32_t>(item._iLoc));
	hasher.AppendInt32(static_cast<int32_t>(item._iClass));
	hasher.AppendInt32(item._ivalue);
	hasher.AppendInt32(item._iIvalue);
	hasher.AppendInt32(item._iMinDam);
	hasher.AppendInt32(item._iMaxDam);
	hasher.AppendInt32(item._iAC);
	hasher.AppendInt32(static_cast<int32_t>(item._iFlags));
	hasher.AppendInt32(static_cast<int32_t>(item._iMiscId));
	hasher.AppendInt32(static_cast<int32_t>(item._iSpell));
	hasher.AppendInt32(static_cast<int32_t>(item.IDidx));
	hasher.AppendInt32(item._iCharges);
	hasher.AppendInt32(item._iMaxCharges);
	hasher.AppendInt32(item._iDurability);
	hasher.AppendInt32(item._iMaxDur);
	hasher.AppendInt32(item._iPLDam);
	hasher.AppendInt32(item._iPLToHit);
	hasher.AppendInt32(item._iPLAC);
	hasher.AppendInt32(item._iPLStr);
	hasher.AppendInt32(item._iPLMag);
	hasher.AppendInt32(item._iPLDex);
	hasher.AppendInt32(item._iPLVit);
	hasher.AppendInt32(item._iPLFR);
	hasher.AppendInt32(item._iPLLR);
	hasher.AppendInt32(item._iPLMR);
	hasher.AppendInt32(item._iPLMana);
	hasher.AppendInt32(item._iPLHP);
	hasher.AppendInt32(item._iPLDamMod);
	hasher.AppendInt32(item._iPLGetHit);
	hasher.AppendInt32(item._iPLLight);
	hasher.AppendInt32(item._iSplLvlAdd);
	hasher.AppendInt32(item._iUid);
	hasher.AppendInt32(item._iFMinDam);
	hasher.AppendInt32(item._iFMaxDam);
	hasher.AppendInt32(item._iLMinDam);
	hasher.AppendInt32(item._iLMaxDam);
	hasher.AppendInt32(item._iPLEnAc);
	hasher.AppendInt32(static_cast<int32_t>(item._iPrePower));
	hasher.AppendInt32(static_cast<int32_t>(item._iSufPower));
	hasher.AppendInt32(item._iVAdd1);
	hasher.AppendInt32(item._iVMult1);
	hasher.AppendInt32(item._iVAdd2);
	hasher.AppendInt32(item._iVMult2);
	hasher.AppendInt32(item._iMinStr);
	hasher.AppendInt32(item._iMinMag);
	hasher.AppendInt32(item._iMinDex);
	hasher.AppendBool(item._iStatFlag);
	hasher.AppendInt32(static_cast<int32_t>(item._iDamAcFlags));
	hasher.AppendUint32(item.dwBuff);
}

void AppendReplayPlayerState(ReplayStateHasher &hasher, uint8_t playerId, const Player &player)
{
	hasher.AppendUint8(playerId);
	hasher.AppendString(player.name());
	hasher.AppendInt32(static_cast<int32_t>(player._pClass));
	hasher.AppendUint8(player.getCharacterLevel());
	hasher.AppendUint32(player._pExperience);
	hasher.AppendInt32(player._pGold);

	AppendModifiableAttribute(hasher, player.attributes.strength);
	AppendModifiableAttribute(hasher, player.attributes.magic);
	AppendModifiableAttribute(hasher, player.attributes.dexterity);
	AppendModifiableAttribute(hasher, player.attributes.vitality);
	AppendVitalResource(hasher, player.life);
	AppendVitalResource(hasher, player.mana);
	hasher.AppendUint32(static_cast<uint32_t>(player._pIFlags));
	hasher.AppendUint8(static_cast<uint8_t>(player.position.tile.x));
	hasher.AppendUint8(static_cast<uint8_t>(player.position.tile.y));
	hasher.AppendInt32(player._pNumInv);

	AppendItemRange(hasher, std::span<const Item>(player.InvBody));
	AppendItemRange(hasher, std::span<const Item>(player.InvList));
	AppendItemRange(hasher, std::span<const Item>(player.SpdList));
	AppendReplayItemState(hasher, player.HoldItem);
	for (const int8_t cell : player.InvGrid)
		hasher.AppendInt32(cell);
}

void AppendReplayStoreState(ReplayStateHasher &hasher, const StoreManager &storeManager)
{
	hasher.AppendInt32(static_cast<int32_t>(storeManager.activeStore()));
	hasher.AppendInt32(storeManager.premiumItemCount());
	hasher.AppendInt32(storeManager.premiumItemLevel());
	AppendItemRange(hasher, storeManager.smithItems());
	AppendItemRange(hasher, storeManager.premiumItems());
	AppendItemRange(hasher, storeManager.healerItems());
	AppendItemRange(hasher, storeManager.witchItems());
	AppendReplayItemState(hasher, storeManager.boyItem());
}

} // namespace devilution
