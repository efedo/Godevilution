using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Devilution.Diablo;

namespace Devilution.Server.Stores;

public sealed partial class DiabloCohortCodec
{
    private static readonly ConcurrentDictionary<(Type Type, CodecSchema Schema), GraphContractPlan> GraphContracts = new();
    private static readonly ConcurrentDictionary<Type, GraphDictionaryEntryPlan> GraphDictionaryEntries = new();

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

        var contract = GraphContracts.GetOrAdd((type, schema), static key => CreateGraphContract(key.Type, key.Schema));
        if (contract.Kind == GraphContractKind.Dictionary) {
            foreach (var entry in (IEnumerable)value) {
                var accessors = GraphDictionaryEntries.GetOrAdd(entry.GetType(), static entryType => CreateDictionaryEntryPlan(entryType));
                var key = accessors.GetKey(entry)!;
                var item = accessors.GetValue(entry);
                ValidateGraph(key, contract.KeyType!, visited, schema);
                if (item is null)
                    throw new InvalidDataException("Cohort dictionaries cannot contain null values.");
                ValidateGraph(item, contract.ValueType!, visited, schema);
            }
            return;
        }
        if (contract.Kind == GraphContractKind.Sequence) {
            var elementType = contract.ElementType!;
            // Primitive geometry layers need no per-cell walk.
            if (elementType.IsPrimitive && elementType != typeof(char))
                return;
            foreach (var element in (IEnumerable)value) {
                if (element is null)
                    throw new InvalidDataException("Cohort arrays cannot contain null elements.");
                ValidateGraph(element, elementType, visited, schema);
            }
            return;
        }

        foreach (var property in contract.Properties) {
            var item = property.GetValue(value);
            if (item is null) {
                if (property.Required)
                    throw new InvalidDataException($"Cohort field '{property.Name}' cannot be null.");
                continue;
            }
            ValidateGraph(item, property.PropertyType, visited, schema);
        }
    }

    private static GraphContractPlan CreateGraphContract(Type type, CodecSchema schema)
    {
        var dictionary = FindGenericContract(type, typeof(IReadOnlyDictionary<,>))
            ?? FindGenericContract(type, typeof(IDictionary<,>));
        if (dictionary is not null) {
            var arguments = dictionary.GetGenericArguments();
            return new GraphContractPlan(GraphContractKind.Dictionary, arguments[0], arguments[1], null, Array.Empty<GraphPropertyPlan>());
        }

        var sequence = FindGenericContract(type, typeof(IEnumerable<>));
        if (sequence is not null)
            return new GraphContractPlan(GraphContractKind.Sequence, null, null, sequence.GetGenericArguments()[0], Array.Empty<GraphPropertyPlan>());

        var properties = GetProperties(type, schema)
            .Select(property => new GraphPropertyPlan(
                property.Name,
                property.PropertyType,
                CreateGraphGetter(property),
                !AllowsNull(property)
                    || IsRequiredCathedralDoors(type, property, schema)
                    || IsRequiredNativeSarcophagi(type, property, schema)
                    || IsRequiredNativeBarrels(type, property, schema)
                    || IsRequiredNativeTraps(type, property, schema)
                    || IsRequiredNativeTown(type, property, schema)))
            .ToArray();
        return new GraphContractPlan(GraphContractKind.Object, null, null, null, properties);
    }

    private static GraphDictionaryEntryPlan CreateDictionaryEntryPlan(Type entryType)
    {
        var key = entryType.GetProperty("Key")!;
        var value = entryType.GetProperty("Value")!;
        return new GraphDictionaryEntryPlan(CreateGraphGetter(key), CreateGraphGetter(value));
    }

    private static Func<object, object?> CreateGraphGetter(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var target = Expression.Convert(instance, property.DeclaringType!);
        var access = Expression.Property(target, property);
        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
    }

    private enum GraphContractKind
    {
        Dictionary,
        Sequence,
        Object,
    }

    private sealed class GraphContractPlan
    {
        public GraphContractPlan(
            GraphContractKind kind,
            Type? keyType,
            Type? valueType,
            Type? elementType,
            GraphPropertyPlan[] properties)
        {
            Kind = kind;
            KeyType = keyType;
            ValueType = valueType;
            ElementType = elementType;
            Properties = Array.AsReadOnly(properties);
        }

        public GraphContractKind Kind { get; }
        public Type? KeyType { get; }
        public Type? ValueType { get; }
        public Type? ElementType { get; }
        public IReadOnlyList<GraphPropertyPlan> Properties { get; }
    }

    private sealed class GraphPropertyPlan
    {
        public GraphPropertyPlan(string name, Type propertyType, Func<object, object?> getValue, bool required)
        {
            Name = name;
            PropertyType = propertyType;
            GetValue = getValue;
            Required = required;
        }

        public string Name { get; }
        public Type PropertyType { get; }
        public Func<object, object?> GetValue { get; }
        public bool Required { get; }
    }

    private sealed class GraphDictionaryEntryPlan
    {
        public GraphDictionaryEntryPlan(Func<object, object?> getKey, Func<object, object?> getValue)
        {
            GetKey = getKey;
            GetValue = getValue;
        }

        public Func<object, object?> GetKey { get; }
        public Func<object, object?> GetValue { get; }
    }
}
