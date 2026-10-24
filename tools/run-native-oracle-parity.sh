#!/usr/bin/env bash
# Generates the archive-free native oracle corpora with the open native test
# fixtures, then compares the managed implementation against them.
#
# Usage: tools/run-native-oracle-parity.sh <native-build-dir> <output-root>
#
# Every capture listed here runs only against DevilutionX's bundled assets and
# txtdata tables, except the MPQ reader oracle, which reads the freely
# redistributable spawn.mpq; none needs DIABDAT.MPQ, so all run in CI.
# Oracles that need the retail archive or retail-derived metadata stay local
# (see docs/ai/msvc-build.md). A skipped capture or managed test is a failure.
set -euo pipefail

if [[ $# -ne 2 ]]; then
	echo "usage: $0 <native-build-dir> <output-root>" >&2
	exit 2
fi
build_dir=$(cd "$1" && pwd)
repository_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
mkdir -p "$2"
output_root=$(cd "$2" && pwd)
case "$output_root/" in
"$repository_root"/*)
	echo "The output root must be outside the repository." >&2
	exit 2
	;;
esac
if [[ -n "$(ls -A "$output_root")" ]]; then
	echo "The output root must be empty." >&2
	exit 2
fi

# executable | gtest filter | oracle environment variable
captures=(
	"animationinfo_test|AnimationInfo.CaptureTimingOracle|GODEVILUTION_ANIMATION_TIMING_ORACLE_DIRECTORY"
	"player_animation_context_test|NativePlayerAnimationContext.CaptureContextOracle|GODEVILUTION_PLAYER_ANIMATION_CONTEXT_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeItemPlacementTest.CaptureIndependentSelectorsAndWorldCreation|GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeStaticLightingTest.CaptureZeroOffsetNativeStaticLighting|GODEVILUTION_THEME_STATIC_LIGHT_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureCompleteSelectedThemePass|GODEVILUTION_THEME_ROOM_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeItemPlacementTest.CaptureNativeChestOperations|GODEVILUTION_CATHEDRAL_CHEST_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureNativeBarrelOperations|GODEVILUTION_CATHEDRAL_BARREL_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureNativeSarcophagusOperations|GODEVILUTION_CATHEDRAL_SARCOPHAGUS_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureNativeTrapArrowBoundaries|GODEVILUTION_CATHEDRAL_TRAP_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureMonsterRuntimeMetadata|GODEVILUTION_CATHEDRAL_MONSTER_RUNTIME_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureCompleteCatacombsThemePass|GODEVILUTION_CATACOMBS_THEME_ROOM_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureNativeCatacombsChestOperations|GODEVILUTION_CATACOMBS_CHEST_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureNativeCatacombsRangedReleases|GODEVILUTION_CATACOMBS_RANGED_ORACLE_DIRECTORY"
	"theme_item_generation_test|ThemeRoomGenerationTest.CaptureNativeCatacombsArrowFlightAndImpact|GODEVILUTION_CATACOMBS_ARROW_FLIGHT_ORACLE_DIRECTORY"
	# Reads spawn.mpq from the build directory, or GODEVILUTION_MPQ_ORACLE_ARCHIVE when set.
	"native_mpq_oracle_test|NativeMpqOracle.CaptureReaderDigests|GODEVILUTION_MPQ_ORACLE_DIRECTORY"
)

# Managed consumers whose only external input is one of the corpora above.
domain_classes=(NativeAnimationTimingOracleTests)
diablo_classes=(
	NativePlayerAnimationContextOracleTests
	AuthoritativeCathedralThemeItemWorldNativeOracleTests
	AuthoritativeCathedralThemeStaticLightNativeOracleTests
	AuthoritativeCathedralThemeRoomNativeOracleTests
	AuthoritativeCathedralChestLootNativeOracleTests
	AuthoritativeCathedralBarrelNativeOracleTests
	AuthoritativeCathedralNativePopulationBarrelIntegrationTests
	AuthoritativeCathedralSarcophagusNativeOracleTests
	AuthoritativeCathedralNativeTrapArrowTests
	AuthoritativeCathedralMonsterRuntimeNativeOracleTests
	AuthoritativeCatacombsThemeRoomNativeOracleTests
	AuthoritativeCatacombsChestNativeOracleTests
	AuthoritativeNativeCatacombsRangedNativeOracleTests
	AuthoritativeNativeCatacombsArrowNativeOracleTests
)
client_classes=(NativeArchiveOracleTests)

for entry in "${captures[@]}"; do
	IFS='|' read -r executable filter variable <<<"$entry"
	directory="$output_root/${variable#GODEVILUTION_}"
	mkdir "$directory"
	log="$directory.log"
	echo "::group::capture $filter"
	started=$SECONDS
	if ! (cd "$build_dir" && env "$variable=$directory" "./$executable" "--gtest_filter=$filter") >"$log" 2>&1; then
		cat "$log"
		echo "::endgroup::"
		echo "Native capture failed: $filter" >&2
		exit 1
	fi
	if grep -q '\[  SKIPPED \]' "$log" || ! grep -q '\[  PASSED  \] 1 test' "$log"; then
		cat "$log"
		echo "::endgroup::"
		echo "Native capture did not run: $filter" >&2
		exit 1
	fi
	if [[ -z "$(ls -A "$directory")" ]]; then
		echo "::endgroup::"
		echo "Native capture wrote no oracle: $filter" >&2
		exit 1
	fi
	echo "$filter: $(du -sh "$directory" | cut -f1) in $((SECONDS - started))s"
	echo "::endgroup::"
	export "$variable=$directory"
done

filters() {
	for class in "$@"; do
		printf -- '--filter-class\n*.%s\n' "$class"
	done
}

cd "$repository_root"
mapfile -t domain_filters < <(filters "${domain_classes[@]}")
dotnet run --no-build --configuration Release \
	--project server/tests/Devilution.Domain.Tests/Devilution.Domain.Tests.csproj -- \
	"${domain_filters[@]}" --explicit on --fail-skips on
mapfile -t diablo_filters < <(filters "${diablo_classes[@]}")
dotnet run --no-build --configuration Release \
	--project server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj -- \
	"${diablo_filters[@]}" --explicit on --fail-skips on
mapfile -t client_filters < <(filters "${client_classes[@]}")
dotnet run --no-build --configuration Release \
	--project godot-client/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj -- \
	"${client_filters[@]}" --explicit on --fail-skips on
