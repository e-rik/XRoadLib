﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using XRoadLib.Extensions;
using XRoadLib.Protocols;
using XRoadLib.Schema;
using XRoadLib.Serialization.Mapping;

namespace XRoadLib.Serialization
{
    public sealed class SerializerCache : ISerializerCache
    {
        private readonly Assembly contractAssembly;
        private readonly SchemaDefinitionReader schemaDefinitionReader;

        private readonly ConcurrentDictionary<Type, ITypeMap> customTypeMaps = new ConcurrentDictionary<Type, ITypeMap>();
        private readonly ConcurrentDictionary<XName, IServiceMap> serviceMaps = new ConcurrentDictionary<XName, IServiceMap>();
        private readonly ConcurrentDictionary<XName, Tuple<ITypeMap, ITypeMap>> xmlTypeMaps = new ConcurrentDictionary<XName, Tuple<ITypeMap, ITypeMap>>();
        private readonly ConcurrentDictionary<Type, ITypeMap> runtimeTypeMaps = new ConcurrentDictionary<Type, ITypeMap>();

        public Protocol Protocol { get; }
        public uint? Version { get; }

        public SerializerCache(Protocol protocol, SchemaDefinitionReader schemaDefinitionReader, Assembly contractAssembly, uint? version = null)
        {
            this.schemaDefinitionReader = schemaDefinitionReader;
            this.contractAssembly = contractAssembly;

            Protocol = protocol;
            Version = version;

            AddSystemType<DateTime>("dateTime", x => new DateTimeTypeMap(x));
            AddSystemType<DateTime>("date", x => new DateTypeMap(x));

            AddSystemType<bool>("boolean", x => new BooleanTypeMap(x));

            AddSystemType<float>("float", x => new SingleTypeMap(x));
            AddSystemType<double>("double", x => new DoubleTypeMap(x));
            AddSystemType<decimal>("decimal", x => new DecimalTypeMap(x));

            AddSystemType<long>("long", x => new Int64TypeMap(x));
            AddSystemType<int>("int", x => new Int32TypeMap(x));
            AddSystemType<short>("short", x => new Int16TypeMap(x));
            AddSystemType<BigInteger>("integer", x => new IntegerTypeMap(x));

            AddSystemType<string>("string", x => new StringTypeMap(x));
            AddSystemType<string>("anyURI", x => new StringTypeMap(x));

            AddSystemType<Stream>("base64Binary", x => new ContentTypeMap(x));
            AddSystemType<Stream>("hexBinary", x => new ContentTypeMap(x));
            AddSystemType<Stream>("base64", x => new ContentTypeMap(x));

            var legacyProtocol = protocol as ILegacyProtocol;
            if (legacyProtocol == null)
                return;

            CreateTestSystem(legacyProtocol);
            CreateGetState(legacyProtocol);
            CreateListMethods(legacyProtocol);
        }

        public IServiceMap GetServiceMap(string operationName)
        {
            return GetServiceMap(XName.Get(operationName, Protocol.ProducerNamespace));
        }

        public IServiceMap GetServiceMap(XName qualifiedName)
        {
            if (qualifiedName == null)
                return null;

            IServiceMap serviceMap;
            if (!serviceMaps.TryGetValue(qualifiedName, out serviceMap))
                serviceMap = AddServiceMap(qualifiedName);

            return serviceMap;
        }

        private IServiceMap AddServiceMap(XName qualifiedName)
        {
            var methodInfo = GetServiceInterface(contractAssembly, qualifiedName);
            if (methodInfo == null)
                throw XRoadException.UnknownType(qualifiedName.ToString());

            var operationDefinition = schemaDefinitionReader.GetOperationDefinition(methodInfo, qualifiedName, Version);

            var methodParameters = operationDefinition.MethodInfo.GetParameters();
            if (methodParameters.Length > 1)
                throw new Exception($"Invalid X-Road operation contract `{operationDefinition.Name.LocalName}`: expected 0-1 input parameters, but {methodParameters.Length} was given.");

            var inputTypeMap = GetTypeMap(methodParameters.SingleOrDefault()?.ParameterType);
            var outputTypeMap = GetTypeMap(operationDefinition.MethodInfo.ReturnType);

            return serviceMaps.GetOrAdd(qualifiedName, new ServiceMap(operationDefinition, inputTypeMap, outputTypeMap));
        }

