/**
 * @file lua/lua_event.cpp
 *
 * Implementation of lua event.
 */


#include "lua/lua_event.hpp"

#include <optional>
#include <string_view>
#include <utility>

#include <sol/sol.hpp>

#include "game/events/event_bus.hpp"
#include "game/monsters/monsters.hpp"
#include "game/players/players.hpp"
#include "lua/lua_global.hpp"
#include "utils/log.hpp"

namespace devilution {

namespace {

template <typename... Args>
void CallLuaEvent(std::string_view name, Args &&...args)
{
	sol::table *events = GetLuaEvents();
	if (events == nullptr) {
		return;
	}

	const auto trigger = events->traverse_get<std::optional<sol::object>>(name, "trigger");
	if (!trigger.has_value() || !trigger->is<sol::protected_function>()) {
		LogError("events.{}.trigger is not a function", name);
		return;
	}
	const sol::protected_function fn = trigger->as<sol::protected_function>();
	SafeCallResult(fn(std::forward<Args>(args)...), /*optional=*/true);
}

class LuaEventAdapter final : public GameEventListener {
public:
	void OnMonsterDataLoaded() override
	{
		CallLuaEvent("MonsterDataLoaded");
	}
	void OnUniqueMonsterDataLoaded() override
	{
		CallLuaEvent("UniqueMonsterDataLoaded");
	}
	void OnItemDataLoaded() override
	{
		CallLuaEvent("ItemDataLoaded");
	}
	void OnUniqueItemDataLoaded() override
	{
		CallLuaEvent("UniqueItemDataLoaded");
	}
	void OnStoreOpened(std::string_view name) override
	{
		CallLuaEvent("StoreOpened", name);
	}
	void OnMonsterTakeDamage(size_t monsterId, int damage, int damageType) override
	{
		if (monsterId < MaxMonsters)
			CallLuaEvent("OnMonsterTakeDamage", &Monsters[monsterId], damage, damageType);
	}
	void OnPlayerGainExperience(uint8_t playerId, uint32_t experience) override
	{
		if (playerId < Players.size())
			CallLuaEvent("OnPlayerGainExperience", &Players[playerId], experience);
	}
	void OnPlayerTakeDamage(uint8_t playerId, int damage, int damageType) override
	{
		if (playerId < Players.size())
			CallLuaEvent("OnPlayerTakeDamage", &Players[playerId], damage, damageType);
	}
	void OnGameDataReloaded() override
	{
		CallLuaEvent("LoadModsComplete");
	}
	void OnGameDrawComplete() override
	{
		CallLuaEvent("GameDrawComplete");
	}
	void OnGameStart() override
	{
		CallLuaEvent("GameStart");
	}
};

std::optional<LuaEventAdapter> CurrentLuaEventAdapter;

} // namespace

void InitializeLuaEventAdapter()
{
	CurrentLuaEventAdapter.emplace();
	CurrentGameEventBus.Subscribe(*CurrentLuaEventAdapter);
}

void ShutdownLuaEventAdapter()
{
	if (!CurrentLuaEventAdapter.has_value())
		return;
	CurrentGameEventBus.Unsubscribe(*CurrentLuaEventAdapter);
	CurrentLuaEventAdapter.reset();
}

} // namespace devilution
