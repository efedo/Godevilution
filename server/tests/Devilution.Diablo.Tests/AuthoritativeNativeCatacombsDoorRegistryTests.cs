using Devilution.Engine;
using static Devilution.Diablo.Tests.AuthoritativeCathedralNativePopulationIntegrationTests;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Exercises the live door registry only after the qualified C2 owner is fully built. */
public sealed class AuthoritativeNativeCatacombsDoorRegistryTests
{
	[Fact(Explicit = true)]
	public void GeneratedOwnerBindsOperatesAndRetainsNativeDoorOccupancy()
	{
		using var fixture = CreateFixture(0);
		var build = fixture.CreateBuild(5, 700);
		var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
		var owner = build.Policy.Bindings;
		var catalog = build.DoorCatalog;
		var initialFloor = Assert.Single(world.CaptureNativeCatacombsDoorStates());

		Assert.True(catalog.Supports(fixture.Metadata));
		Assert.Equal(catalog.Fingerprint, build.Policy.DoorCatalogFingerprint);
		Assert.Equal(catalog.Fingerprint, owner.CatacombsDoorCatalogFingerprint);
		Assert.Equal(700U, initialFloor.LevelId);
		Assert.Equal(5, initialFloor.Depth);
		Assert.Equal(build.Policy.InitialDoorCount, initialFloor.Doors.Count);
		Assert.NotEmpty(initialFloor.Doors);
		Assert.All(build.Objects.Where(value => value.NativeObjectId is 42 or 43), value =>
			Assert.Null(value.CathedralDoor));

		var rawRows = build.Policy.InitialState.ActiveObjectIds
			.Take(build.Policy.InitialState.ActiveObjectCount)
			.Select(slot => build.Policy.InitialState.ObjectSlots[slot])
			.Where(raw => raw.DoorFlag)
			.ToArray();
		Assert.Equal(build.Policy.InitialDoorCount, rawRows.Length);
		foreach (var raw in rawRows) {
			var entityId = owner.ObjectEntityAt(raw.Position.X, raw.Position.Y);
			Assert.True(entityId.HasValue);
			Assert.True(world.TryGetNativeCatacombsDoorState(entityId!.Value, out var bound));
			Assert.Equal((uint)raw.Type, bound.NativeObjectId);
			Assert.Equal(700U, bound.LevelId);
			Assert.Equal(raw.Position.X, bound.PositionX);
			Assert.Equal(raw.Position.Y, bound.PositionY);
			Assert.Equal(raw.Var4, (int)bound.Status);
			Assert.Equal((int)raw.AnimFrame, bound.AnimationFrame);
			Assert.Equal(raw.SelectionRegion, bound.SelectionRegion);
			Assert.Equal(raw.PreFlag, bound.PreFlag);
			Assert.Equal(raw.MissilePassable, bound.MissFlag);
			Assert.Null(world.Objects[entityId.Value].CathedralDoor);
		}

		var availableDoor = initialFloor.Doors.First(door =>
			!world.CombatTargets.Values.Any(actor => actor.LevelId == door.LevelId
				&& (actor.PositionX == door.PositionX && actor.PositionY == door.PositionY
					|| actor.IsMoving && actor.FuturePositionX == door.PositionX && actor.FuturePositionY == door.PositionY))
			&& !world.Corpses.Values.Any(corpse => corpse.LevelId == door.LevelId
				&& corpse.PositionX == door.PositionX && corpse.PositionY == door.PositionY)
			&& !world.WorldItems.Values.Any(item => item.LevelId == door.LevelId
				&& item.PositionX == door.PositionX && item.PositionY == door.PositionY));
		var entity = availableDoor.EntityId;
		var cell = availableDoor.PositionY * initialFloor.EffectiveGeometry.Width + availableDoor.PositionX;
		var openPiece = availableDoor.Kind == AuthoritativeCatacombsDoorKind.Left
			? AuthoritativeCatacombsDoors.OpenLeftPiece : AuthoritativeCatacombsDoors.OpenRightPiece;
		var openSpecial = availableDoor.Kind == AuthoritativeCatacombsDoorKind.Left
			? AuthoritativeCatacombsDoors.OpenLeftSpecial : AuthoritativeCatacombsDoors.OpenRightSpecial;
		var closedPiece = availableDoor.Kind == AuthoritativeCatacombsDoorKind.Left
			? AuthoritativeCatacombsDoors.ClosedLeftPiece : AuthoritativeCatacombsDoors.ClosedRightPiece;
		var closedFrame = availableDoor.AnimationFrame;
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Closed, availableDoor.Status);

