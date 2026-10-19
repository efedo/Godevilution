/**
 * @file ui/debug_overlay/console_history.cpp
 *
 * Implementation of console command history.
 */


#include "ui/debug_overlay/console_history.hpp"

#include "utils/algorithm/container.hpp"

namespace devilution {

void DebugConsoleHistory::Push(std::string_view command)
{
	if (command.empty())
		return;

	auto existing = c_find(commands_, command);
	if (existing != commands_.end()) {
		commands_.erase(existing);
	}
	commands_.emplace_back(command);
	draft_.clear();
	index_ = -1;
}

std::string_view DebugConsoleHistory::Previous(std::string_view draft)
{
	if (commands_.empty())
		return draft;

	if (index_ == -1) {
		draft_ = draft;
		index_ = static_cast<int>(commands_.size()) - 1;
	} else if (index_ > 0) {
		--index_;
	}

	return commands_[index_];
}

std::string_view DebugConsoleHistory::Next()
{
	if (index_ == -1)
		return {};

	if (index_ + 1 >= static_cast<int>(commands_.size())) {
		index_ = -1;
		return draft_;
	}

	++index_;
	return commands_[index_];
}

} // namespace devilution
