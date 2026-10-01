using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** The route family represented by native-generated endpoint bindings. */
public enum AuthoritativeNativeGeneratedRouteKind
{
	Warp,
	Portal,
}

/** The endpoint of a native-generated route whose coordinate is being bound. */
public enum AuthoritativeNativeGeneratedRouteEndpoint
{
	Source,
	Destination,
}

/** Stable coordinate anchors permitted by a native-generated route binding. */
public enum AuthoritativeNativeGeneratedRouteAnchor
{
	UpTrigger,
	DownTrigger,
	MainEntry,
	PreviousEntry,
	TownCathedralTrigger,
	TownCathedralReturn,
}

/** One validated row from native_generated_route_bindings.tsv. */
public sealed record AuthoritativeNativeGeneratedRouteBinding(
	AuthoritativeNativeGeneratedRouteKind RouteKind,
	uint RouteId,
	string RouteSymbol,
	AuthoritativeNativeGeneratedRouteEndpoint Endpoint,
	uint LevelId,
	string LevelSymbol,
	AuthoritativeNativeGeneratedRouteAnchor Anchor);

/** Immutable parsed native-generated route binding table. */
public sealed class AuthoritativeNativeGeneratedRouteBindingCatalog
{
	internal AuthoritativeNativeGeneratedRouteBindingCatalog(
		string catalogFingerprint,
		IEnumerable<AuthoritativeNativeGeneratedRouteBinding> bindings)
	{
		CatalogFingerprint = catalogFingerprint;
		Bindings = new ReadOnlyCollection<AuthoritativeNativeGeneratedRouteBinding>(bindings.ToArray());
	}

	public string CatalogFingerprint { get; }

	public IReadOnlyList<AuthoritativeNativeGeneratedRouteBinding> Bindings { get; }
}

/** Immutable staged routes resolved against generated Cathedral and Town anchors. */
public sealed class AuthoritativeNativeGeneratedRouteBindingResolution
{
	internal AuthoritativeNativeGeneratedRouteBindingResolution(
		IEnumerable<AuthoritativeWarpTarget> warps,
		IEnumerable<AuthoritativePortal> portals,
		string routeFingerprint)
	{
		Warps = new ReadOnlyCollection<AuthoritativeWarpTarget>(warps.ToArray());
		Portals = new ReadOnlyCollection<AuthoritativePortal>(portals.ToArray());
		RouteFingerprint = routeFingerprint;
	}

	public IReadOnlyList<AuthoritativeWarpTarget> Warps { get; }

	public IReadOnlyList<AuthoritativePortal> Portals { get; }

	public string RouteFingerprint { get; }
}

/**
 * Parses and resolves the optional route coordinate table for generated native levels.
 * The resolver stages copied routes only; world and caller-owned route collections are never
 * mutated while validation is in progress.
 */
public static class AuthoritativeNativeGeneratedRouteBindings
{
	public const string TableName = "native_generated_route_bindings.tsv";

	public const int TownCathedralTriggerX = 25;
	public const int TownCathedralTriggerY = 29;
	public const int TownCathedralReturnX = 25;
	public const int TownCathedralReturnY = 31;

	private static readonly IReadOnlyList<string> RequiredColumns = [
		"route_kind",
		"route_id",
		"route_symbol",
		"endpoint",
		"level_id",
		"level_symbol",
		"anchor",
	];

