using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeWorldDestinationTests
{
    [Fact]
    public void ListsDeclaredLocalTargetsBeyondVisionWithoutMutatingSnapshot()
    {
        var snapshot = State();
        snapshot.TownerGreetings.Add(new TownerGreetingSnapshot {
            TownerId = 7, LevelId = 0, PositionX = 43, PositionY = 66,
        });
        snapshot.TownerGreetings.Add(new TownerGreetingSnapshot {
            TownerId = 0, LevelId = 0, PositionX = 62, PositionY = 63,
        });
        snapshot.TownerGreetings.Add(new TownerGreetingSnapshot { TownerId = 1, LevelId = 2 });
        snapshot.LevelGeometry.Add(new LevelGeometrySnapshot {
            LevelId = 0, Width = 112, Height = 112, NativeVisionBlockedCells = { 112 * 111 },
        });
        var before = snapshot.Clone();
        var targets = AuthoritativeWorldDestinationSelector.GetDestinations(snapshot);
        Assert.Equal(new[] { "towner:0", "towner:7", "portal:42" }, targets.Select(target => target.Key));
        Assert.Equal(62, targets[0].X);
        Assert.Equal(63, targets[0].Y);
        Assert.Null(AuthoritativeTownerGreetingSelector.SelectAt(snapshot, 62, 63));
        Assert.True(AuthoritativeWorldDestinationSelector.IsPresent(snapshot, 62, 63, targets[0].CreateInteraction()));
        Assert.Equal(before, snapshot);
        snapshot.TownerGreetings[1].PositionX++;
        Assert.Equal(62, targets[0].X);
        Assert.False(AuthoritativeWorldDestinationSelector.IsPresent(snapshot, 62, 63, targets[0].CreateInteraction()));
    }

    [Fact]
    public void ResolvesBidirectionalPortalDestinationAndReturnsFreshIntents()
    {
        var snapshot = State();
        var target = Assert.Single(AuthoritativeWorldDestinationSelector.GetDestinations(snapshot));
        Assert.Equal((57, 40), (target.X, target.Y));
        Assert.Equal(42U, target.CreateInteraction().UsePortalRequested.PortalId);
        target.CreateInteraction().UsePortalRequested.PortalId = 5;
        Assert.Equal(42U, target.CreateInteraction().UsePortalRequested.PortalId);
        Assert.True(AuthoritativeWorldDestinationSelector.IsPresent(snapshot, 57, 40, target.CreateInteraction()));
        snapshot.Portals.Clear();
        Assert.False(AuthoritativeWorldDestinationSelector.IsPresent(snapshot, 57, 40, target.CreateInteraction()));
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 1)]
    [InlineData(false, 1, 1)]
    [InlineData(true, 1, 1)]
    [InlineData(true, 2, 0)]
    public void DoesNotInventOneWayOrOtherLevelEndpoints(bool bidirectional, uint level, int count)
    {
        var snapshot = State();
        snapshot.Players[0].LevelId = level;
        snapshot.Portals[0].Bidirectional = bidirectional;
        var targets = AuthoritativeWorldDestinationSelector.GetDestinations(snapshot);
        Assert.Equal(count, targets.Count);
        if (level == 1 && count == 1)
            Assert.Equal((20, 21), (targets[0].X, targets[0].Y));
    }

    [Fact]
    public void SameLevelPortalKeepsNativeSourcePrecedence()
    {
        var snapshot = State();
        snapshot.Portals[0].SourceLevelId = 0;
        var target = Assert.Single(AuthoritativeWorldDestinationSelector.GetDestinations(snapshot));
        Assert.Equal((20, 21), (target.X, target.Y));
        Assert.False(AuthoritativeWorldDestinationSelector.IsPresent(snapshot, 57, 40, target.CreateInteraction()));
    }

    [Fact]
    public void HeldCursorHidesGreetingButDoesNotInventAPortalRestriction()
    {
        var snapshot = State();
        snapshot.TownerGreetings.Add(new TownerGreetingSnapshot { TownerId = 7 });
        snapshot.Players[0].HeldItem = new ItemSnapshot();
        Assert.True(Assert.Single(AuthoritativeWorldDestinationSelector.GetDestinations(snapshot)).IsPortal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("no-player")]
    [InlineData("dead")]
    public void RequiresLiveLocalFirstPlayer(string cause)
    {
        var snapshot = State();
        if (cause == "no-player") snapshot.Players.Clear();
        if (cause == "dead") snapshot.Players[0].Life = 0;
        Assert.Empty(AuthoritativeWorldDestinationSelector.GetDestinations(cause == "null" ? null : snapshot));
    }

    [Fact]
    public void DistantGreetingUsesExistingAcknowledgedTravelAndCancelsWhenRemoved()
    {
        var snapshot = State();
        snapshot.TownerGreetings.Add(new TownerGreetingSnapshot { TownerId = 7, PositionX = 43, PositionY = 66 });
        var target = AuthoritativeWorldDestinationSelector.GetDestinations(snapshot)[0];
        var travel = new AuthoritativeInteractionTravel();
        travel.Start(snapshot, target.X, target.Y, target.CreateInteraction(), true);
        var move = travel.Next(snapshot, null, true, 0,
            AuthoritativeWorldDestinationSelector.IsPresent(snapshot, target.X, target.Y, target.CreateInteraction()), false);
        Assert.True(move!.MoveRequested.TargetCell);
        Assert.Equal((43, 66), (move.MoveRequested.TargetX, move.MoveRequested.TargetY));
        travel.Submitted(1, snapshot.Players[0]);
        snapshot.TownerGreetings.Clear();
        Assert.Null(travel.Next(snapshot, null, true, 0,
            AuthoritativeWorldDestinationSelector.IsPresent(snapshot, target.X, target.Y, target.CreateInteraction()), false));
        Assert.False(travel.Active);
    }

    private static Snapshot State() => new() {
        Tick = 10,
        Players = { new PlayerSnapshot { EntityId = 1, Life = 100, LevelId = 0 } },
        Portals = { new PortalSnapshot {
            PortalId = 42, SourceLevelId = 1, SourcePositionX = 20, SourcePositionY = 21,
            DestinationLevelId = 0, DestinationPositionX = 57, DestinationPositionY = 40,
            Bidirectional = true,
        } },
    };
}
