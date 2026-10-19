/**
 * @file application/debug_commands.hpp
 *
 * Engine-neutral registry for native debug and administrative commands.
 */
#pragma once

#include <functional>
#include <map>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace devilution {

struct DebugCommandDefinition {
	std::string name;
	std::string description;
};

using DebugCommandHandler = std::function<std::string(std::string_view arguments)>;

/** Deterministic command registry shared by native debug frontends. */
class DebugCommandRegistry {
public:
	/** Registers a command, returning false for an invalid or duplicate name. */
	bool Register(std::string name, std::string description, DebugCommandHandler handler);

	/** Executes a complete command line, or returns nullopt when it is unknown. */
	[[nodiscard]] std::optional<std::string> Execute(std::string_view commandLine) const;

	/** Returns registered commands in ordinal name order. */
	[[nodiscard]] std::vector<DebugCommandDefinition> Definitions() const;

	/** Formats the registered commands for the native debug console. */
	[[nodiscard]] std::string HelpText() const;

private:
	struct RegisteredCommand {
		std::string description;
		DebugCommandHandler handler;
	};

	std::map<std::string, RegisteredCommand, std::less<>> commands;
};

extern DebugCommandRegistry CurrentDebugCommandRegistry;

} // namespace devilution
