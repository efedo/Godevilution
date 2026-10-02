using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Devilution.Diablo;

namespace Devilution.Server.Stores;

/**
 * Strict host-owned codec for the complete, transport-neutral Diablo cohort.
 * Legacy per-session save envelopes and module gameplay remain unchanged.
 * Configuration/ownership validation belongs to fresh module candidate creation;
 * generation, integrity and durable publication belong to the neutral host store.
 */
public sealed class DiabloCohortCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();
    private static readonly ConcurrentDictionary<PropertyInfo, bool> NullableProperties = new();
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize(DiabloCohortState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try {
            ValidateGraph(state, typeof(DiabloCohortState), new HashSet<object>(ReferenceEqualityComparer.Instance));
            state.ValidateShape();
            return JsonSerializer.Serialize(state, Options);
        } catch (Exception exception) when (IsEncodingFailure(exception)) {
            throw new InvalidDataException("The Diablo cohort cannot be encoded without losing state.", exception);
        }
    }

    public DiabloCohortState Deserialize(string serializedState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedState);
        try {
            StrictUtf8.GetByteCount(serializedState);
            using var document = JsonDocument.Parse(serializedState);
            ValidateJson(document.RootElement, typeof(DiabloCohortState), false);
            var state = document.RootElement.Deserialize<DiabloCohortState>(Options)
                ?? throw new InvalidDataException("The Diablo cohort must be an explicit object.");
            state.ValidateShape();
            return state;
        } catch (Exception exception) when (IsEncodingFailure(exception)) {
            throw new InvalidDataException("The Diablo cohort is invalid or incomplete.", exception);
        }
    }

    private static bool IsEncodingFailure(Exception exception) => exception is
        JsonException or EncoderFallbackException or ArgumentException
        or InvalidOperationException or NotSupportedException or TargetInvocationException
        or FormatException or OverflowException;

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Kind != JsonTypeInfoKind.Object)
                return;
            var fields = GetProperties(info.Type).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
            for (var index = info.Properties.Count - 1; index >= 0; index--) {
                if (!fields.Contains(info.Properties[index].Name))
                    info.Properties.RemoveAt(index);
            }
        });
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = resolver,
        };
        // Immutable legacy projection records deliberately have convenience
        // constructors. Adapt those here, without JSON attributes in the module.
        options.Converters.Add(new CompleteRecordConverterFactory());
        options.Converters.Add(new CheckedEnumConverterFactory());
        return options;
    }

    private static PropertyInfo[] GetProperties(Type type) => Properties.GetOrAdd(
        type,
        static candidate => candidate.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true
                && property.GetIndexParameters().Length == 0
                && (property.SetMethod is not null
                    || candidate.GetConstructors().Any(constructor => constructor.GetParameters().Any(parameter =>
                        string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                        && property.PropertyType == parameter.ParameterType))))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray());

    private static bool AllowsNull(PropertyInfo property) => NullableProperties.GetOrAdd(
        property,
        static field => Nullable.GetUnderlyingType(field.PropertyType) is not null
            || (!field.PropertyType.IsValueType
                && new NullabilityInfoContext().Create(field).ReadState == NullabilityState.Nullable));

    private static Type? FindGenericContract(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == definition);

    private static void ValidateJson(JsonElement value, Type declaredType, bool allowsNull)
    {
        var nullable = Nullable.GetUnderlyingType(declaredType);
        if (value.ValueKind == JsonValueKind.Null) {
            if (!allowsNull && nullable is null)
                throw new InvalidDataException($"A required {declaredType.Name} value is null.");
            return;
        }
        var type = nullable ?? declaredType;
        if (type == typeof(string)) {
            if (value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Cohort text must be an explicit JSON string.");
            ValidateEscapedText(value.GetRawText());
            StrictUtf8.GetByteCount(value.GetString()!);
            return;
        }
        if (type.IsPrimitive || type.IsEnum || type == typeof(decimal))
            return; // The checked serializer enforces scalar types and numeric ranges.
        var dictionary = FindGenericContract(type, typeof(IReadOnlyDictionary<,>))
            ?? FindGenericContract(type, typeof(IDictionary<,>));
        if (dictionary is not null) {
            RequireKind(value, JsonValueKind.Object, type);
            var arguments = dictionary.GetGenericArguments();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in value.EnumerateObject()) {
                if (!names.Add(entry.Name))
                    throw new InvalidDataException("A cohort dictionary contains a duplicate key.");
                if (arguments[0] == typeof(string)) {
                    StrictUtf8.GetByteCount(entry.Name);
                } else {
                    var key = Convert.ChangeType(entry.Name, arguments[0], CultureInfo.InvariantCulture);
                    if (Convert.ToString(key, CultureInfo.InvariantCulture) != entry.Name)
                        throw new InvalidDataException("Cohort numeric dictionary keys must be canonical.");
                }
                ValidateJson(entry.Value, arguments[1], false);
            }
            return;
        }
        var sequence = FindGenericContract(type, typeof(IEnumerable<>));
        if (sequence is not null) {
            RequireKind(value, JsonValueKind.Array, type);
            var elementType = sequence.GetGenericArguments()[0];
            foreach (var element in value.EnumerateArray())
                ValidateJson(element, elementType, false);
            return;
        }
        RequireKind(value, JsonValueKind.Object, type);
        var properties = GetProperties(type).ToDictionary(property => property.Name, StringComparer.Ordinal);
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject()) {
            if (!present.Add(field.Name) || !properties.TryGetValue(field.Name, out var property))
                throw new InvalidDataException($"Unknown or duplicate cohort field '{field.Name}'.");
            ValidateJson(field.Value, property.PropertyType, AllowsNull(property));
        }
        if (present.Count != properties.Count)
            throw new InvalidDataException($"The {type.Name} cohort object is missing required fields.");
    }

    private static void RequireKind(JsonElement value, JsonValueKind kind, Type type)
    {
        if (value.ValueKind != kind)
            throw new InvalidDataException($"A cohort {type.Name} must be a JSON {kind}.");
    }

    private static void ValidateGraph(object value, Type declaredType, HashSet<object> visited)
    {
        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (type == typeof(string)) {
            StrictUtf8.GetByteCount((string)value);
            return;
        }
        if (type.IsEnum) {
            CheckEnum(type, value);
            return;
        }
        if (type.IsPrimitive || type == typeof(decimal))
            return;
        if (!type.IsValueType && !visited.Add(value))
            return;
        var dictionary = FindGenericContract(type, typeof(IReadOnlyDictionary<,>))
            ?? FindGenericContract(type, typeof(IDictionary<,>));
        if (dictionary is not null) {
            var arguments = dictionary.GetGenericArguments();
            foreach (var entry in (IEnumerable)value) {
                var entryType = entry.GetType();
                var key = entryType.GetProperty("Key")!.GetValue(entry)!;
                var item = entryType.GetProperty("Value")!.GetValue(entry);
                ValidateGraph(key, arguments[0], visited);
                if (item is null)
                    throw new InvalidDataException("Cohort dictionaries cannot contain null values.");
                ValidateGraph(item, arguments[1], visited);
            }
            return;
        }
        var sequence = FindGenericContract(type, typeof(IEnumerable<>));
        if (sequence is not null) {
            var elementType = sequence.GetGenericArguments()[0];
            // Primitive geometry layers need no reflection or per-cell walk.
            if (elementType.IsPrimitive && elementType != typeof(char))
                return;
            foreach (var element in (IEnumerable)value) {
                if (element is null)
                    throw new InvalidDataException("Cohort arrays cannot contain null elements.");
                ValidateGraph(element, elementType, visited);
            }
            return;
        }
        foreach (var property in GetProperties(type)) {
            var item = property.GetValue(value);
            if (item is null) {
                if (!AllowsNull(property))
                    throw new InvalidDataException($"Cohort field '{property.Name}' cannot be null.");
                continue;
            }
            ValidateGraph(item, property.PropertyType, visited);
        }
    }

    private static void ValidateEscapedText(string token)
    {
        for (var index = 1; index < token.Length - 1; index++) {
            if (token[index] != '\\')
                continue;
            if (token[++index] != 'u')
                continue;
            var code = ushort.Parse(token.AsSpan(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            index += 4;
            if (char.IsLowSurrogate((char)code))
                throw new InvalidDataException("Cohort text contains an unpaired escaped surrogate.");
            if (!char.IsHighSurrogate((char)code))
                continue;
            if (index + 6 >= token.Length || token[index + 1] != '\\' || token[index + 2] != 'u')
                throw new InvalidDataException("Cohort text contains an unpaired escaped surrogate.");
            var low = ushort.Parse(token.AsSpan(index + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (!char.IsLowSurrogate((char)low))
                throw new InvalidDataException("Cohort text contains an unpaired escaped surrogate.");
            index += 6;
        }
    }

    private static void CheckEnum(Type type, object value)
    {
        if (Enum.IsDefined(type, value))
            return;
        if (type.IsDefined(typeof(FlagsAttribute), false)) {
            ulong mask = 0;
            foreach (var defined in Enum.GetValues(type))
                mask |= EnumBits(type, defined);
            if ((EnumBits(type, value) & ~mask) == 0)
                return;
        }
        throw new InvalidDataException($"Undefined cohort {type.Name} value.");
    }

    private static ulong EnumBits(Type type, object value) => Type.GetTypeCode(Enum.GetUnderlyingType(type)) switch {
        TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 =>
            unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        _ => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
    };

    private sealed class CheckedEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(CheckedEnumConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class CheckedEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var raw = JsonSerializer.Deserialize(ref reader, Enum.GetUnderlyingType(typeof(T)), options)!;
            var value = (T)Enum.ToObject(typeof(T), raw);
            CheckEnum(typeof(T), value);
            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            CheckEnum(typeof(T), value);
            var underlying = Enum.GetUnderlyingType(typeof(T));
            JsonSerializer.Serialize(writer, Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture), underlying, options);
        }
    }

    private sealed class CompleteRecordConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.Assembly == typeof(DiabloCohortState).Assembly
            && typeToConvert.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
            && typeToConvert.GetConstructors().Length > 1;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(CompleteRecordConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class CompleteRecordConverter<T> : JsonConverter<T> where T : class
    {
        private static readonly PropertyInfo[] Fields = GetProperties(typeof(T));
        private static readonly ConstructorInfo Constructor = typeof(T).GetConstructors()
            .Where(constructor => constructor.GetParameters().All(parameter => Fields.Any(field =>
                string.Equals(field.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                && field.PropertyType == parameter.ParameterType)))
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new NotSupportedException($"No complete constructor is available for {typeof(T).Name}.");

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var values = Fields.ToDictionary(
                field => field.Name,
                field => root.GetProperty(field.Name).Deserialize(field.PropertyType, options),
                StringComparer.Ordinal);
            var parameters = Constructor.GetParameters();
            var bound = parameters.Select(parameter => Fields.Single(field =>
                string.Equals(field.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
            var result = (T)Constructor.Invoke(bound.Select(field => values[field.Name]).ToArray());
            foreach (var field in Fields) {
                if (bound.Contains(field)) {
                    if (!Equals(field.GetValue(result), values[field.Name]))
                        throw new InvalidDataException($"Constructor normalized cohort field '{field.Name}'.");
                } else if (field.SetMethod is not null) {
                    field.SetValue(result, values[field.Name]);
                } else {
                    throw new NotSupportedException($"Cohort field '{field.Name}' has no restore seam.");
                }
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var field in Fields) {
                writer.WritePropertyName(field.Name);
                JsonSerializer.Serialize(writer, field.GetValue(value), field.PropertyType, options);
            }
            writer.WriteEndObject();
        }
    }
}
