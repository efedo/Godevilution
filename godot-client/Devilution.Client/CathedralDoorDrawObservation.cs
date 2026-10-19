using Devilution.Client.Protocol;

namespace Devilution.Client;

/** Bounded current-frame observation of a door actually drawn by the world view. */
internal readonly record struct CathedralDoorDrawObservation(
    uint EntityId, uint NativeObjectId, int DoorState, uint AnimationFrame, ObjectPresentationFrame Frame);