	public static AuthoritativeNativeGeneratedRouteBindingCatalog Load(
		string contents,
		StableContentIdCatalog stableIds)
	{
		ArgumentNullException.ThrowIfNull(contents);
		ArgumentNullException.ThrowIfNull(stableIds);

		var table = TsvTable.Parse(TableName, contents);
		if (!table.Columns.SequenceEqual(RequiredColumns, StringComparer.Ordinal))
			throw new InvalidDataException(
				$"{TableName} must have the exact ordered header: {string.Join(',', RequiredColumns)}.");

		var bindings = new List<AuthoritativeNativeGeneratedRouteBinding>(table.Rows.Count);
		var keys = new HashSet<(AuthoritativeNativeGeneratedRouteKind Kind, uint Id, AuthoritativeNativeGeneratedRouteEndpoint Endpoint)>();
		foreach (var row in table.Rows)
		{
			var kind = ParseRouteKind(row);
			var routeId = ParseUInt(row, "route_id");
			var routeSymbol = RequiredText(row, "route_symbol");
			var endpoint = ParseEndpoint(row);
			var levelId = ParseUInt(row, "level_id");
			var levelSymbol = RequiredText(row, "level_symbol");
			var anchor = ParseAnchor(row);
			var routeNamespace = kind == AuthoritativeNativeGeneratedRouteKind.Warp ? "warp" : "portal";
			if (!stableIds.TryResolve(routeNamespace, routeSymbol, out var resolvedRouteId)
				|| resolvedRouteId != routeId)
				throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} does not match stable {routeNamespace} identity '{routeSymbol}'.");
			if (!stableIds.TryResolve("level", levelSymbol, out var resolvedLevelId)
				|| resolvedLevelId != levelId)
				throw new InvalidDataException(
					$"{TableName} row {row.LineNumber} does not match stable level identity '{levelSymbol}'.");
			if (routeId == 0)
				throw new InvalidDataException($"{TableName} row {row.LineNumber} has a zero route_id.");
			if (!keys.Add((kind, routeId, endpoint)))
				throw new InvalidDataException(
					$"{TableName} contains a duplicate {RouteKindText(kind)} {routeId} {EndpointText(endpoint)} binding.");

			bindings.Add(new AuthoritativeNativeGeneratedRouteBinding(
				kind,
				routeId,
				routeSymbol,
				endpoint,
				levelId,
				levelSymbol,
				anchor));
		}

