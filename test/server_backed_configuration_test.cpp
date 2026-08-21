#include <string>
#include <string_view>
#include <tuple>

#include <gtest/gtest.h>

#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_runtime.hpp"

namespace devilution::authoritative {
namespace {

TEST(ServerBackedConfiguration, IsDisabledByDefault)
{
	const ServerBackedRuntimeConfiguration configuration;

	EXPECT_EQ(configuration.mode, ServerBackedRuntimeConfiguration::GameMode::Legacy);
	EXPECT_FALSE(configuration.enabled);
	EXPECT_EQ(configuration.host, "127.0.0.1");
	EXPECT_EQ(configuration.port, 6113);
	EXPECT_EQ(configuration.clientBuildId, "devilutionx-client");
	EXPECT_EQ(configuration.protocolSchemaVersion, "0.2.0");
	EXPECT_TRUE(configuration.contentManifestHash.empty());
	EXPECT_TRUE(configuration.rulesetIdentityHash.empty());
	EXPECT_EQ(configuration.diagnosticsDirectory, "authoritative-diagnostics");
}

TEST(ServerBackedRuntime, DisabledStartPreservesLocalPath)
{
	StoreManager store;
	ServerBackedRuntime runtime;
	ServerBackedRuntimeConfiguration configuration;

	auto result = runtime.Start(configuration, store);
	ASSERT_TRUE(result.has_value()) << result.error();
	EXPECT_FALSE(runtime.IsConnected());
}

TEST(ServerBackedRuntime, StashRemainsDisabledUntilItHasAnAuthoritativeProjection)
{
	ServerBackedRuntime runtime;

	EXPECT_FALSE(runtime.SupportsStash());
}

TEST(ServerBackedRuntime, SnapshotDrivenPortalUseRequiresConnection)
{
	ServerBackedRuntime runtime;

	auto result = runtime.UsePortalAt(57, 40, 0, 0);
	ASSERT_FALSE(result.has_value());
	EXPECT_NE(result.error().find("not connected"), std::string::npos);
}

TEST(ServerBackedRuntime, MissingContentIdentityFailsBeforeConnecting)
{
	StoreManager store;
	ServerBackedRuntime runtime;
	ServerBackedRuntimeConfiguration configuration;
	configuration.enabled = true;

	auto result = runtime.Start(configuration, store);
	ASSERT_FALSE(result.has_value());
	EXPECT_NE(result.error().find("content identity"), std::string::npos);
	EXPECT_FALSE(runtime.IsConnected());
}

TEST(ServerBackedRuntime, ProtocolV2RequiresRulesetIdentity)
{
	StoreManager store;
	ServerBackedRuntime runtime;
	ServerBackedRuntimeConfiguration configuration;
	configuration.enabled = true;
	configuration.contentManifestHash = "content";

	auto result = runtime.Start(configuration, store);
	ASSERT_FALSE(result.has_value());
	EXPECT_NE(result.error().find("ruleset identity"), std::string::npos);
	EXPECT_FALSE(runtime.IsConnected());
}

class ValidServerEndpointTest : public testing::TestWithParam<std::tuple<std::string_view, std::string_view, uint16_t>> {
};

TEST_P(ValidServerEndpointTest, ParsesAndEnablesConfiguration)
{
	const auto &[endpoint, expectedHost, expectedPort] = GetParam();

	const auto configuration = ParseServerEndpoint(endpoint);

	ASSERT_TRUE(configuration.has_value()) << configuration.error();
	EXPECT_TRUE(configuration->enabled);
	EXPECT_EQ(configuration->mode, ServerBackedRuntimeConfiguration::GameMode::Authoritative);
	EXPECT_EQ(configuration->host, expectedHost);
	EXPECT_EQ(configuration->port, expectedPort);
}

INSTANTIATE_TEST_SUITE_P(
    HostnameIPv4AndIPv6,
    ValidServerEndpointTest,
    testing::Values(
        std::tuple { "localhost:6113", "localhost", 6113 },
        std::tuple { "server.example.com:1", "server.example.com", 1 },
        std::tuple { "game-server.internal:65535", "game-server.internal", 65535 },
        std::tuple { "127.0.0.1:6113", "127.0.0.1", 6113 },
        std::tuple { "192.168.10.25:443", "192.168.10.25", 443 },
        std::tuple { "[::1]:6113", "::1", 6113 },
        std::tuple { "[2001:db8::1]:12345", "2001:db8::1", 12345 },
        std::tuple { "[fe80::1%3]:65535", "fe80::1%3", 65535 }));

class InvalidServerEndpointTest : public testing::TestWithParam<std::string_view> {
};

TEST_P(InvalidServerEndpointTest, RejectsMalformedEndpoint)
{
	const auto configuration = ParseServerEndpoint(GetParam());

	ASSERT_FALSE(configuration.has_value()) << GetParam();
	EXPECT_FALSE(configuration.error().empty());
}

INSTANTIATE_TEST_SUITE_P(
    MissingOrInvalidComponents,
    InvalidServerEndpointTest,
    testing::Values(
        "",
        "localhost",
        "localhost:",
        ":6113",
        "localhost:0",
        "localhost:65536",
        "localhost:-1",
        "localhost:+6113",
        "localhost:abc",
        "localhost:61x3",
        "localhost:6113extra",
        "localhost: 6113",
        "localhost:6113 ",
        "local host:6113",
        "-localhost:6113",
        "localhost-:6113",
        "host..example:6113",
        ".localhost:6113",
        "localhost.:6113",
        "127.0.0:6113",
        "127.0.0.256:6113",
        "127..0.1:6113",
        "127.0.0.1:6113:7",
        "::1:6113",
        "[::1]",
        "[::1]:",
        "[::1]6113",
        "[::1:6113",
        "[]:6113",
        "[not-ipv6]:6113",
        "[2001:db8::1]]:6113",
        "[2001:db8::1]:6113:7",
        "[2001:db8::1]:0",
        "[2001:db8::1]:65536"));

} // namespace
} // namespace devilution::authoritative
