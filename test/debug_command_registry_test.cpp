#include "application/debug_commands.hpp"

#include <gtest/gtest.h>

namespace devilution {
namespace {

TEST(DebugCommandRegistry, ExecutesTrimmedCommandAndArguments)
{
	DebugCommandRegistry registry;
	ASSERT_TRUE(registry.Register("echo", "Echo arguments.", [](std::string_view arguments) {
		return std::string(arguments);
	}));

	const auto result = registry.Execute("  echo   hello world  ");
	ASSERT_TRUE(result.has_value());
	EXPECT_EQ(*result, "hello world");
}

TEST(DebugCommandRegistry, RejectsDuplicateAndInvalidRegistrations)
{
	DebugCommandRegistry registry;
	EXPECT_FALSE(registry.Register("", "invalid", [](std::string_view) { return std::string {}; }));
	EXPECT_FALSE(registry.Register("missing-handler", "invalid", {}));
	ASSERT_TRUE(registry.Register("same", "first", [](std::string_view) { return std::string("first"); }));
	EXPECT_FALSE(registry.Register("same", "second", [](std::string_view) { return std::string("second"); }));
}

TEST(DebugCommandRegistry, ListsCommandsInOrdinalOrderAndLeavesUnknownCommandsUnchanged)
{
	DebugCommandRegistry registry;
	ASSERT_TRUE(registry.Register("zeta", "Z", [](std::string_view) { return std::string {}; }));
	ASSERT_TRUE(registry.Register("alpha", "A", [](std::string_view) { return std::string {}; }));

	const auto definitions = registry.Definitions();
	ASSERT_EQ(definitions.size(), 2U);
	EXPECT_EQ(definitions[0].name, "alpha");
	EXPECT_EQ(definitions[1].name, "zeta");
	EXPECT_FALSE(registry.Execute("unknown").has_value());
}

TEST(DebugCommandRegistry, FormatsNativeHelpFromRegisteredCommands)
{
	DebugCommandRegistry registry;
	ASSERT_TRUE(registry.Register("zeta", "Z command.", [](std::string_view) { return std::string {}; }));
	ASSERT_TRUE(registry.Register("alpha", "A command.", [](std::string_view) { return std::string {}; }));

	EXPECT_EQ(
	    registry.HelpText(),
	    "Native debug commands:\n"
	    "  alpha - A command.\n"
	    "  zeta - Z command.");
}

} // namespace
} // namespace devilution