		var fingerprint = Convert.ToHexString(SHA256.HashData(
			Encoding.UTF8.GetBytes(table.ToCanonicalTsv())));
		return new AuthoritativeNativeGeneratedRouteBindingCatalog(fingerprint, bindings);
	}

	public static AuthoritativeNativeGeneratedRouteBindingResolution Resolve(
		AuthoritativeWorld world,
		AuthoritativeLevel townLevel,
		AuthoritativeCathedralLevel1GenerationPlan plan,
		IEnumerable<AuthoritativeWarpTarget> warps,
		IEnumerable<AuthoritativePortal> portals,
		AuthoritativeNativeGeneratedRouteBindingCatalog bindings)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(townLevel);
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(warps);
		ArgumentNullException.ThrowIfNull(portals);
		ArgumentNullException.ThrowIfNull(bindings);

		ValidateTownLevel(world, townLevel);
		var generated = BuildGeneratedLevels(plan, townLevel);
		var bindingMap = bindings.Bindings.ToDictionary(
			binding => (binding.RouteKind, binding.RouteId, binding.Endpoint));
		var consumedBindings = new HashSet<(AuthoritativeNativeGeneratedRouteKind Kind, uint Id, AuthoritativeNativeGeneratedRouteEndpoint Endpoint)>();
		var resolvedRoutes = new List<ResolvedRouteInfo>();
		var resolvedWarps = new List<AuthoritativeWarpTarget>();
		var resolvedPortals = new List<AuthoritativePortal>();
		var routeIds = new HashSet<(AuthoritativeNativeGeneratedRouteKind Kind, uint Id)>();

		foreach (var warp in warps)
		{
			ArgumentNullException.ThrowIfNull(warp);
			if (!routeIds.Add((AuthoritativeNativeGeneratedRouteKind.Warp, warp.WarpId)))
				throw new InvalidDataException($"Warp {warp.WarpId} is present more than once.");
			var source = ResolveEndpoint(
				world,
				townLevel,
				generated,
				bindingMap,
				consumedBindings,
				AuthoritativeNativeGeneratedRouteKind.Warp,
				warp.WarpId,
				warp.StableSymbol,
				AuthoritativeNativeGeneratedRouteEndpoint.Source,
				warp.SourceLevelId,
				warp.TriggerX,
				warp.TriggerY);
			var destination = ResolveEndpoint(
				world,
				townLevel,
				generated,
				bindingMap,
				consumedBindings,
				AuthoritativeNativeGeneratedRouteKind.Warp,
				warp.WarpId,
				warp.StableSymbol,
				AuthoritativeNativeGeneratedRouteEndpoint.Destination,
				warp.TargetLevelId,
				warp.TargetX,
				warp.TargetY);
			resolvedWarps.Add(CloneWarp(warp, source.Position, destination.Position));
			resolvedRoutes.Add(new ResolvedRouteInfo(
				AuthoritativeNativeGeneratedRouteKind.Warp,
				warp.WarpId,
				warp.StableSymbol,
				source,
				destination,
				warp.RequiredQuestId,
				warp.RequiredQuestCompleted,
				warp.TargetSetLevelId,
				warp.TargetSetLevelActiveMap,
				warp.TargetSetLevelSymbol,
				0,
				0,
				false));
		}

		foreach (var portal in portals)
		{
			ArgumentNullException.ThrowIfNull(portal);
			if (!routeIds.Add((AuthoritativeNativeGeneratedRouteKind.Portal, portal.PortalId)))
				throw new InvalidDataException($"Portal {portal.PortalId} is present more than once.");
			var source = ResolveEndpoint(
				world,
				townLevel,
				generated,
				bindingMap,
				consumedBindings,
				AuthoritativeNativeGeneratedRouteKind.Portal,
				portal.PortalId,
				portal.StableSymbol,
				AuthoritativeNativeGeneratedRouteEndpoint.Source,
				portal.SourceLevelId,
				portal.SourcePositionX,
				portal.SourcePositionY);
			var destination = ResolveEndpoint(
				world,
				townLevel,
				generated,
				bindingMap,
				consumedBindings,
				AuthoritativeNativeGeneratedRouteKind.Portal,
				portal.PortalId,
				portal.StableSymbol,
				AuthoritativeNativeGeneratedRouteEndpoint.Destination,
				portal.DestinationLevelId,
				portal.DestinationPositionX,
				portal.DestinationPositionY);
			resolvedPortals.Add(portal with {
				SourcePositionX = source.Position.X,
				SourcePositionY = source.Position.Y,
				DestinationPositionX = destination.Position.X,
				DestinationPositionY = destination.Position.Y,
			});
			resolvedRoutes.Add(new ResolvedRouteInfo(
				AuthoritativeNativeGeneratedRouteKind.Portal,
				portal.PortalId,
				portal.StableSymbol,
				source,
				destination,
				0,
				false,
				portal.DestinationSetLevelId,
				portal.DestinationSetLevelActiveMap,
				portal.DestinationSetLevelSymbol,
				portal.RemainingTicks,
				portal.OwnerEntityId,
				portal.Bidirectional));
		}

		foreach (var binding in bindings.Bindings)
		{
			if (!consumedBindings.Contains((binding.RouteKind, binding.RouteId, binding.Endpoint)))
				throw new InvalidDataException(
					$"{TableName} contains a dangling {RouteKindText(binding.RouteKind)} {binding.RouteId} {EndpointText(binding.Endpoint)} binding.");
		}

		RequireGeneratedRoutes(generated, resolvedRoutes);
		var fingerprint = Fingerprint(resolvedRoutes, bindings.CatalogFingerprint);
		return new AuthoritativeNativeGeneratedRouteBindingResolution(
			resolvedWarps,
			resolvedPortals,
			fingerprint);
	}

	private static Dictionary<uint, GeneratedLevel> BuildGeneratedLevels(
		AuthoritativeCathedralLevel1GenerationPlan plan,
		AuthoritativeLevel townLevel)
	{
		var generated = new Dictionary<uint, GeneratedLevel>();
		foreach (var candidate in plan.Candidates)
		{
			if (!generated.TryAdd(candidate.Level.LevelId, new GeneratedLevel(candidate.Level, candidate.Routes)))
				throw new InvalidDataException($"Native Cathedral generation plan contains duplicate level {candidate.Level.LevelId}.");
			if (candidate.Level.LevelId == townLevel.LevelId)
				throw new InvalidDataException("Native Cathedral generation plan cannot replace the Town level.");
			if (candidate.Routes.UpTrigger == candidate.Routes.MainEntry)
				throw new InvalidDataException(
					$"Generated Cathedral level {candidate.Level.LevelId} has the UpTrigger at its MainEntry.");
		}
		return generated;
	}

	private static void ValidateTownLevel(AuthoritativeWorld world, AuthoritativeLevel townLevel)
	{
		if (townLevel.LevelId != AuthoritativePortalRules.TownLevelId
			|| string.IsNullOrWhiteSpace(townLevel.StableSymbol)
			|| !world.Levels.Any(level => ReferenceEquals(level, townLevel)))
			throw new InvalidDataException("Native generated route bindings require the registered ordinary Town level 0.");
		ValidatePoint(townLevel, TownCathedralTriggerX, TownCathedralTriggerY, "Town Cathedral trigger");
		ValidatePoint(townLevel, TownCathedralReturnX, TownCathedralReturnY, "Town Cathedral return");
	}

	private static EndpointResolution ResolveEndpoint(
		AuthoritativeWorld world,
		AuthoritativeLevel townLevel,
		IReadOnlyDictionary<uint, GeneratedLevel> generated,
		IReadOnlyDictionary<(AuthoritativeNativeGeneratedRouteKind Kind, uint Id, AuthoritativeNativeGeneratedRouteEndpoint Endpoint), AuthoritativeNativeGeneratedRouteBinding> bindingMap,
		ISet<(AuthoritativeNativeGeneratedRouteKind Kind, uint Id, AuthoritativeNativeGeneratedRouteEndpoint Endpoint)> consumedBindings,
		AuthoritativeNativeGeneratedRouteKind routeKind,
		uint routeId,
		string routeSymbol,
		AuthoritativeNativeGeneratedRouteEndpoint endpoint,
		uint actualLevelId,
		int actualX,
		int actualY)
	{
		if (routeId == 0)
			throw new InvalidDataException($"{RouteKindText(routeKind)} has a zero route ID.");
		var key = (routeKind, routeId, endpoint);
		bindingMap.TryGetValue(key, out var binding);
		if (binding is not null)
		{
			if (!string.Equals(binding.RouteSymbol, routeSymbol, StringComparison.Ordinal))
				throw new InvalidDataException(
					$"{RouteKindText(routeKind)} {routeId} does not match binding symbol '{binding.RouteSymbol}'.");
			if (binding.LevelId != actualLevelId)
				throw new InvalidDataException(
					$"{RouteKindText(routeKind)} {routeId} {EndpointText(endpoint)} binding targets level {binding.LevelId}, expected {actualLevelId}.");
			consumedBindings.Add(key);
		}

		if (generated.TryGetValue(actualLevelId, out var generatedLevel))
		{
			if (binding is null)
				throw new InvalidDataException(
					$"Generated level {actualLevelId} requires an explicit {RouteKindText(routeKind)} {routeId} {EndpointText(endpoint)} binding.");
			if (!string.Equals(binding.LevelSymbol, generatedLevel.Level.StableSymbol, StringComparison.Ordinal))
				throw new InvalidDataException(
					$"Generated level {actualLevelId} binding has level symbol '{binding.LevelSymbol}', expected '{generatedLevel.Level.StableSymbol}'.");
			var position = GeneratedAnchor(generatedLevel, binding.Anchor, routeId, endpoint);
			ValidatePoint(generatedLevel.Level, position.X, position.Y, "Generated route anchor");
			return new EndpointResolution(actualLevelId, generatedLevel.Level.StableSymbol, position, binding.Anchor);
		}

		if (actualLevelId == townLevel.LevelId)
		{
			if (binding is null)
			{
				ValidateWorldPoint(world, actualLevelId, actualX, actualY, "Unbound Town route endpoint");
				return new EndpointResolution(actualLevelId, townLevel.StableSymbol, new AuthoritativeCathedralPoint(actualX, actualY), null);
			}
			if (!string.Equals(binding.LevelSymbol, townLevel.StableSymbol, StringComparison.Ordinal))
				throw new InvalidDataException(
					$"Town route binding has level symbol '{binding.LevelSymbol}', expected '{townLevel.StableSymbol}'.");
			var position = TownAnchor(binding.Anchor, routeId, endpoint);
			ValidatePoint(townLevel, position.X, position.Y, "Town route anchor");
			return new EndpointResolution(actualLevelId, townLevel.StableSymbol, position, binding.Anchor);
		}

		if (binding is not null)
			throw new InvalidDataException(
				$"Non-generated route endpoint level {actualLevelId} may only use Town-specific anchors.");
		var level = world.Levels.SingleOrDefault(candidate => candidate.LevelId == actualLevelId)
			?? throw new InvalidDataException($"Route endpoint references unknown level {actualLevelId}.");
		ValidatePoint(level, actualX, actualY, "Unbound route endpoint");
		return new EndpointResolution(actualLevelId, level.StableSymbol, new AuthoritativeCathedralPoint(actualX, actualY), null);
	}

	private static AuthoritativeCathedralPoint GeneratedAnchor(
		GeneratedLevel generated,
		AuthoritativeNativeGeneratedRouteAnchor anchor,
		uint routeId,
		AuthoritativeNativeGeneratedRouteEndpoint endpoint)
	{
		return anchor switch
		{
			AuthoritativeNativeGeneratedRouteAnchor.UpTrigger => generated.Routes.UpTrigger,
			AuthoritativeNativeGeneratedRouteAnchor.DownTrigger => generated.Routes.DownTrigger,
			AuthoritativeNativeGeneratedRouteAnchor.MainEntry => generated.Routes.MainEntry,
			AuthoritativeNativeGeneratedRouteAnchor.PreviousEntry => generated.Routes.PreviousEntry,
			_ => throw new InvalidDataException(
				$"Generated route {routeId} {EndpointText(endpoint)} cannot use Town anchor '{AnchorText(anchor)}'."),
		};
	}

	private static AuthoritativeCathedralPoint TownAnchor(
		AuthoritativeNativeGeneratedRouteAnchor anchor,
		uint routeId,
		AuthoritativeNativeGeneratedRouteEndpoint endpoint)
	{
		return anchor switch
		{
			AuthoritativeNativeGeneratedRouteAnchor.TownCathedralTrigger => new AuthoritativeCathedralPoint(TownCathedralTriggerX, TownCathedralTriggerY),
			AuthoritativeNativeGeneratedRouteAnchor.TownCathedralReturn => new AuthoritativeCathedralPoint(TownCathedralReturnX, TownCathedralReturnY),
			_ => throw new InvalidDataException(
				$"Town route {routeId} {EndpointText(endpoint)} requires a Town-specific anchor."),
		};
	}

	private static void RequireGeneratedRoutes(
		IReadOnlyDictionary<uint, GeneratedLevel> generated,
		IEnumerable<ResolvedRouteInfo> routes)
	{
		var values = routes.ToArray();
		foreach (var (levelId, generatedLevel) in generated)
		{
			var hasMainRoute = values.Any(route =>
				route.Kind == AuthoritativeNativeGeneratedRouteKind.Warp
				&& route.Source.LevelId == AuthoritativePortalRules.TownLevelId
				&& route.Destination.LevelId == levelId
				&& route.Source.Anchor == AuthoritativeNativeGeneratedRouteAnchor.TownCathedralTrigger
				&& route.Destination.Anchor == AuthoritativeNativeGeneratedRouteAnchor.MainEntry
				&& IsUnconditionalOrdinaryWarp(route));
            var hasReturnRoute = values.Any(route =>
                route.Kind == AuthoritativeNativeGeneratedRouteKind.Warp
                && route.Source.LevelId == levelId
				&& route.Destination.LevelId == AuthoritativePortalRules.TownLevelId
				&& route.Source.Anchor == AuthoritativeNativeGeneratedRouteAnchor.UpTrigger
				&& route.Destination.Anchor == AuthoritativeNativeGeneratedRouteAnchor.TownCathedralReturn
				&& IsUnconditionalOrdinaryWarp(route));
			if (!hasMainRoute || !hasReturnRoute)
				throw new InvalidDataException(
					$"Generated Cathedral level {levelId} requires explicit Town-to-Main and UpTrigger-to-TownReturn routes.");
			if (generatedLevel.Routes.UpTrigger == generatedLevel.Routes.MainEntry)
				throw new InvalidDataException($"Generated Cathedral level {levelId} has a non-distinct trigger and entry.");
		}
	}

	private static bool IsUnconditionalOrdinaryWarp(ResolvedRouteInfo route)
		=> route.RequiredQuestId == 0
            && !route.RequiredQuestCompleted
            && route.SetLevelId == -1
            && route.SetLevelSymbol.Length == 0;

	private static AuthoritativeWarpTarget CloneWarp(
		AuthoritativeWarpTarget source,
		AuthoritativeCathedralPoint trigger,
		AuthoritativeCathedralPoint target)
	{
		return new AuthoritativeWarpTarget(
			source.WarpId,
			source.SourceLevelId,
			trigger.X,
			trigger.Y,
			source.TargetLevelId,
			target.X,
			target.Y,
			source.RequiredQuestId,
			source.RequiredQuestCompleted,
			source.TargetSetLevelId,
			source.TargetSetLevelActiveMap) {
			TargetSetLevelSymbol = source.TargetSetLevelSymbol,
			StableSymbol = source.StableSymbol,
		};
	}

	private static string Fingerprint(IEnumerable<ResolvedRouteInfo> routes, string catalogFingerprint)
	{
		var semanticRows = routes
			.OrderBy(route => route.Kind)
			.ThenBy(route => route.RouteId)
			.Select(RouteFingerprintRow)
			.ToArray();
		return AuthoritativeCathedralLevel1RuntimeCatalog.HashSequence(
			"native-generated-routes:" + catalogFingerprint,
			semanticRows);
	}

	private static string RouteFingerprintRow(ResolvedRouteInfo route)
	{
		var fields = new[] {
			RouteKindText(route.Kind),
			route.RouteId.ToString(CultureInfo.InvariantCulture),
			route.RouteSymbol,
			AnchorText(route.Source.Anchor),
			route.Source.LevelId.ToString(CultureInfo.InvariantCulture),
			route.Source.LevelSymbol,
			route.Source.Position.X.ToString(CultureInfo.InvariantCulture),
			route.Source.Position.Y.ToString(CultureInfo.InvariantCulture),
			AnchorText(route.Destination.Anchor),
			route.Destination.LevelId.ToString(CultureInfo.InvariantCulture),
			route.Destination.LevelSymbol,
			route.Destination.Position.X.ToString(CultureInfo.InvariantCulture),
			route.Destination.Position.Y.ToString(CultureInfo.InvariantCulture),
			route.RequiredQuestId.ToString(CultureInfo.InvariantCulture),
			route.RequiredQuestCompleted ? "completed" : "active",
			route.SetLevelId.ToString(CultureInfo.InvariantCulture),
			route.SetLevelActiveMap ? "active" : "inactive",
			route.SetLevelSymbol,
			route.RemainingTicks.ToString(CultureInfo.InvariantCulture),
			route.OwnerEntityId.ToString(CultureInfo.InvariantCulture),
			route.Bidirectional ? "bidirectional" : "one-way",
		};
		return string.Join('|', fields.Select(FingerprintField));
	}

	private static string FingerprintField(string value)
		=> value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;

	private static void ValidatePoint(AuthoritativeLevel level, int x, int y, string name)
	{
		if (!level.ContainsPosition(x, y) || !level.IsWalkable(x, y))
			throw new InvalidDataException(
				$"{name} ({x}, {y}) is outside level {level.LevelId} or is blocked.");
	}

	private static void ValidateWorldPoint(AuthoritativeWorld world, uint levelId, int x, int y, string name)
	{
		if (!world.ContainsPosition(levelId, x, y) || !world.IsWalkable(levelId, x, y))
			throw new InvalidDataException($"{name} ({x}, {y}) is outside level {levelId} or is blocked.");
	}

	private static string RequiredText(TsvRow row, string column)
	{
		var value = row.Required(column).Trim();
		if (value.Length == 0)
			throw new InvalidDataException($"{TableName} row {row.LineNumber} has a blank {column}.");
		return value;
	}

	private static uint ParseUInt(TsvRow row, string column)
	{
		var value = RequiredText(row, column);
		if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			throw new InvalidDataException($"{TableName} row {row.LineNumber} has an invalid {column} '{value}'.");
		return result;
	}

	private static AuthoritativeNativeGeneratedRouteKind ParseRouteKind(TsvRow row)
	{
		return RequiredText(row, "route_kind") switch
		{
			"warp" => AuthoritativeNativeGeneratedRouteKind.Warp,
			"portal" => AuthoritativeNativeGeneratedRouteKind.Portal,
			_ => throw new InvalidDataException($"{TableName} row {row.LineNumber} has an unknown route_kind."),
		};
	}

	private static AuthoritativeNativeGeneratedRouteEndpoint ParseEndpoint(TsvRow row)
	{
		return RequiredText(row, "endpoint") switch
		{
			"source" => AuthoritativeNativeGeneratedRouteEndpoint.Source,
			"destination" => AuthoritativeNativeGeneratedRouteEndpoint.Destination,
			_ => throw new InvalidDataException($"{TableName} row {row.LineNumber} has an unknown endpoint."),
		};
	}

	private static AuthoritativeNativeGeneratedRouteAnchor ParseAnchor(TsvRow row)
	{
		return RequiredText(row, "anchor") switch
		{
			"up_trigger" => AuthoritativeNativeGeneratedRouteAnchor.UpTrigger,
			"down_trigger" => AuthoritativeNativeGeneratedRouteAnchor.DownTrigger,
			"main_entry" => AuthoritativeNativeGeneratedRouteAnchor.MainEntry,
			"previous_entry" => AuthoritativeNativeGeneratedRouteAnchor.PreviousEntry,
			"town_cathedral_trigger" => AuthoritativeNativeGeneratedRouteAnchor.TownCathedralTrigger,
			"town_cathedral_return" => AuthoritativeNativeGeneratedRouteAnchor.TownCathedralReturn,
			_ => throw new InvalidDataException($"{TableName} row {row.LineNumber} has an unknown anchor."),
		};
	}

	private static string RouteKindText(AuthoritativeNativeGeneratedRouteKind kind)
		=> kind == AuthoritativeNativeGeneratedRouteKind.Warp ? "warp" : "portal";

	private static string EndpointText(AuthoritativeNativeGeneratedRouteEndpoint endpoint)
		=> endpoint == AuthoritativeNativeGeneratedRouteEndpoint.Source ? "source" : "destination";

	private static string AnchorText(AuthoritativeNativeGeneratedRouteAnchor anchor)
		=> anchor switch
		{
			AuthoritativeNativeGeneratedRouteAnchor.UpTrigger => "up_trigger",
			AuthoritativeNativeGeneratedRouteAnchor.DownTrigger => "down_trigger",
			AuthoritativeNativeGeneratedRouteAnchor.MainEntry => "main_entry",
			AuthoritativeNativeGeneratedRouteAnchor.PreviousEntry => "previous_entry",
			AuthoritativeNativeGeneratedRouteAnchor.TownCathedralTrigger => "town_cathedral_trigger",
			AuthoritativeNativeGeneratedRouteAnchor.TownCathedralReturn => "town_cathedral_return",
			_ => throw new InvalidDataException("Unknown native generated route anchor."),
		};

	private static string AnchorText(AuthoritativeNativeGeneratedRouteAnchor? anchor)
		=> anchor is { } value ? AnchorText(value) : "none";

	private sealed record GeneratedLevel(
		AuthoritativeLevel Level,
		AuthoritativeCathedralLevel1RoutePoints Routes);

	private readonly record struct EndpointResolution(
		uint LevelId,
		string LevelSymbol,
		AuthoritativeCathedralPoint Position,
		AuthoritativeNativeGeneratedRouteAnchor? Anchor);

	private sealed record ResolvedRouteInfo(
		AuthoritativeNativeGeneratedRouteKind Kind,
		uint RouteId,
		string RouteSymbol,
		EndpointResolution Source,
		EndpointResolution Destination,
		uint RequiredQuestId,
		bool RequiredQuestCompleted,
		int SetLevelId,
		bool SetLevelActiveMap,
		string SetLevelSymbol,
		uint RemainingTicks,
		uint OwnerEntityId,
		bool Bidirectional);
}
