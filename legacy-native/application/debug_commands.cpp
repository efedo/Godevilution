/**
 * @file application/debug_commands.cpp
 *
 * Implementation of the native debug-command registry.
 */

#include "application/debug_commands.hpp"

#include <cctype>

namespace devilution {
namespace {

std::string_view TrimWhitespace(std::string_view value)
{
	while (!value.empty() && std::isspace(static_cast<unsigned char>(value.front())) != 0)
		value.remove_prefix(1);
	while (!value.empty() && std::isspace(static_cast<unsigned char>(value.back())) != 0)
		value.remove_suffix(1);
	return value;
}

} // namespace

DebugCommandRegistry CurrentDebugCommandRegistry;

bool DebugCommandRegistry::Register(std::string name, std::string description, DebugCommandHandler handler)
{
	if (name.empty() || !handler)
		return false;
	return commands.emplace(std::move(name), RegisteredCommand { std::move(description), std::move(handler) }).second;
}

std::optional<std::string> DebugCommandRegistry::Execute(std::string_view commandLine) const
{
	commandLine = TrimWhitespace(commandLine);
	if (commandLine.empty())
		return std::nullopt;

	const size_t separator = commandLine.find_first_of(" \t\r\n");
	const std::string_view name = commandLine.substr(0, separator);
	const auto command = commands.find(name);
	if (command == commands.end())
		return std::nullopt;

	const std::string_view arguments = separator == std::string_view::npos
	    ? std::string_view {}
	    : TrimWhitespace(commandLine.substr(separator + 1));
	return command->second.handler(arguments);
}

std::vector<DebugCommandDefinition> DebugCommandRegistry::Definitions() const
{
	std::vector<DebugCommandDefinition> result;
	result.reserve(commands.size());
	for (const auto &[name, command] : commands)
		result.push_back(DebugCommandDefinition { name, command.description });
	return result;
}

std::string DebugCommandRegistry::HelpText() const
{
	std::string result = "Native debug commands:";
	for (const DebugCommandDefinition &command : Definitions()) {
		result.append("\n  ");
		result.append(command.name);
		if (!command.description.empty()) {
			result.append(" - ");
			result.append(command.description);
		}
	}
	return result;
}

} // namespace devilution
