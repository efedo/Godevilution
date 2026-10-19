/**
 * @file game/events/event_bus.cpp
 *
 * Engine-neutral game events.
 */


#include "game/events/event_bus.hpp"

#include <algorithm>

namespace devilution {

GameEventBus CurrentGameEventBus;

void GameEventBus::Subscribe(GameEventListener &listener)
{
	Unsubscribe(listener);
	listeners_.push_back(&listener);
}

void GameEventBus::Unsubscribe(GameEventListener &listener)
{
	listeners_.erase(std::remove(listeners_.begin(), listeners_.end(), &listener), listeners_.end());
}

void GameEventBus::MonsterDataLoaded()
{
	for (GameEventListener *listener : listeners_)
		listener->OnMonsterDataLoaded();
}

void GameEventBus::UniqueMonsterDataLoaded()
{
	for (GameEventListener *listener : listeners_)
		listener->OnUniqueMonsterDataLoaded();
}

void GameEventBus::ItemDataLoaded()
{
	for (GameEventListener *listener : listeners_)
		listener->OnItemDataLoaded();
}

void GameEventBus::UniqueItemDataLoaded()
{
	for (GameEventListener *listener : listeners_)
		listener->OnUniqueItemDataLoaded();
}

void GameEventBus::StoreOpened(std::string_view name)
{
	for (GameEventListener *listener : listeners_)
		listener->OnStoreOpened(name);
}

void GameEventBus::MonsterTakeDamage(size_t monsterId, int damage, int damageType)
{
	for (GameEventListener *listener : listeners_)
		listener->OnMonsterTakeDamage(monsterId, damage, damageType);
}

void GameEventBus::PlayerGainExperience(uint8_t playerId, uint32_t experience)
{
	for (GameEventListener *listener : listeners_)
		listener->OnPlayerGainExperience(playerId, experience);
}

void GameEventBus::PlayerTakeDamage(uint8_t playerId, int damage, int damageType)
{
	for (GameEventListener *listener : listeners_)
		listener->OnPlayerTakeDamage(playerId, damage, damageType);
}

void GameEventBus::GameDataReloaded()
{
	for (GameEventListener *listener : listeners_)
		listener->OnGameDataReloaded();
}

void GameEventBus::GameDrawComplete()
{
	for (GameEventListener *listener : listeners_)
		listener->OnGameDrawComplete();
}

void GameEventBus::GameStart()
{
	for (GameEventListener *listener : listeners_)
		listener->OnGameStart();
}

} // namespace devilution