        private MethodInfo GetServiceInterface(Assembly typeAssembly, XName qualifiedName)
        {
            return typeAssembly?.GetTypes()
                                .Where(t => t.IsInterface)
                                .SelectMany(t => t.GetMethods())
                                .SingleOrDefault(x => x.GetServices()
                                                       .Any(m => m.Name == qualifiedName.LocalName
                                                                 && (!Version.HasValue || m.ExistsInVersion(Version.Value))));
        }

        public ITypeMap GetTypeMapFromXsiType(XmlReader reader)
        {
            var typeValue = reader.GetTypeAttributeValue();
            return typeValue == null ? null : GetTypeMap(typeValue.Item1, typeValue.Item2);
        }

        public ITypeMap GetTypeMap(Type runtimeType, IDictionary<Type, ITypeMap> partialTypeMaps = null)
        {
            if (runtimeType == null)
                return null;

            var normalizedType = Nullable.GetUnderlyingType(runtimeType) ?? runtimeType;

            ITypeMap typeMap;
            if (!runtimeTypeMaps.TryGetValue(normalizedType, out typeMap) && (partialTypeMaps == null || !partialTypeMaps.TryGetValue(normalizedType, out typeMap)))
                typeMap = AddTypeMap(normalizedType, partialTypeMaps);

            return typeMap;
        }

        public ITypeMap GetTypeMap(XName qualifiedName, bool isArray)
        {
            if (qualifiedName == null)
                return null;

            Tuple<ITypeMap, ITypeMap> typeMaps;
            if (!xmlTypeMaps.TryGetValue(qualifiedName, out typeMaps))
                typeMaps = AddTypeMap(qualifiedName);

            return isArray ? typeMaps?.Item2 : typeMaps?.Item1;
        }

        private ITypeMap AddTypeMap(Type runtimeType, IDictionary<Type, ITypeMap> partialTypeMaps)
        {
            if (runtimeType.IsXRoadSerializable() && Version.HasValue && !runtimeType.ExistsInVersion(Version.Value))
                throw XRoadException.UnknownType(runtimeType.ToString());

            var typeDefinition = schemaDefinitionReader.GetTypeDefinition(runtimeType);

            ITypeMap typeMap;

            var collectionDefinition = typeDefinition as CollectionDefinition;
            if (collectionDefinition != null)
            {
                var itemTypeMap = GetTypeMap(typeDefinition.Type.GetElementType(), partialTypeMaps);
                collectionDefinition.ItemDefinition = itemTypeMap.Definition;

                var typeMapType = typeof(ArrayTypeMap<>).MakeGenericType(itemTypeMap.Definition.Type);
                typeMap = (ITypeMap)Activator.CreateInstance(typeMapType, this, collectionDefinition, itemTypeMap);
                return runtimeTypeMaps.GetOrAdd(runtimeType, typeMap);
            }

            if (typeDefinition.Type.Assembly != contractAssembly)
                return null;

            if (!typeDefinition.Type.IsAbstract && typeDefinition.Type.GetConstructor(Type.EmptyTypes) == null)
                throw XRoadException.NoDefaultConstructorForType(typeDefinition.Type.Name);

            if (typeDefinition.Type.IsAbstract)
                typeMap = (ITypeMap)Activator.CreateInstance(typeof(AbstractTypeMap<>).MakeGenericType(typeDefinition.Type), typeDefinition);
            else if (typeDefinition.HasStrictContentOrder)
                typeMap = (ITypeMap)Activator.CreateInstance(typeof(SequenceTypeMap<>).MakeGenericType(typeDefinition.Type), this, typeDefinition);
            else
                typeMap = (ITypeMap)Activator.CreateInstance(typeof(AllTypeMap<>).MakeGenericType(typeDefinition.Type), this, typeDefinition);

            partialTypeMaps = partialTypeMaps ?? new Dictionary<Type, ITypeMap>();
            partialTypeMaps.Add(typeDefinition.Type, typeMap);
            typeMap.InitializeProperties(GetRuntimeProperties(typeDefinition, partialTypeMaps));
            partialTypeMaps.Remove(typeDefinition.Type);

            return runtimeTypeMaps.GetOrAdd(runtimeType, typeMap);
        }

