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
    private const string CathedralDoorsPropertyName = "CathedralDoors";
    private const int LegacyFormatVersion = DiabloCohortState.CurrentFormatVersion;
    private const int CathedralDoorsFormatVersion = DiabloCohortState.CathedralDoorFormatVersion;
    private const int NativeMonsterFormatVersion = DiabloCohortState.NativeMonsterFormatVersion;
    private const int CathedralItemFormatVersion = DiabloCohortState.CathedralItemFormatVersion;
    private const int NativeCombatFormatVersion = DiabloCohortState.NativeCombatFormatVersion;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly ConcurrentDictionary<(Type Type, CodecSchema Schema), PropertyInfo[]> Properties = new();
    private static readonly ConcurrentDictionary<PropertyInfo, bool> NullableProperties = new();
    private static readonly JsonSerializerOptions LegacyOptions = CreateOptions(CodecSchema.Legacy);
    private static readonly JsonSerializerOptions CathedralDoorsOptions = CreateOptions(CodecSchema.CathedralDoors);
    private static readonly JsonSerializerOptions NativeMonsterOptions = CreateOptions(CodecSchema.NativeMonsters);
    private static readonly JsonSerializerOptions CathedralItemOptions = CreateOptions(CodecSchema.CathedralItems);
    private static readonly JsonSerializerOptions NativeCombatOptions = CreateOptions(CodecSchema.NativeCombat);

    public string Serialize(DiabloCohortState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var schema = SelectSerializationSchema(state);
        try {
            ValidateGraph(state, typeof(DiabloCohortState), new HashSet<object>(ReferenceEqualityComparer.Instance), schema);
            state.ValidateShape();
            return JsonSerializer.Serialize(state, GetOptions(schema));
        } catch (Exception exception) when (IsEncodingFailure(exception)) {
            throw new InvalidDataException("The Diablo cohort cannot be encoded without losing state.", exception);
        }
    }

    /** Strict standalone codec shared by Cathedral door save metadata. */
    internal static string SerializeCathedralDoorCheckpoint(DiabloCathedralDoorCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        try {
            ValidateGraph(checkpoint, typeof(DiabloCathedralDoorCheckpoint),
                new HashSet<object>(ReferenceEqualityComparer.Instance), CodecSchema.CathedralDoors);
            checkpoint.ValidateShape();
            return JsonSerializer.Serialize(checkpoint, CathedralDoorsOptions);
        } catch (Exception exception) when (IsEncodingFailure(exception)) {
            throw new InvalidDataException(
                "The Cathedral door checkpoint cannot be encoded without losing state.", exception);
        }
    }

    /** Strict standalone reader shared by Cathedral door save metadata. */
    internal static DiabloCathedralDoorCheckpoint DeserializeCathedralDoorCheckpoint(JsonElement value)
    {
        try {
            ValidateJson(value, typeof(DiabloCathedralDoorCheckpoint), false, CodecSchema.CathedralDoors);
            var checkpoint = value.Deserialize<DiabloCathedralDoorCheckpoint>(CathedralDoorsOptions)
                ?? throw new InvalidDataException("The Cathedral door checkpoint must be an explicit object.");
            checkpoint.ValidateShape();
            return checkpoint;
        } catch (Exception exception) when (IsEncodingFailure(exception)) {
            throw new InvalidDataException(
                "The Cathedral door checkpoint is invalid or incomplete.", exception);
        }
    }

    public DiabloCohortState Deserialize(string serializedState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedState);
        try {
            StrictUtf8.GetByteCount(serializedState);
            using var document = JsonDocument.Parse(serializedState);
            var schema = SelectDeserializationSchema(document.RootElement);
            ValidateJson(document.RootElement, typeof(DiabloCohortState), false, schema);
            var state = document.RootElement.Deserialize<DiabloCohortState>(GetOptions(schema))
                ?? throw new InvalidDataException("The Diablo cohort must be an explicit object.");
            if (state.FormatVersion != GetFormatVersion(schema))
                throw new InvalidDataException("The Diablo cohort format version was normalized while decoding.");
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

    private static JsonSerializerOptions CreateOptions(CodecSchema schema)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Kind != JsonTypeInfoKind.Object)
                return;
            var fields = GetProperties(info.Type, schema).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
            for (var index = info.Properties.Count - 1; index >= 0; index--) {
                if (!fields.Contains(info.Properties[index].Name))
                    info.Properties.RemoveAt(index);
                else if (IsHorkMoveField(info.Type, info.Properties[index].Name))
                    info.Properties[index].ShouldSerialize = (value, _) => !HasDefaultHorkMoveState(value);
            }
        });
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = resolver,
        };
        // Immutable legacy projection records deliberately have convenience
        // constructors. Adapt those here, without JSON attributes in the module.
        options.Converters.Add(new CompleteRecordConverterFactory(schema));
        options.Converters.Add(new CheckedEnumConverterFactory());
        return options;
    }

    private static JsonSerializerOptions GetOptions(CodecSchema schema) => schema switch {
        CodecSchema.Legacy => LegacyOptions,
        CodecSchema.CathedralDoors => CathedralDoorsOptions,
        CodecSchema.NativeMonsters => NativeMonsterOptions,
        CodecSchema.CathedralItems => CathedralItemOptions,
        CodecSchema.NativeCombat => NativeCombatOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(schema)),
    };

    private static PropertyInfo[] GetProperties(Type type, CodecSchema schema) => Properties.GetOrAdd(
        (type, schema),
        static key => key.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod?.IsPublic == true
                && property.GetIndexParameters().Length == 0
                && (property.SetMethod is not null
                    || key.Type.GetConstructors().Any(constructor => constructor.GetParameters().Any(parameter =>
                        string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                        && property.PropertyType == parameter.ParameterType)))
                && (key.Schema is CodecSchema.NativeMonsters or CodecSchema.CathedralItems or CodecSchema.NativeCombat
                    || !IsNativeMonsterField(key.Type, property.Name))
                && (key.Schema is CodecSchema.CathedralItems or CodecSchema.NativeCombat || !IsCathedralItemField(key.Type, property.Name))
                && (key.Schema == CodecSchema.NativeCombat || !IsNativeCombatField(key.Type, property.Name))
                && (key.Schema != CodecSchema.Legacy
                    || key.Type != typeof(DiabloCohortState)
                    || property.Name != CathedralDoorsPropertyName))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray());

    private static CodecSchema SelectSerializationSchema(DiabloCohortState state)
    {
        if ((state.FormatVersion != CathedralItemFormatVersion && state.FormatVersion != NativeCombatFormatVersion)
            && (state.CathedralItems is not null || state.Configuration?.CathedralItemMetadataFingerprint is not null
                || state.Configuration?.CathedralItemGenerationFingerprint is not null))
            throw new InvalidDataException("Native item state cannot be down-converted to an older cohort.");
        if (state.FormatVersion != NativeMonsterFormatVersion && state.FormatVersion != CathedralItemFormatVersion && state.FormatVersion != NativeCombatFormatVersion
            && (state.Configuration?.NativeMonsterMetadataFingerprint is not null
                || state.CombatTargets?.Any(target => target?.NativeInitialization is not null
                    || target?.Monster?.NativeInitializationPresent == true) == true))
            throw new InvalidDataException("Native monster state cannot be down-converted to a legacy cohort.");
        if (state.FormatVersion != NativeCombatFormatVersion
            && (state.CombatTargets?.Any(target => target?.Monster?.NativeMeleeCursor is not null
                    || target?.Monster?.NativeWalkCursor is not null) == true
                || state.Projectiles?.Any(projectile => projectile?.NativeWeaponArrow is not null) == true))
            throw new InvalidDataException("Native combat state cannot be down-converted to an older cohort.");
        var hasCathedralDoors = GetCathedralDoors(state) is not null;
        return state.FormatVersion switch {
            LegacyFormatVersion when !hasCathedralDoors => CodecSchema.Legacy,
            LegacyFormatVersion => throw new InvalidDataException(
                "Legacy Diablo cohorts cannot carry Cathedral door state."),
            CathedralDoorsFormatVersion when hasCathedralDoors => CodecSchema.CathedralDoors,
            CathedralDoorsFormatVersion => throw new InvalidDataException(
                "Cathedral door cohorts require explicit Cathedral door state."),
            NativeMonsterFormatVersion => CodecSchema.NativeMonsters,
            CathedralItemFormatVersion when state.CathedralItems is not null => CodecSchema.CathedralItems,
            CathedralItemFormatVersion => throw new InvalidDataException("Native item cohorts require their complete item pools."),
            NativeCombatFormatVersion when state.CathedralItems is not null => CodecSchema.NativeCombat,
            NativeCombatFormatVersion => throw new InvalidDataException("Native combat cohorts require their complete item pools."),
            _ => throw new InvalidDataException(
                $"Unsupported Diablo cohort format version {state.FormatVersion}."),
        };
    }

    private static CodecSchema SelectDeserializationSchema(JsonElement root)
    {
        RequireKind(root, JsonValueKind.Object, typeof(DiabloCohortState));
        var versionCount = 0;
        var version = 0;
        foreach (var field in root.EnumerateObject()) {
            if (!string.Equals(field.Name, "FormatVersion", StringComparison.Ordinal))
                continue;
            if (++versionCount != 1)
                throw new InvalidDataException("A Diablo cohort must contain exactly one FormatVersion field.");
            if (field.Value.ValueKind != JsonValueKind.Number
                || !field.Value.TryGetInt32(out version)
                || !string.Equals(field.Value.GetRawText(),
                    version.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                throw new InvalidDataException("A Diablo cohort FormatVersion must be one canonical integer.");
        }
        if (versionCount != 1)
            throw new InvalidDataException("A Diablo cohort must contain exactly one FormatVersion field.");
        return version switch {
            LegacyFormatVersion => CodecSchema.Legacy,
            CathedralDoorsFormatVersion => CodecSchema.CathedralDoors,
            NativeMonsterFormatVersion => CodecSchema.NativeMonsters,
            CathedralItemFormatVersion => CodecSchema.CathedralItems,
            NativeCombatFormatVersion => CodecSchema.NativeCombat,
            _ => throw new InvalidDataException(
                $"Unsupported Diablo cohort format version {version}."),
        };
    }

    private static int GetFormatVersion(CodecSchema schema) => schema switch {
        CodecSchema.Legacy => LegacyFormatVersion,
        CodecSchema.CathedralDoors => CathedralDoorsFormatVersion,
        CodecSchema.NativeMonsters => NativeMonsterFormatVersion,
        CodecSchema.CathedralItems => CathedralItemFormatVersion,
        CodecSchema.NativeCombat => NativeCombatFormatVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(schema)),
    };

    private static DiabloCathedralDoorCheckpoint? GetCathedralDoors(DiabloCohortState state) =>
        state.CathedralDoors;

    private static bool IsNativeMonsterField(Type type, string name) =>
        (type == typeof(DiabloCohortConfigurationState)
            && name == nameof(DiabloCohortConfigurationState.NativeMonsterMetadataFingerprint))
        || (type == typeof(DiabloCohortCombatTargetState)
            && name == nameof(DiabloCohortCombatTargetState.NativeInitialization))
        || (type == typeof(DiabloMonsterState)
            && name == nameof(DiabloMonsterState.NativeInitializationPresent));

    private static bool IsCathedralItemField(Type type, string name) =>
        (type == typeof(DiabloCohortState) && name == nameof(DiabloCohortState.CathedralItems))
        || (type == typeof(DiabloCohortConfigurationState)
            && name is nameof(DiabloCohortConfigurationState.CathedralItemMetadataFingerprint)
                or nameof(DiabloCohortConfigurationState.CathedralItemGenerationFingerprint));

    private static bool IsNativeCombatField(Type type, string name) =>
        (type == typeof(DiabloProjectileState) && name == nameof(DiabloProjectileState.NativeWeaponArrow))
        || (type == typeof(DiabloMonsterState)
            && name is nameof(DiabloMonsterState.NativeMeleeCursor) or nameof(DiabloMonsterState.NativeWalkCursor))
        || (type == typeof(AuthoritativeNativeMonsterInitialization)
            && name is nameof(AuthoritativeNativeMonsterInitialization.NativeAttackFrameCount)
                or nameof(AuthoritativeNativeMonsterInitialization.NativeAttackTicksPerFrame)
                or nameof(AuthoritativeNativeMonsterInitialization.NativeHitRecoveryFrameCount)
                or nameof(AuthoritativeNativeMonsterInitialization.NativeHitRecoveryTicksPerFrame)
                or nameof(AuthoritativeNativeMonsterInitialization.NativeWalkFrameCount)
                or nameof(AuthoritativeNativeMonsterInitialization.NativeWalkTicksPerFrame));

    private static bool IsRequiredCathedralDoors(Type type, PropertyInfo property, CodecSchema schema) =>
        type == typeof(DiabloCohortState)
        && ((schema == CodecSchema.CathedralDoors
                && string.Equals(property.Name, CathedralDoorsPropertyName, StringComparison.Ordinal))
            || (schema is CodecSchema.CathedralItems or CodecSchema.NativeCombat
                && property.Name == nameof(DiabloCohortState.CathedralItems)));

    private static bool AllowsNull(PropertyInfo property) => NullableProperties.GetOrAdd(
        property,
        static field => Nullable.GetUnderlyingType(field.PropertyType) is not null
            || (!field.PropertyType.IsValueType
                && new NullabilityInfoContext().Create(field).ReadState == NullabilityState.Nullable));

    // This additive pair is absent from legacy cohorts. Other fields remain
    // required, and partial Move state must not silently acquire defaults.
    private static bool IsHorkMoveField(Type type, string name) => type == typeof(DiabloMonsterState)
        && name is nameof(DiabloMonsterState.HorkDemonMoveTicks) or nameof(DiabloMonsterState.HorkDemonMoveTurn);

    private static bool HasDefaultHorkMoveState(object value) => value is DiabloMonsterState monster
        && monster.HorkDemonMoveTicks == -1 && monster.HorkDemonMoveTurn == 0;

    private static Type? FindGenericContract(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == definition);

    private static void ValidateJson(JsonElement value, Type declaredType, bool allowsNull, CodecSchema schema)
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
                ValidateJson(entry.Value, arguments[1], false, schema);
            }
            return;
        }
        var sequence = FindGenericContract(type, typeof(IEnumerable<>));
        if (sequence is not null) {
            RequireKind(value, JsonValueKind.Array, type);
            var elementType = sequence.GetGenericArguments()[0];
            foreach (var element in value.EnumerateArray())
                ValidateJson(element, elementType, false, schema);
            return;
        }
        RequireKind(value, JsonValueKind.Object, type);
        var properties = GetProperties(type, schema).ToDictionary(property => property.Name, StringComparer.Ordinal);
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject()) {
            if (!present.Add(field.Name) || !properties.TryGetValue(field.Name, out var property))
                throw new InvalidDataException($"Unknown or duplicate cohort field '{field.Name}'.");
            ValidateJson(field.Value, property.PropertyType,
                AllowsNull(property) && !IsRequiredCathedralDoors(type, property, schema), schema);
        }
        var missing = properties.Keys.Where(name => !present.Contains(name)).ToArray();
        if (missing.Length != 0 && !(missing.Length == 2 && missing.All(name => IsHorkMoveField(type, name))))
            throw new InvalidDataException($"The {type.Name} cohort object is missing required fields.");
    }

    private static void RequireKind(JsonElement value, JsonValueKind kind, Type type)
    {
        if (value.ValueKind != kind)
            throw new InvalidDataException($"A cohort {type.Name} must be a JSON {kind}.");
    }

    private static void ValidateGraph(object value, Type declaredType, HashSet<object> visited, CodecSchema schema)
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
                ValidateGraph(key, arguments[0], visited, schema);
                if (item is null)
                    throw new InvalidDataException("Cohort dictionaries cannot contain null values.");
                ValidateGraph(item, arguments[1], visited, schema);
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
                ValidateGraph(element, elementType, visited, schema);
            }
            return;
        }
        foreach (var property in GetProperties(type, schema)) {
            var item = property.GetValue(value);
            if (item is null) {
                if (!AllowsNull(property) || IsRequiredCathedralDoors(type, property, schema))
                    throw new InvalidDataException($"Cohort field '{property.Name}' cannot be null.");
                continue;
            }
            ValidateGraph(item, property.PropertyType, visited, schema);
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

    private enum CodecSchema
    {
        Legacy,
        CathedralDoors,
        NativeMonsters,
        CathedralItems,
        NativeCombat,
    }

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
        private readonly CodecSchema schema;

        public CompleteRecordConverterFactory(CodecSchema schema)
        {
            this.schema = schema;
        }

        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.Assembly == typeof(DiabloCohortState).Assembly
            && typeToConvert.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null
            && typeToConvert.GetConstructors().Length > 1;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(CompleteRecordConverter<>).MakeGenericType(typeToConvert),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [schema],
                culture: null)!;
    }

    private sealed class CompleteRecordConverter<T> : JsonConverter<T> where T : class
    {
        private readonly PropertyInfo[] fields;
        private readonly ConstructorInfo constructor;

        public CompleteRecordConverter(CodecSchema schema)
        {
            fields = GetProperties(typeof(T), schema);
            constructor = typeof(T).GetConstructors()
                .Where(candidate => candidate.GetParameters().All(parameter => fields.Any(field =>
                string.Equals(field.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                && field.PropertyType == parameter.ParameterType)))
                .OrderByDescending(candidate => candidate.GetParameters().Length)
                .FirstOrDefault()
                ?? throw new NotSupportedException($"No complete constructor is available for {typeof(T).Name}.");
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var values = fields.ToDictionary(
                field => field.Name,
                field => root.TryGetProperty(field.Name, out var stored)
                    ? stored.Deserialize(field.PropertyType, options)
                    : IsHorkMoveField(typeof(T), field.Name)
                        ? field.Name == nameof(DiabloMonsterState.HorkDemonMoveTicks) ? -1 : 0
                        : throw new InvalidDataException($"Missing cohort field '{field.Name}'."),
                StringComparer.Ordinal);
            var parameters = constructor.GetParameters();
            var bound = parameters.Select(parameter => fields.Single(field =>
                string.Equals(field.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
            var result = (T)constructor.Invoke(bound.Select(field => values[field.Name]).ToArray());
            foreach (var field in fields) {
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
            foreach (var field in fields) {
                if (IsHorkMoveField(typeof(T), field.Name) && HasDefaultHorkMoveState(value))
                    continue;
                writer.WritePropertyName(field.Name);
                JsonSerializer.Serialize(writer, field.GetValue(value), field.PropertyType, options);
            }
            writer.WriteEndObject();
        }
    }
}
