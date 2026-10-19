#pragma once

/**
 * @file game/events/event_bus.hpp
 *
 * Engine-neutral game events.
 */


#include <cstddef>
#include <cstdint>
#include <string_view>
#include <vector>

namespace devilution {

class GameEventListener {
public:
	virtual ~GameEventListener() = default;

	virtual void OnMonsterDataLoaded() = 0;
	virtual void OnUniqueMonsterDataLoaded() = 0;
	virtual void OnItemDataLoaded() = 0;
	virtual void OnUniqueItemDataLoaded() = 0;
	virtual void OnStoreOpened(std::string_view name) = 0;
	virtual void OnMonsterTakeDamage(size_t monsterId, int damage, int damageType) = 0;
	virtual void OnPlayerGainExperience(uint8_t playerId, uint32_t experience) = 0;
	virtual void OnPlayerTakeDamage(uint8_t playerId, int damage, int damageType) = 0;
	virtual void OnGameDataReloaded() = 0;
	virtual void OnGameDrawComplete() = 0;
	virtual void OnGameStart() = 0;
};

class GameEventBus {
public:
	void Subscribe(GameEventListener &listener);
	void Unsubscribe(GameEventListener &listener);

	void MonsterDataLoaded();
	void UniqueMonsterDataLoaded();
	void ItemDataLoaded();
	void UniqueItemDataLoaded();
	void StoreOpened(std::string_view name);
	void MonsterTakeDamage(size_t monsterId, int damage, int damageType);
	void PlayerGainExperience(uint8_t playerId, uint32_t experience);
	void PlayerTakeDamage(uint8_t playerId, int damage, int damageType);
	void GameDataReloaded();
	void GameDrawComplete();
	void GameStart();

private:
	std::vector<GameEventListener *> listeners_;
};

extern GameEventBus CurrentGameEventBus;

} // namespace devilution