        private Tuple<ITypeMap, ITypeMap> AddTypeMap(XName qualifiedName)
        {
            var runtimeType = GetRuntimeType(qualifiedName);
            if (runtimeType == null)
                return null;

            var typeDefinition = schemaDefinitionReader.GetTypeDefinition(runtimeType);

            if (!typeDefinition.Type.IsAbstract && typeDefinition.Type.GetConstructor(Type.EmptyTypes) == null)
                throw XRoadException.NoDefaultConstructorForType(typeDefinition.Name);

            ITypeMap typeMap;
            if (typeDefinition.Type.IsAbstract)
                typeMap = (ITypeMap)Activator.CreateInstance(typeof(AbstractTypeMap<>).MakeGenericType(typeDefinition.Type), typeDefinition);
            else if (typeDefinition.HasStrictContentOrder)
                typeMap = (ITypeMap)Activator.CreateInstance(typeof(SequenceTypeMap<>).MakeGenericType(typeDefinition.Type), this, typeDefinition);
            else
                typeMap = (ITypeMap)Activator.CreateInstance(typeof(AllTypeMap<>).MakeGenericType(typeDefinition.Type), this, typeDefinition);

            var arrayTypeMap = (ITypeMap)Activator.CreateInstance(typeof(ArrayTypeMap<>).MakeGenericType(typeDefinition.Type), this, schemaDefinitionReader.GetCollectionDefinition(typeDefinition), typeMap);

            var partialTypeMaps = new Dictionary<Type, ITypeMap>
            {
                { typeMap.Definition.Type, typeMap },
                { arrayTypeMap.Definition.Type, arrayTypeMap }
            };

            typeMap.InitializeProperties(GetRuntimeProperties(typeDefinition, partialTypeMaps));

            return xmlTypeMaps.GetOrAdd(qualifiedName, Tuple.Create(typeMap, arrayTypeMap));
        }

        private Type GetRuntimeType(XName qualifiedName)
        {
            if (!qualifiedName.NamespaceName.StartsWith("http://", StringComparison.InvariantCulture))
            {
                var type = contractAssembly.GetType($"{qualifiedName.Namespace}.{qualifiedName.LocalName}");
                return type != null && type.IsXRoadSerializable() ? type : null;
            }

            if (!Protocol.ProducerNamespace.Equals(qualifiedName.NamespaceName))
                throw XRoadException.TundmatuNimeruum(qualifiedName.NamespaceName);

            var runtimeType = contractAssembly.GetTypes()
                                              .Where(type => type.Name.Equals(qualifiedName.LocalName))
                                              .Where(type => !Version.HasValue || type.ExistsInVersion(Version.Value))
                                              .SingleOrDefault(type => type.IsXRoadSerializable());
            if (runtimeType != null)
                return runtimeType;

            throw XRoadException.UnknownType(qualifiedName.ToString());
        }
        
        public XName GetXmlTypeName(Type type)
        {
            if (type.IsNullable())
                return GetXmlTypeName(Nullable.GetUnderlyingType(type));

            switch (type.FullName)
            {
                case "System.Byte": return XName.Get("byte", NamespaceConstants.XSD);
                case "System.DateTime": return XName.Get("dateTime", NamespaceConstants.XSD);
                case "System.Boolean": return XName.Get("boolean", NamespaceConstants.XSD);
                case "System.Single": return XName.Get("float", NamespaceConstants.XSD);
                case "System.Double": return XName.Get("double", NamespaceConstants.XSD);
                case "System.Decimal": return XName.Get("decimal", NamespaceConstants.XSD);
                case "System.Int64": return XName.Get("long", NamespaceConstants.XSD);
                case "System.Int32": return XName.Get("int", NamespaceConstants.XSD);
                case "System.String": return XName.Get("string", NamespaceConstants.XSD);
            }

            if (type.Assembly == contractAssembly)
                return XName.Get(type.Name, Protocol.ProducerNamespace);

            throw XRoadException.AndmetüübileVastavNimeruumPuudub(type.FullName);
        }