		Assert.True(world.TryOperateNativeCatacombsDoor(entity, []));
		var opened = ReadDoor(world, entity);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, opened.Door.Status);
		Assert.Equal(closedFrame + 2, opened.Door.AnimationFrame);
		Assert.Equal(openPiece, opened.Geometry.NativePieceLayer[cell]);
		Assert.Equal(openSpecial, opened.Geometry.NativeSpecialLayer[cell]);

		var adjacentX = availableDoor.PositionX > 0 ? availableDoor.PositionX - 1 : availableDoor.PositionX + 1;
		var directionX = adjacentX < availableDoor.PositionX ? 1 : -1;
		var movingPlayer = CreatePlayer(0xF1000001, 700, adjacentX,
			availableDoor.PositionY, 0, new AuthoritativePlayerActionState(
				DiabloPlayerActionKind.Move, 1, 1, 2, 3) {
				DirectionX = directionX,
			});
		Assert.Equal((availableDoor.PositionX, availableDoor.PositionY),
			(movingPlayer.FuturePositionX, movingPlayer.FuturePositionY));
		world.UpdateNativeCatacombsDoorOccupancy([movingPlayer]);
		var blocked = ReadDoor(world, entity);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Blocked, blocked.Door.Status);
		Assert.Equal(opened.Door.AnimationFrame, blocked.Door.AnimationFrame);
		Assert.Equal(openPiece, blocked.Geometry.NativePieceLayer[cell]);
		Assert.Equal(openSpecial, blocked.Geometry.NativeSpecialLayer[cell]);
		Assert.True(world.TryOperateNativeCatacombsDoor(entity, [movingPlayer]));
		Assert.Equal(blocked.Door, ReadDoor(world, entity).Door);

		var clearCell = Enumerable.Range(0, initialFloor.EffectiveGeometry.Width * initialFloor.EffectiveGeometry.Height)
			.Select(index => (X: index % initialFloor.EffectiveGeometry.Width,
				Y: index / initialFloor.EffectiveGeometry.Width))
			.First(position => initialFloor.Doors.All(door =>
				door.PositionX != position.X || door.PositionY != position.Y));
		var clearPlayer = CreatePlayer(0xF1000002, 700, clearCell.X, clearCell.Y, 0);
		world.UpdateNativeCatacombsDoorOccupancy([clearPlayer]);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, ReadDoor(world, entity).Door.Status);
		world.ValidateNativePopulationState([]);
		Assert.True(world.TryOperateNativeCatacombsDoor(entity, []));
		var reclosed = ReadDoor(world, entity);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Closed, reclosed.Door.Status);
		Assert.Equal(closedFrame, reclosed.Door.AnimationFrame);
		Assert.Equal(closedPiece, reclosed.Geometry.NativePieceLayer[cell]);
		Assert.Equal((ushort)0, reclosed.Geometry.NativeSpecialLayer[cell]);

		Assert.True(world.TryOperateNativeCatacombsDoor(entity, []));
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, ReadDoor(world, entity).Door.Status);

		var movingActorId = 0xF2000001U;
		world.CombatTargets.Add(movingActorId, new AuthoritativeCombatTarget(
			movingActorId, adjacentX, availableDoor.PositionY, 100,
			maxHitPoints: 100, levelId: 700, moving: true,
			futurePositionX: availableDoor.PositionX, futurePositionY: availableDoor.PositionY));
		AssertBlockedByOccupant(world, entity, clearPlayer);
		world.CombatTargets.Remove(movingActorId);
		world.UpdateNativeCatacombsDoorOccupancy([clearPlayer]);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, ReadDoor(world, entity).Door.Status);

		var deathActorId = 0xF2000002U;
		var deathActor = new AuthoritativeCombatTarget(deathActorId,
			availableDoor.PositionX, availableDoor.PositionY, 0, maxHitPoints: 100,
			levelId: 700, deathAnimationTicks: 8);
		deathActor.RestoreDeathAnimation(3);
		Assert.True(deathActor.DeathTicksRemaining > 0);
		world.CombatTargets.Add(deathActorId, deathActor);
		AssertBlockedByOccupant(world, entity, clearPlayer);
		world.CombatTargets.Remove(deathActorId);
		world.UpdateNativeCatacombsDoorOccupancy([clearPlayer]);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, ReadDoor(world, entity).Door.Status);

		const uint corpseId = 0xF3000001;
		world.Corpses.Add(corpseId, new AuthoritativeCorpse(corpseId, 700,
			availableDoor.PositionX, availableDoor.PositionY));
		AssertBlockedByOccupant(world, entity, clearPlayer);
		world.Corpses.Remove(corpseId);
		world.UpdateNativeCatacombsDoorOccupancy([clearPlayer]);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, ReadDoor(world, entity).Door.Status);

		const uint itemId = 0xF4000001;
		var itemState = fixture.StoreItems.First();
		world.WorldItems.Add(itemId, new AuthoritativeWorldItem(itemId, 700,
			availableDoor.PositionX, availableDoor.PositionY, 1, 0, itemState));
		AssertBlockedByOccupant(world, entity, clearPlayer);
		var beforeOffFloorUpdate = ReadDoor(world, entity);
		world.WorldItems.Remove(itemId);
		var townPlayer = CreatePlayer(0xF1000003, 0, 20, 20, 0);
		world.UpdateNativeCatacombsDoorOccupancy([townPlayer]);
		var retainedOffFloor = ReadDoor(world, entity);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Blocked, retainedOffFloor.Door.Status);
		Assert.Equal(beforeOffFloorUpdate.Door, retainedOffFloor.Door);
		Assert.Equal(beforeOffFloorUpdate.Geometry.NativePieceLayer, retainedOffFloor.Geometry.NativePieceLayer);
		Assert.Equal(beforeOffFloorUpdate.Geometry.NativeSpecialLayer, retainedOffFloor.Geometry.NativeSpecialLayer);

		world.UpdateNativeCatacombsDoorOccupancy([clearPlayer]);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, ReadDoor(world, entity).Door.Status);
	}

	[Fact(Explicit = true)]
	public void GeneratedOwnerRejectsDuplicateDoorConfigurationAndTamperedProjection()
	{
		using var fixture = CreateFixture(0);
		var build = fixture.CreateBuild(5, 700);
		var owner = build.Policy.Bindings;
		var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
		var beforeDuplicate = ReadDoorFloor(world);
		Assert.Throws<InvalidOperationException>(() =>
			world.ConfigureNativeCatacombsDoors(owner, build.DoorCatalog));
		Assert.Equal(beforeDuplicate.Doors, ReadDoorFloor(world).Doors);
		Assert.Equal(beforeDuplicate.EffectiveGeometry.NativePieceLayer,
			ReadDoorFloor(world).EffectiveGeometry.NativePieceLayer);

		var door = beforeDuplicate.Doors[0];
		var value = world.Objects[door.EntityId];
		value.AnimationFrame++;
		Assert.Throws<InvalidDataException>(() => world.TryGetNativeCatacombsDoorState(door.EntityId, out _));
		value.AnimationFrame = door.AnimationFrame;

		var geometryWorld = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
		var backing = geometryWorld.World!.Levels.Single(level => level.LevelId == 700);
		var cell = door.PositionY * backing.Width + door.PositionX;
		var pieces = backing.NativePieceLayer.ToArray();
		pieces[cell] = checked((ushort)((pieces[cell] + 1) % AuthoritativeDunTilePropertyCatalog.NativeTileCount));
		geometryWorld.World.ReplaceLevel(CopyLevel(backing, pieces));
		Assert.Throws<InvalidDataException>(() => geometryWorld.ValidateNativeCatacombsDoors(owner));
	}

	[Fact(Explicit = true)]
	public void GeneratedOwnerExecutesTypedDoorCommandsAndRefusesArmedCatacombsChests()
	{
		using var fixture = CreateFixture(0);
		var build = fixture.CreateBuild(5, 700, dungeonSeed: 2588);
		var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, fixture.Activation.MonsterMissiles!);
		var floor = Assert.Single(world.CaptureNativeCatacombsDoorStates());
		var door = floor.Doors.First(candidate =>
			!world.CombatTargets.Values.Any(actor => actor.LevelId == candidate.LevelId
				&& (actor.PositionX == candidate.PositionX && actor.PositionY == candidate.PositionY
					|| actor.IsMoving && actor.FuturePositionX == candidate.PositionX
						&& actor.FuturePositionY == candidate.PositionY))
			&& !world.Corpses.Values.Any(corpse => corpse.LevelId == candidate.LevelId
				&& corpse.PositionX == candidate.PositionX && corpse.PositionY == candidate.PositionY)
			&& !world.WorldItems.Values.Any(item => item.LevelId == candidate.LevelId
				&& item.PositionX == candidate.PositionX && item.PositionY == candidate.PositionY));
		var value = world.Objects[door.EntityId];
		Assert.Contains(value.NativeObjectId, new uint[] { 42, 43 });
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Closed, door.Status);
		Assert.False(value.HasAuthoritativeInteraction);
		Assert.False(value.Activated);

		var playerX = door.PositionX > 0 ? door.PositionX - 1 : door.PositionX + 1;
		var player = CreatePlayer(0xF5000001, 700, playerX, door.PositionY, 0);
		var processorRandom = new DeterministicLegacyRandom(0x1357_9BDF);
		var processor = new DiabloWorldCommandProcessor(world, random: processorRandom,
			items: fixture.Items, spells: fixture.Spells);
		var initialFrame = value.AnimationFrame;
		var cell = door.PositionY * floor.EffectiveGeometry.Width + door.PositionX;
		var openPiece = door.Kind == AuthoritativeCatacombsDoorKind.Left
			? AuthoritativeCatacombsDoors.OpenLeftPiece : AuthoritativeCatacombsDoors.OpenRightPiece;
		var closedPiece = door.Kind == AuthoritativeCatacombsDoorKind.Left
			? AuthoritativeCatacombsDoors.ClosedLeftPiece : AuthoritativeCatacombsDoors.ClosedRightPiece;
		var openSpecial = door.Kind == AuthoritativeCatacombsDoorKind.Left
			? AuthoritativeCatacombsDoors.OpenLeftSpecial : AuthoritativeCatacombsDoors.OpenRightSpecial;
		var result = processor.Execute(player, new DiabloOperateObjectIntent(door.EntityId), 1, [player]);
		Assert.True(result.Succeeded, result.Failure?.ToString());
		Assert.True(world.TryGetNativeCatacombsDoorState(door.EntityId, out var opened));
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, opened.Status);
		Assert.Equal(initialFrame + 2, opened.AnimationFrame);
		var openedFloor = Assert.Single(world.CaptureNativeCatacombsDoorStates());
		Assert.Equal(openPiece, openedFloor.EffectiveGeometry.NativePieceLayer[cell]);
		Assert.Equal(openSpecial, openedFloor.EffectiveGeometry.NativeSpecialLayer[cell]);
		Assert.False(value.Activated);
		Assert.False(value.HasAuthoritativeInteraction);
		Assert.Equal(opened.AnimationFrame, value.AnimationFrame);
		Assert.True(value.MissilePassable);

		result = processor.Execute(player, new DiabloOperateObjectIntent(door.EntityId), 2, [player]);
		Assert.True(result.Succeeded, result.Failure?.ToString());
		Assert.True(world.TryGetNativeCatacombsDoorState(door.EntityId, out var closed));
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Closed, closed.Status);
		Assert.Equal(initialFrame, closed.AnimationFrame);
		var closedFloor = Assert.Single(world.CaptureNativeCatacombsDoorStates());
		Assert.Equal(closedPiece, closedFloor.EffectiveGeometry.NativePieceLayer[cell]);
		Assert.Equal((ushort)0, closedFloor.EffectiveGeometry.NativeSpecialLayer[cell]);
		Assert.False(value.Activated);
		Assert.False(value.HasAuthoritativeInteraction);
		Assert.Equal(closed.AnimationFrame, value.AnimationFrame);
		Assert.False(value.MissilePassable);

		bool IsUnopenedCatacombsChest(AuthoritativeWorldObject candidate)
			=> candidate.LevelId == 700
				&& candidate.NativeObjectId is (5 or 6 or 7 or 68 or 69 or 70)
				&& !candidate.Activated;
		var chest = world.Objects.Values.FirstOrDefault(candidate =>
			IsUnopenedCatacombsChest(candidate) && candidate.TrapArmed)
			?? world.Objects.Values.FirstOrDefault(IsUnopenedCatacombsChest);
		if (chest is not null) {
			var chestPlayerX = chest.PositionX > 0 ? chest.PositionX - 1 : chest.PositionX + 1;
			var chestPlayer = CreatePlayer(0xF5000002, 700, chestPlayerX, chest.PositionY, 0);
			var chestFrame = chest.AnimationFrame;
			var chestTrapArmed = chest.TrapArmed;
			var randomBeforeChest = processorRandom.State;
			result = processor.Execute(chestPlayer, new DiabloOperateObjectIntent(chest.EntityId), 3, [chestPlayer]);
			Assert.False(result.Succeeded);
			Assert.Equal(chestTrapArmed ? DiabloRuleFailure.NotAllowed : DiabloRuleFailure.InvalidTarget,
				result.Failure);
			Assert.Equal(chestFrame, chest.AnimationFrame);
			Assert.False(chest.Activated);
			Assert.Equal(chestTrapArmed, chest.TrapArmed);
			Assert.Equal(randomBeforeChest, processorRandom.State);
		}
	}

	private static void AssertBlockedByOccupant(
		DiabloWorldState world,
		uint entityId,
		AuthoritativePlayerState activeFloorParticipant)
	{
		var before = ReadDoor(world, entityId);
		if (before.Door.Status == AuthoritativeCatacombsDoorStatus.Closed)
			Assert.True(world.TryOperateNativeCatacombsDoor(entityId, []));
		var open = ReadDoor(world, entityId);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Open, open.Door.Status);
		world.UpdateNativeCatacombsDoorOccupancy([activeFloorParticipant]);
		var blocked = ReadDoor(world, entityId);
		Assert.Equal(AuthoritativeCatacombsDoorStatus.Blocked, blocked.Door.Status);
		Assert.Equal(open.Door.AnimationFrame, blocked.Door.AnimationFrame);
		Assert.Equal(open.Geometry.NativePieceLayer, blocked.Geometry.NativePieceLayer);
		Assert.Equal(open.Geometry.NativeSpecialLayer, blocked.Geometry.NativeSpecialLayer);
	}

	private static AuthoritativeNativeCatacombsDoorFloorState ReadDoorFloor(DiabloWorldState world)
		=> Assert.Single(world.CaptureNativeCatacombsDoorStates());

	private static (AuthoritativeCatacombsDoorState Door, AuthoritativeLevel Geometry) ReadDoor(
		DiabloWorldState world, uint entityId)
	{
		var floor = ReadDoorFloor(world);
		var door = floor.Doors.Single(value => value.EntityId == entityId);
		return (door, floor.EffectiveGeometry);
	}

	private static AuthoritativePlayerState CreatePlayer(
		uint entityId,
		uint levelId,
		int x,
		int y,
		uint classId,
		AuthoritativePlayerActionState? action = null)
		=> new(0, 0, 100, 0, 0, PlayerAttributesState.Zero, [],
			Enumerable.Repeat(-1, 40).ToArray(), [],
			x, y, 100, 1, levelId, 0, characterClassId: classId) {
			EntityId = entityId,
			CurrentAction = action,
		};

	private static AuthoritativeLevel CopyLevel(AuthoritativeLevel source, IReadOnlyList<ushort> pieces)
		=> new(source.LevelId, source.Width, source.Height,
			blockedCells: source.BlockedCells,
			missileBlockedCells: source.MissileBlockedCells,
			isDefaultStart: source.IsDefaultStart,
			suppressMultiplayerDeathDrops: source.SuppressMultiplayerDeathDrops,
			nativeTransparency: source.NativeTransparency,
			nativeObjectLayer: source.NativeObjectLayer,
			nativeMegaTileLayer: source.NativeMegaTileLayer,
			nativePieceLayer: pieces,
			nativePieceProperties: source.NativePieceProperties,
			nativePieceCollisionCells: source.NativePieceCollisionCells,
			nativePieceMissileBlockedCells: source.NativePieceMissileBlockedCells,
			nativeMonsterLayer: source.NativeMonsterLayer,
			startPositionX: source.StartPositionX,
			startPositionY: source.StartPositionY,
			nativeVisionBlockedCells: source.HasExplicitNativeVisionBlockedCells
				? source.NativeVisionBlockedCells : null,
			nativeSpecialLayer: source.NativeSpecialLayer,
			staticObjectBlockedCells: source.StaticObjectBlockedCells) {
			StableSymbol = source.StableSymbol,
		};
}
