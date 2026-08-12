using System.Text.Json;
using Devilution.Protocol.V1;

namespace Devilution.Server.Protocol;

public static class CommandDeliveryVectorLoader
{
    public static CommandDeliveryVector Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var vector = new CommandDeliveryVector(
            RequiredInt(root, "format_version"),
            RequiredString(root, "vector_id"),
            RequiredString(root, "protocol_schema_version"),
            RequiredUInt(root, "initial_rtt_ms"),
            ParseSteps(root.GetProperty("steps")),
            root.GetProperty("assertions").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());

        if (vector.FormatVersion != 1 || vector.Steps.Count == 0)
            throw new InvalidDataException("Unsupported or empty command-delivery vector.");
        return vector;
    }

    private static IReadOnlyList<CommandDeliveryVectorStep> ParseSteps(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Command-delivery steps must be an array.");

        return value.EnumerateArray().Select(step => new CommandDeliveryVectorStep(
            RequiredString(step, "operation"),
            RequiredULong(step, "client_sequence"),
            OptionalULong(step, "requested_tick"),
            OptionalULong(step, "server_receipt_sequence"),
            OptionalULong(step, "applied_tick"),
            OptionalStatus(step),
            OptionalRejectReason(step),
            OptionalBool(step, "acknowledgement_delivered"),
            OptionalULong(step, "at_ms"))).ToArray();
    }

    private static CommandStatus? OptionalStatus(JsonElement value)
    {
        var status = OptionalString(value, "status");
        return status is null ? null : status switch {
            "ACCEPTED" => CommandStatus.Accepted,
            "REJECTED" => CommandStatus.Rejected,
            "RESCHEDULED" => CommandStatus.Rescheduled,
            "DUPLICATE" => CommandStatus.Duplicate,
            _ => throw new InvalidDataException($"Unknown command status '{status}'."),
        };
    }

    private static CommandRejectReason? OptionalRejectReason(JsonElement value)
    {
        var reason = OptionalString(value, "reject_reason");
        return reason is null ? null : reason switch {
            "TOO_LATE" => CommandRejectReason.TooLate,
            "MALFORMED" => CommandRejectReason.Malformed,
            "NOT_ALLOWED" => CommandRejectReason.NotAllowed,
            "INVALID_TARGET" => CommandRejectReason.InvalidTarget,
            "INSUFFICIENT_RESOURCES" => CommandRejectReason.InsufficientResources,
            _ => throw new InvalidDataException($"Unknown command rejection reason '{reason}'."),
        };
    }

    private static string RequiredString(JsonElement value, string property)
    {
        var result = OptionalString(value, property);
        return result ?? throw new InvalidDataException($"Vector property '{property}' is required.");
    }

    private static string? OptionalString(JsonElement value, string property)
    {
        return value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : null;
    }

    private static int RequiredInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || !result.TryGetInt32(out var number))
            throw new InvalidDataException($"Vector property '{property}' is required.");
        return number;
    }

    private static uint RequiredUInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || !result.TryGetUInt32(out var number))
            throw new InvalidDataException($"Vector property '{property}' is required.");
        return number;
    }

    private static ulong RequiredULong(JsonElement value, string property)
    {
        return OptionalULong(value, property)
            ?? throw new InvalidDataException($"Vector property '{property}' is required.");
    }

    private static ulong? OptionalULong(JsonElement value, string property)
    {
        return value.TryGetProperty(property, out var result) && result.TryGetUInt64(out var number) ? number : null;
    }

    private static bool? OptionalBool(JsonElement value, string property)
    {
        return value.TryGetProperty(property, out var result) && result.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? result.GetBoolean()
            : null;
    }
}