        private void CreateTestSystem(ILegacyProtocol legacyProtocol)
        {
            var methodInfo = typeof(MockMethods).GetMethod("TestSystem");

            var operationDefinition = schemaDefinitionReader.GetOperationDefinition(methodInfo, XName.Get("testSystem", legacyProtocol.XRoadNamespace), 1u);
            operationDefinition.State = DefinitionState.Hidden;

            serviceMaps.GetOrAdd(operationDefinition.Name, new ServiceMap(operationDefinition, null, null));
        }

        private void CreateGetState(ILegacyProtocol legacyProtocol)
        {
            var methodInfo = typeof(MockMethods).GetMethod("GetState");

            var operationDefinition = schemaDefinitionReader.GetOperationDefinition(methodInfo, XName.Get("getState", legacyProtocol.XRoadNamespace), 1u);
            operationDefinition.State = DefinitionState.Hidden;

            var outputTypeMap = GetTypeMap(methodInfo.ReturnType);
            var serviceMap = new ServiceMap(operationDefinition, null, outputTypeMap);

            serviceMaps.GetOrAdd(operationDefinition.Name, serviceMap);
        }

        private void CreateListMethods(ILegacyProtocol legacyProtocol)
        {
            var methodInfo = typeof(MockMethods).GetMethod("ListMethods");

            var operationDefinition = schemaDefinitionReader.GetOperationDefinition(methodInfo, XName.Get("listMethods", legacyProtocol.XRoadNamespace), 1u);
            operationDefinition.State = DefinitionState.Hidden;

            var outputTypeMap = GetTypeMap(methodInfo.ReturnType);
            serviceMaps.GetOrAdd(operationDefinition.Name, new ServiceMap(operationDefinition, null, outputTypeMap));
        }

        private void AddSystemType<T>(string typeName, Func<TypeDefinition, ITypeMap> createTypeMap)
        {
            var typeDefinition = schemaDefinitionReader.GetSimpleTypeDefinition<T>(typeName);

            var typeMap = GetCustomTypeMap(typeDefinition.TypeMapType) ?? createTypeMap(typeDefinition);

            if (typeDefinition.Type != null)
                runtimeTypeMaps.TryAdd(typeDefinition.Type, typeMap);

            var collectionDefinition = schemaDefinitionReader.GetCollectionDefinition(typeDefinition);
            var arrayTypeMap = GetCustomTypeMap(collectionDefinition.TypeMapType) ?? new ArrayTypeMap<T>(this, collectionDefinition, typeMap);

            if (collectionDefinition.Type != null)
                runtimeTypeMaps.TryAdd(collectionDefinition.Type, arrayTypeMap);

            if (typeDefinition.Name != null)
                xmlTypeMaps.TryAdd(typeDefinition.Name, Tuple.Create(typeMap, arrayTypeMap));
        }

        private IEnumerable<Tuple<PropertyDefinition, ITypeMap>> GetRuntimeProperties(TypeDefinition typeDefinition, IDictionary<Type, ITypeMap> partialTypeMaps)
        {
            return typeDefinition.Type
                                 .GetAllPropertiesSorted(typeDefinition.ContentComparer, Version, p => schemaDefinitionReader.GetPropertyDefinition(p, typeDefinition))
                                 .Where(d => d.State != DefinitionState.Ignored)
                                 .Select(p =>
                                 {
                                     var typeMap = GetContentDefinitionTypeMap(p, partialTypeMaps);
                                     p.TypeName = typeMap.Definition.Name;
                                     return Tuple.Create(p, typeMap);
                                 });
        }

        private ITypeMap GetContentDefinitionTypeMap(IContentDefinition contentDefinition, IDictionary<Type, ITypeMap> partialTypeMaps)
        {
            var runtimeType = contentDefinition.RuntimeType;

            return contentDefinition.TypeName == null
                ? GetTypeMap(runtimeType, partialTypeMaps)
                : GetTypeMap(contentDefinition.TypeName, runtimeType.IsArray);
        }

        private ITypeMap GetCustomTypeMap(Type typeMapType)
        {
            if (typeMapType == null)
                return null;

            ITypeMap typeMap;
            if (customTypeMaps.TryGetValue(typeMapType, out typeMap))
                return typeMap;

            typeMap = (ITypeMap)Activator.CreateInstance(typeMapType, null, this);

            return customTypeMaps.GetOrAdd(typeMapType, typeMap);
        }
    }
}